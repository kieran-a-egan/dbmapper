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
                Check(first.OrderBy(x => x.Key).SequenceEqual(ReadBundle(packagedOutput).OrderBy(x => x.Key)), "installed server bundle matches library output");
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

            await Execute(admin, "DENY VIEW ANY DATABASE TO [dbmapper_test_reader];");
            Check(await Cli.RunAsync(args, stdout, stderr) == 2 && stderr.ToString().Contains("VIEW ANY DATABASE", StringComparison.Ordinal), "hidden server listing refused");
            Check(partialFiles.OrderBy(x => x.Key).SequenceEqual(ReadBundle(output).OrderBy(x => x.Key)), "discovery failure preserves previous server bundle");
            await Execute(admin, "REVOKE VIEW ANY DATABASE FROM [dbmapper_test_reader];");
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();
            Check(await Cli.RunAsync(args, stdout, stderr, cancellation.Token) == 130 && partialFiles.OrderBy(x => x.Key).SequenceEqual(ReadBundle(output).OrderBy(x => x.Key)), "server cancellation preserves output");
            File.AppendAllText(Path.Combine(output, "server.md"), "Handwritten server note");
            Check(await Cli.RunAsync(args, stdout, stderr) == 2, "edited server output protected");
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
