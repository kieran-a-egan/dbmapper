using System.Diagnostics;
using DbMapper;
using Microsoft.Data.SqlClient;

internal static partial class Program
{
    private static async Task ServerIntegration(SqlConnection admin, string adminSecret, string metadataSecret, string originalDatabase, string project, string secretId, string temp)
    {
        Console.WriteLine("Testing server discovery, isolated databases, named output and partial scan reporting...");
        var second = "DbMapperTest_;]Second_" + Guid.NewGuid().ToString("N");
        var restricted = "DbMapperTest_Restricted_" + Guid.NewGuid().ToString("N");
        var inaccessible = "DbMapperTest_NoAccess_" + Guid.NewGuid().ToString("N");
        var offline = "DbMapperTest_Offline_" + Guid.NewGuid().ToString("N");
        var created = new List<string>();
        var output = Path.Combine(temp, "server-bundle");
        var args = new[] { "--project", project, "--output", output, "--all-databases" };
        try
        {
            await Execute(admin, "CREATE DATABASE " + Quote(second));
            created.Add(second);
            await using (var fixture = new SqlConnection(new SqlConnectionStringBuilder(adminSecret) { InitialCatalog = second }.ConnectionString))
            {
                await fixture.OpenAsync();
                await Execute(fixture, """
                    CREATE TABLE dbo.Customers (WarehouseId int NOT NULL PRIMARY KEY, Note nvarchar(30) DEFAULT N'PRIVATE_SERVER_DEFAULT');
                    INSERT dbo.Customers (WarehouseId, Note) VALUES (1, N'PRIVATE_SERVER_ROW');
                    CREATE USER [dbmapper_test_reader] FOR LOGIN [dbmapper_test_reader];
                    GRANT CONNECT, VIEW DEFINITION TO [dbmapper_test_reader];
                    DENY SELECT, INSERT, UPDATE, DELETE, EXECUTE ON SCHEMA::dbo TO [dbmapper_test_reader];
                    """);
            }
            await Execute(admin, "ALTER DATABASE " + Quote(second) + " SET READ_ONLY WITH ROLLBACK IMMEDIATE;");
            var complete = await ServerReader.ReadAsync(adminSecret, 10, CancellationToken.None);
            foreach (var failed in complete.Where(d => d.Schema is null))
            {
                Console.Error.WriteLine("Unexpected fixture scan status: " + failed.Status);
                await CatalogReader.ReadAsync(adminSecret, 10, CancellationToken.None, failed.Name);
            }
            Check(complete.All(d => d.Schema is not null), "all discovered databases exported with sufficient metadata access");
            Check(complete.Count(d => d.IsSystem) == 4, "master model msdb tempdb included");
            Check(complete.Single(d => d.Name == originalDatabase).Schema!.Relations.Count == 3, "first database retains its objects");
            Check(complete.Single(d => d.Name == second).Schema!.Relations.Single().Columns.Any(c => c.Name == "WarehouseId"), "database name metacharacters handled as literal connection catalog");
            var completeFiles = OkfBundle.RenderServer(complete);
            ValidateBundle(completeFiles);
            Check(completeFiles.Values.Any(v => v.Contains("title: \"" + second, StringComparison.Ordinal)), "server output includes database names");
            Check(!string.Join("\n", completeFiles.Values).Contains("PRIVATE_", StringComparison.Ordinal), "server export excludes row and definition sentinels");

            // Reuse the actual user-secrets CLI workflow for a complete server export.
            Check(await SetSecrets(secretId, new() { ["ConnectionStrings:Metadata"] = adminSecret }) == 0, "server admin fixture secret setup");
            var stdout = new StringWriter();
            var stderr = new StringWriter();
            Check(await Cli.RunAsync(args, stdout, stderr) == 0, "complete server CLI exits zero");
            var first = ReadBundle(output);
            Check(await Cli.RunAsync(args, stdout, stderr) == 0 && first.OrderBy(x => x.Key).SequenceEqual(ReadBundle(output).OrderBy(x => x.Key)), "complete server refresh is deterministic");

            var selectedOutput = Path.Combine(temp, "selected-bundle");
            var selectedArgs = new[] { "--project", project, "--output", selectedOutput, "--database", originalDatabase, "--database", second };
            Check(await Cli.RunAsync(selectedArgs, stdout, stderr) == 0, "selected databases CLI succeeds");
            var selectedFirst = ReadBundle(selectedOutput);
            Check(Directory.GetDirectories(Path.Combine(selectedOutput, "databases")).Length == 2
                && selectedFirst["server.md"].Contains("2 of 2 selected databases", StringComparison.Ordinal), "selection excludes unrequested system databases");
            Check(await Cli.RunAsync(selectedArgs.Concat(["--database", "PRIVATE_MISSING_DATABASE"]).ToArray(), stdout, stderr) == 2
                && selectedFirst.OrderBy(p => p.Key).SequenceEqual(ReadBundle(selectedOutput).OrderBy(p => p.Key)), "unknown selection preserves prior bundle");

            // Change both fixtures: a targeted update must pick up only the chosen
            // database, even when another database's live metadata has also changed.
            foreach (var name in new[] { originalDatabase, second })
            {
                await Execute(admin, "ALTER DATABASE " + Quote(name) + " SET READ_WRITE WITH ROLLBACK IMMEDIATE;");
                await using (var fixture = new SqlConnection(new SqlConnectionStringBuilder(adminSecret) { InitialCatalog = name }.ConnectionString))
                {
                    await fixture.OpenAsync();
                    await Execute(fixture, "CREATE TABLE dbo.AfterWork (Id int NOT NULL);");
                    if (name == second) await Execute(fixture, "DROP TABLE dbo.Customers;");
                }
                await Execute(admin, "ALTER DATABASE " + Quote(name) + " SET READ_ONLY WITH ROLLBACK IMMEDIATE;");
            }
            var updateArgs = new[] { "--project", project, "--output", selectedOutput, "--update-database", second };
            Check(await Cli.RunAsync(updateArgs, stdout, stderr) == 0, "targeted CLI update after schema work");
            var updated = ReadBundle(selectedOutput);
            var originalDirectory = Path.Combine("databases", "db-" + OkfBundle.Slug(originalDatabase)) + Path.DirectorySeparatorChar;
            var secondDirectory = Path.Combine("databases", "db-" + OkfBundle.Slug(second)) + Path.DirectorySeparatorChar;
            Check(selectedFirst.Where(p => p.Key.StartsWith(originalDirectory, StringComparison.Ordinal)).All(p => updated[p.Key] == p.Value), "targeted CLI preserves unselected database bytes despite live changes");
            Check(updated.Keys.Any(p => p.StartsWith(secondDirectory, StringComparison.Ordinal) && p.EndsWith("table-" + OkfBundle.Slug("AfterWork") + ".md", StringComparison.Ordinal))
                && !updated.Keys.Any(p => p.StartsWith(secondDirectory, StringComparison.Ordinal) && p.EndsWith("table-" + OkfBundle.Slug("Customers") + ".md", StringComparison.Ordinal)), "targeted CLI adds new object and removes stale target object");
            Check(await Cli.RunAsync(updateArgs, stdout, stderr) == 0 && updated.OrderBy(p => p.Key).SequenceEqual(ReadBundle(selectedOutput).OrderBy(p => p.Key)), "targeted CLI refresh is deterministic");
            await Execute(admin, "ALTER DATABASE " + Quote(second) + " SET OFFLINE WITH ROLLBACK IMMEDIATE;");
            try
            {
                Check(await Cli.RunAsync(updateArgs, stdout, stderr) == 3
                    && updated.OrderBy(p => p.Key).SequenceEqual(ReadBundle(selectedOutput).OrderBy(p => p.Key)), "targeted SQL failure preserves previous schema files");
            }
            finally { await Execute(admin, "ALTER DATABASE " + Quote(second) + " SET ONLINE;"); }
            Check(await Cli.RunAsync(["--project", project, "--output", selectedOutput, "--update-database", "PRIVATE_MISSING_DATABASE"], stdout, stderr) == 2
                && updated.OrderBy(p => p.Key).SequenceEqual(ReadBundle(selectedOutput).OrderBy(p => p.Key)), "unknown update target preserves bundle");
            using (var cancelledUpdate = new CancellationTokenSource())
            {
                cancelledUpdate.Cancel();
                Check(await Cli.RunAsync(updateArgs, stdout, stderr, cancelledUpdate.Token) == 130
                    && updated.OrderBy(p => p.Key).SequenceEqual(ReadBundle(selectedOutput).OrderBy(p => p.Key)), "targeted cancellation preserves entire bundle");
            }

            var packagedTool = Environment.GetEnvironmentVariable("DBMAPPER_TEST_TOOL");
            if (!string.IsNullOrEmpty(packagedTool))
            {
                var packagedOutput = Path.Combine(temp, "packaged-server-bundle");
                var start = new ProcessStartInfo(packagedTool) { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true, WorkingDirectory = temp };
                foreach (var arg in new[] { "--all-databases", "--output", packagedOutput }) start.ArgumentList.Add(arg);
                using var process = Process.Start(start)!;
                var toolOut = process.StandardOutput.ReadToEndAsync();
                var toolError = process.StandardError.ReadToEndAsync();
                await process.WaitForExitAsync();
                var diagnostics = await toolOut + await toolError;
                Check(process.ExitCode == 0 && diagnostics.Contains("discovered databases", StringComparison.Ordinal), "installed command supports all-databases");
                Check(await Cli.RunAsync(args, stdout, stderr) == 0 && ReadBundle(output).OrderBy(x => x.Key).SequenceEqual(ReadBundle(packagedOutput).OrderBy(x => x.Key)), "installed server bundle matches library output");
                start.ArgumentList.Clear();
                foreach (var arg in new[] { "--database", originalDatabase, "--database", second, "--output", packagedOutput }) start.ArgumentList.Add(arg);
                using var selectedProcess = Process.Start(start)!;
                var selectedOut = selectedProcess.StandardOutput.ReadToEndAsync();
                var selectedError = selectedProcess.StandardError.ReadToEndAsync();
                await selectedProcess.WaitForExitAsync();
                await Task.WhenAll(selectedOut, selectedError);
                Check(selectedProcess.ExitCode == 0 && Directory.GetDirectories(Path.Combine(packagedOutput, "databases")).Length == 2, "installed command supports database selection");
                start.ArgumentList.Clear();
                foreach (var arg in new[] { "--update-database", second, "--output", packagedOutput }) start.ArgumentList.Add(arg);
                var packagedBeforeUpdate = ReadBundle(packagedOutput);
                using var updateProcess = Process.Start(start)!;
                var updateOut = updateProcess.StandardOutput.ReadToEndAsync();
                var updateError = updateProcess.StandardError.ReadToEndAsync();
                await updateProcess.WaitForExitAsync();
                await Task.WhenAll(updateOut, updateError);
                Check(updateProcess.ExitCode == 0 && packagedBeforeUpdate.OrderBy(p => p.Key).SequenceEqual(ReadBundle(packagedOutput).OrderBy(p => p.Key)), "installed command supports targeted update");
            }

            foreach (var name in new[] { restricted, inaccessible, offline })
            {
                await Execute(admin, "CREATE DATABASE " + Quote(name));
                created.Add(name);
            }
            await using (var fixture = new SqlConnection(new SqlConnectionStringBuilder(adminSecret) { InitialCatalog = restricted }.ConnectionString))
            {
                await fixture.OpenAsync();
                await Execute(fixture, "CREATE USER [dbmapper_test_reader] FOR LOGIN [dbmapper_test_reader]; GRANT CONNECT TO [dbmapper_test_reader];");
            }
            await Execute(admin, "ALTER DATABASE " + Quote(offline) + " SET OFFLINE WITH ROLLBACK IMMEDIATE;");
            var adminPartial = await ServerReader.ReadAsync(adminSecret, 10, CancellationToken.None);
            Check(adminPartial.Single(d => d.Name == offline) is { Schema: null, Status: "Skipped: database is not online." }, "offline database is reported without opening it");
            var partial = await ServerReader.ReadAsync(metadataSecret, 10, CancellationToken.None);
            Check(partial.Single(d => d.Name == originalDatabase).Schema is not null && partial.Single(d => d.Name == second).Schema is not null, "row-denied login scans multiple databases");
            Check(partial.Single(d => d.Name == restricted).Schema is null && partial.Single(d => d.Name == restricted).Status.Contains("VIEW DEFINITION", StringComparison.Ordinal), "per-database metadata permission failure recorded");
            Check(partial.Single(d => d.Name == inaccessible).Schema is null && partial.Single(d => d.Name == inaccessible).Status.Contains("access is unavailable", StringComparison.Ordinal), "inaccessible database recorded");
            Check(await SetSecrets(secretId, new() { ["ConnectionStrings:Metadata"] = metadataSecret }) == 0, "metadata-only server fixture secret setup");
            Check(await Cli.RunAsync(args, stdout, stderr) == 4 && stderr.ToString().Contains("incomplete", StringComparison.Ordinal), "partial server export exits four with a report");
            var partialFiles = ReadBundle(output);
            Check(partialFiles["server.md"].Contains("Incomplete:", StringComparison.Ordinal), "written report marks partial export");
            Check(!Directory.GetFiles(output, "*.md", SearchOption.AllDirectories).Any(p => File.ReadAllText(p).Contains(new SqlConnectionStringBuilder(metadataSecret).Password, StringComparison.Ordinal)), "server output does not contain credentials");
            Check(await Cli.RunAsync(args, stdout, stderr) == 4 && partialFiles.OrderBy(x => x.Key).SequenceEqual(ReadBundle(output).OrderBy(x => x.Key)), "partial server refresh is deterministic");
            Check(await Cli.RunAsync(selectedArgs, stdout, stderr) == 0, "unselected inaccessible databases cannot make a selection incomplete");
            var selectedWithFailures = new[] { "--project", project, "--output", selectedOutput, "--database", second, "--database", restricted };
            Check(await Cli.RunAsync(selectedWithFailures, stdout, stderr) == 4, "selected inaccessible metadata gets a partial status");
            var beforeFailure = ReadBundle(selectedOutput);
            Check(await Cli.RunAsync(["--project", project, "--output", selectedOutput, "--update-database", restricted], stdout, stderr) == 2
                && beforeFailure.OrderBy(p => p.Key).SequenceEqual(ReadBundle(selectedOutput).OrderBy(p => p.Key)), "failed targeted metadata scan preserves bundle instead of replacing target with status page");

            Check(await SetSecrets(secretId, new() { ["ConnectionStrings:Metadata"] = adminSecret }) == 0, "target recovery fixture secret setup");
            Check(await Cli.RunAsync(["--project", project, "--output", selectedOutput, "--update-database", restricted], stdout, stderr) == 0
                && ReadBundle(selectedOutput)["server.md"].Contains("All selected databases exported. Exported 2 of 2", StringComparison.Ordinal), "target recovery refreshes aggregate coverage");
            Check(await SetSecrets(secretId, new() { ["ConnectionStrings:Metadata"] = metadataSecret }) == 0, "restore metadata login for discovery guard");

            await Execute(admin, "DENY VIEW ANY DATABASE TO [dbmapper_test_reader];");
            Check(await Cli.RunAsync(args, stdout, stderr) == 2 && stderr.ToString().Contains("VIEW ANY DATABASE", StringComparison.Ordinal), "hidden server listing refused");
            Check(partialFiles.OrderBy(x => x.Key).SequenceEqual(ReadBundle(output).OrderBy(x => x.Key)), "discovery failure preserves previous server bundle");
            Check(await Cli.RunAsync(updateArgs, stdout, stderr) == 0, "targeted update needs no server discovery permission");
            await Execute(admin, "REVOKE VIEW ANY DATABASE FROM [dbmapper_test_reader];");
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();
            Check(await Cli.RunAsync(args, stdout, stderr, cancellation.Token) == 130 && partialFiles.OrderBy(x => x.Key).SequenceEqual(ReadBundle(output).OrderBy(x => x.Key)), "server cancellation preserves output");
            File.AppendAllText(Path.Combine(output, "server.md"), "Handwritten server note");
            Check(await Cli.RunAsync(args, stdout, stderr) == 2, "edited server output protected");
            File.AppendAllText(Path.Combine(selectedOutput, "server.md"), "Handwritten selected note");
            Check(await Cli.RunAsync(updateArgs, stdout, stderr) == 2 && File.ReadAllText(Path.Combine(selectedOutput, "server.md")).Contains("Handwritten selected note", StringComparison.Ordinal), "targeted update protects hand-written output");
            Check(!(stdout.ToString() + stderr.ToString()).Contains("PRIVATE_", StringComparison.Ordinal), "database selection diagnostics do not echo raw arguments");
        }
        finally
        {
            foreach (var name in created)
            {
                if (name == offline) await Execute(admin, "ALTER DATABASE " + Quote(name) + " SET ONLINE;");
                await Execute(admin, "ALTER DATABASE " + Quote(name) + " SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE " + Quote(name) + ";");
            }
        }
    }

    private static string Quote(string identifier) => "[" + identifier.Replace("]", "]]", StringComparison.Ordinal) + "]";
}
