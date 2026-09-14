using System.Text;
using System.Text.RegularExpressions;
using DbMapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

internal static partial class Program
{
    private static int checks;

    public static async Task<int> Main(string[] args)
    {
        try
        {
            await SelfTest();
            await SqlFileTests();
            if (args.Contains("--integration", StringComparer.Ordinal)) await Integration();
            if (args is ["--write-example", var destination])
            {
                using var writer = new BundleWriter(destination);
                writer.Write(OkfBundle.Render(Example()));
            }
            if (args is ["--write-server-example", var serverDestination])
            {
                using var writer = new BundleWriter(serverDestination);
                writer.Write(OkfBundle.RenderServer(ServerExample()));
            }
            Console.WriteLine($"PASS: {checks} checks" + (args.Contains("--integration", StringComparer.Ordinal) ? " including live SQL Server, CLI and user-secrets integration." : "."));
            return 0;
        }
        catch (Exception e)
        {
            // Test secrets must not appear in failure logs either.
            Console.Error.WriteLine(e is CheckFailure ? e.Message : "FAIL: test execution failed: " + (e is SqlException sql ? "SQL error " + sql.Number : e.GetType().Name) + " (message withheld to protect test credentials).");
            if (e is not CheckFailure) Console.Error.WriteLine(e.StackTrace);
            return 1;
        }
    }

    private static async Task SelfTest()
    {
        Throws(() => Options.Parse(["--connection", "A", "--secret-key", "B"]), "ambiguous key options");
        Throws(() => Options.Parse(["--timeout", "0"]), "unbounded timeout");
        Throws(() => Options.Parse(["--timeout", "301"]), "excessive timeout");
        Throws(() => Options.Parse(["--output"]), "missing argument");
        Throws(() => Options.Parse(["--connection", "A", "--connection", "B"]), "duplicate argument");
        Check(Options.Parse(["--connection", "Main"]).SecretKey == "ConnectionStrings:Main", "named secret mapping");
        Check(!Options.Parse([]).AllDatabases, "single database remains the default");
        Check(Options.Parse(["--all-databases", "--connection", "Main", "--timeout", "7"]).AllDatabases, "server flag combines with value options");
        Throws(() => Options.Parse(["--all-databases", "--all-databases"]), "duplicate server flag refused");
        Check(Options.Parse(["--database", "Application", "--database", "Sales, Archive"]).Databases.SequenceEqual(["Application", "Sales, Archive"]), "repeatable exact database selection without comma splitting");
        Check(Options.Parse(["--update-database", "Application"]).UpdateDatabase == "Application", "targeted update option");
        Throws(() => Options.Parse(["--database"]), "missing database selection");
        Throws(() => Options.Parse(["--update-database", " "]), "empty update target");
        Throws(() => Options.Parse(["--database", "App", "--database", "App"]), "duplicate selected database refused");
        Throws(() => Options.Parse(["--update-database", "App", "--update-database", "Other"]), "multiple update targets refused");
        Throws(() => Options.Parse(["--all-databases", "--database", "App"]), "all and selected modes conflict");
        Throws(() => Options.Parse(["--all-databases", "--update-database", "App"]), "all and update modes conflict");
        Throws(() => Options.Parse(["--database", "App", "--update-database", "App"]), "selection and update modes conflict");
        Check(ServerReader.SelectNames(["App", "app", "Other"], ["App"]).SetEquals(["App"]), "selection excludes other and case-distinct databases");
        Check(ServerReader.SelectNames(["App", "app"], Options.Parse(["--database", "App", "--database", "app"]).Databases).Count == 2, "case-distinct selections remain distinct");
        Throws(() => ServerReader.SelectNames(["App"], ["app"]), "selection must match exact case");
        Throws(() => ServerReader.SelectNames(["App"], ["App", "Missing"]), "missing selection is not silently omitted");
        Throws(() => ServerReader.SelectNames(["App"], []), "empty selection cannot scan all databases");
        var target = "Odd; Database=another]";
        var changed = new SqlConnectionStringBuilder(CatalogReader.PrepareConnection("Server=localhost;Integrated Security=true", 9, target));
        Check(changed.InitialCatalog == target && changed.DataSource == "localhost" && changed.IntegratedSecurity, "database switching is literal and preserves authentication");
        Throws(() => CatalogReader.PrepareConnection("Server=localhost;AttachDBFilename=C:\\private.mdf", 30, "master"), "server mode cannot bypass attach guard");
        var prepared = new SqlConnectionStringBuilder(CatalogReader.PrepareConnection("Server=localhost;Database=Synthetic;Integrated Security=true;Encrypt=true", 9));
        Check(prepared.ApplicationIntent == ApplicationIntent.ReadOnly && !prepared.Enlist && !prepared.Pooling && !prepared.PersistSecurityInfo && prepared.ConnectRetryCount == 0 && prepared.ConnectTimeout == 9, "connection safeguards");
        Check(!prepared.TrustServerCertificate && prepared.Encrypt == SqlConnectionEncryptOption.Mandatory, "TLS preserved");
        Throws(() => CatalogReader.PrepareConnection("Server=localhost;Integrated Security=true", 30), "explicit database required");
        Throws(() => CatalogReader.PrepareConnection("Server=localhost;Database=test;AttachDBFilename=C:\\private.mdf", 30), "attach file refused");
        Throws(() => CatalogReader.PrepareConnection("Server=localhost;Database=test;User Instance=true", 30), "user instance refused");

        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Only"] = "SYNTHETIC_CONNECTION",
            ["Unrelated:Key"] = "UNRELATED_SECRET"
        }).Build();
        Check(ProjectSecrets.Select(config, null) == "SYNTHETIC_CONNECTION", "single secret selection");
        config["ConnectionStrings:Second"] = "SECOND_CONNECTION";
        Throws(() => ProjectSecrets.Select(config, null), "multiple secrets refused");
        Check(ProjectSecrets.Select(config, "ConnectionStrings:Second") == "SECOND_CONNECTION", "explicit secret selection");
        Throws(() => ProjectSecrets.Select(config, "Missing"), "missing secret refused");
        (config as IDisposable)?.Dispose();

        var temp = NewTemp();
        try
        {
            var project = Path.Combine(temp, "App.csproj");
            File.WriteAllText(project, "<Project><PropertyGroup><UserSecretsId>synthetic-id</UserSecretsId></PropertyGroup></Project>");
            Check(ProjectSecrets.FindId(temp) == "synthetic-id", "project discovery");
            File.WriteAllText(project, "<Project><PropertyGroup Condition=\"'$(Configuration)'=='Debug'\"><UserSecretsId>id</UserSecretsId></PropertyGroup></Project>");
            Throws(() => ProjectSecrets.FindId(project), "conditional ID refused");
            File.WriteAllText(project, "<Project><PropertyGroup><UserSecretsId>$(ImportedId)</UserSecretsId></PropertyGroup></Project>");
            Throws(() => ProjectSecrets.FindId(project), "evaluated ID refused");
            File.WriteAllText(project, "<Project />");
            Throws(() => ProjectSecrets.FindId(project), "missing ID refused");
            File.WriteAllText(Path.Combine(temp, "Other.csproj"), "<Project />");
            Throws(() => ProjectSecrets.FindId(temp), "ambiguous project refused");

            var model = Example();
            var files = OkfBundle.Render(model);
            ValidateBundle(files);
            var databases = ServerExample();
            var serverFiles = OkfBundle.RenderServer(databases);
            ValidateBundle(serverFiles);
            Check(serverFiles.OrderBy(x => x.Key).SequenceEqual(OkfBundle.RenderServer(databases.AsEnumerable().Reverse().ToList()).OrderBy(x => x.Key)), "deterministic server database order");
            Check(serverFiles.Keys.Count(p => p.EndsWith("table-" + OkfBundle.Slug("Customers") + ".md", StringComparison.Ordinal)) == 2, "same table and object IDs isolated across databases");
            Check(serverFiles["server.md"].Contains("Incomplete:", StringComparison.Ordinal) && serverFiles.Values.Any(v => v.Contains("not indicate an empty database", StringComparison.Ordinal)), "partial scan coverage is explicit");
            var unusual = new[] { new DatabaseScan("../CON|[name]\n---\ntype: Evil", false, Example(), "Exported.") };
            var unusualFiles = OkfBundle.RenderServer(unusual);
            ValidateBundle(unusualFiles);
            Check(unusualFiles.Keys.All(p => !p.Contains("..", StringComparison.Ordinal)) && unusualFiles.Values.All(v => !v.Contains("\ntype: Evil", StringComparison.Ordinal)), "database names cannot inject paths or frontmatter");
            Throws(() => OkfBundle.RenderServer([databases[0], databases[0]]), "duplicate database paths refused");
            var selectedFiles = OkfBundle.RenderServer([databases[0], databases[1]], selectedOnly: true);
            Check(selectedFiles["server.md"].Contains("2 of 2 selected databases", StringComparison.Ordinal) && !selectedFiles.Values.Any(v => v.Contains("Archive", StringComparison.Ordinal)), "selected bundle reports only selected scope");
            var changedServer = OkfBundle.UpdateServerDatabase(serverFiles, "Application", new SchemaModel());
            ValidateBundle(changedServer);
            var applicationDirectory = "databases/db-" + OkfBundle.Slug("Application") + "/";
            Check(!changedServer.Keys.Any(p => p.StartsWith(applicationDirectory + "schemas/", StringComparison.Ordinal)), "targeted update removes stale target schemas");
            Check(serverFiles.Where(p => p.Key.StartsWith("databases/", StringComparison.Ordinal) && !p.Key.StartsWith(applicationDirectory, StringComparison.Ordinal))
                .All(p => changedServer[p.Key] == p.Value), "targeted update preserves other database pages and unchanged directory index");
            Check(changedServer["server.md"].Contains("2 of 3 discovered databases", StringComparison.Ordinal), "targeted update retains unrelated failure status");
            var recovered = OkfBundle.UpdateServerDatabase(changedServer, "Archive", Example());
            ValidateBundle(recovered);
            Check(recovered["server.md"].Contains("All discovered databases exported. Exported 3 of 3", StringComparison.Ordinal)
                && !recovered["index.md"].Contains("Skipped:", StringComparison.Ordinal)
                && !recovered["databases/index.md"].Contains("Skipped:", StringComparison.Ordinal), "target recovery updates report totals and navigation");
            Check(recovered.OrderBy(p => p.Key).SequenceEqual(OkfBundle.UpdateServerDatabase(recovered, "Archive", Example()).OrderBy(p => p.Key)), "unchanged targeted update is deterministic");
            Check(OkfBundle.UpdateServerDatabase(selectedFiles, "Application", model)["server.md"].Contains("2 of 2 selected databases", StringComparison.Ordinal), "targeted update retains selected scope");
            ValidateBundle(OkfBundle.UpdateServerDatabase(unusualFiles, unusual[0].Name, new SchemaModel()));
            Throws(() => OkfBundle.UpdateServerDatabase(serverFiles, "application", model), "targeted update requires exact existing name");
            Throws(() => OkfBundle.UpdateServerDatabase(files, "Application", model), "single database bundle cannot be mistaken for a server bundle");
            var missingIndex = new Dictionary<string, string>(serverFiles) { ["index.md"] = OkfBundle.IndexHeader };
            Throws(() => OkfBundle.UpdateServerDatabase(missingIndex, "Application", model), "incomplete navigation refuses targeted update");
            var reordered = Example();
            reordered.Relations.Reverse();
            reordered.Relations[0].Columns.Reverse();
            Check(files.OrderBy(x => x.Key).SequenceEqual(OkfBundle.Render(reordered).OrderBy(x => x.Key)), "deterministic ordering");
            Check(files.Values.Any(v => v.Contains("References [dbo.Customers]", StringComparison.Ordinal)), "outgoing relationship link");
            Check(files.Values.Any(v => v.Contains("via <code>FK", StringComparison.Ordinal)), "incoming relationship link");

            var hostile = new SchemaModel();
            hostile.Relations.Add(new(9, "../CON:<schema>", "log\n---\ntype: Evil\n[x](https://bad.invalid)|`<script>", "Table", "NON_TEMPORAL_TABLE"));
            hostile.Relations.Add(new(10, "../CON:<schema>", "LOG\n---\ntype: Evil\n[x](https://bad.invalid)|`<script>", "Table", "NON_TEMPORAL_TABLE"));
            var hostileFiles = OkfBundle.Render(hostile);
            ValidateBundle(hostileFiles);
            Check(hostileFiles.Keys.All(p => !p.Contains("..", StringComparison.Ordinal) && p.Length < 150), "bounded traversal-safe filenames");
            Check(hostileFiles.Values.All(v => !v.Contains("<script>", StringComparison.Ordinal) && !v.Contains("\ntype: Evil", StringComparison.Ordinal)), "Markdown and YAML injection escaped");
            Check(OkfBundle.Slug("Orders") != OkfBundle.Slug("orders"), "case-distinct names have distinct paths");

            var output = Path.Combine(temp, "bundle");
            using (var writer = new BundleWriter(output))
            {
                writer.Write(files);
                Throws(() => { using var competing = new BundleWriter(output); }, "concurrent writer refused");
            }
            var first = ReadBundle(output);
            using (var writer = new BundleWriter(output))
                Check(writer.Read().OrderBy(p => p.Key).SequenceEqual(files.OrderBy(p => p.Key)), "bundle read strips only integrity footers");
            var serverOutput = Path.Combine(temp, "server-bundle");
            using (var writer = new BundleWriter(serverOutput)) writer.Write(serverFiles);
            using (var writer = new BundleWriter(serverOutput)) writer.Write(OkfBundle.UpdateServerDatabase(writer.Read(), "Archive", Example()));
            using (var writer = new BundleWriter(serverOutput))
                Check(writer.Read().OrderBy(p => p.Key).SequenceEqual(OkfBundle.UpdateServerDatabase(serverFiles, "Archive", Example()).OrderBy(p => p.Key)), "targeted update round trips through sealed files");
            foreach (var path in Directory.GetFiles(output, "*.md", SearchOption.AllDirectories))
                File.WriteAllText(path, File.ReadAllText(path).Replace("\n", "\r\n", StringComparison.Ordinal));
            using (var writer = new BundleWriter(output)) writer.Write(files);
            Check(first.OrderBy(x => x.Key).SequenceEqual(ReadBundle(output).OrderBy(x => x.Key)), "byte-stable refresh after Git CRLF normalization");
            var reduced = new Dictionary<string, string> { ["index.md"] = "# Empty index\n" };
            using (var writer = new BundleWriter(output)) writer.Write(reduced);
            Check(Directory.GetFiles(output, "*", SearchOption.AllDirectories).Length == 1, "stale generated files removed");
            File.AppendAllText(Path.Combine(output, "index.md"), "Handwritten note\n");
            Throws(() => { using var writer = new BundleWriter(output); }, "modified output protected");
            Check(File.ReadAllText(Path.Combine(output, "index.md")).Contains("Handwritten note", StringComparison.Ordinal), "modified output retained");
            var foreign = Path.Combine(temp, "foreign");
            Directory.CreateDirectory(foreign);
            File.WriteAllText(Path.Combine(foreign, "manual.md"), "Human notes");
            Throws(() => { using var writer = new BundleWriter(foreign); }, "foreign output protected");
            using (var writer = new BundleWriter(Path.Combine(temp, "traversal")))
                Throws(() => writer.Write(new Dictionary<string, string> { ["../escape.md"] = "bad" }), "writer path traversal refused");
            Check(!File.Exists(Path.Combine(temp, "escape.md")), "no traversal side effect");

            var stdout = new StringWriter();
            var stderr = new StringWriter();
            Check(await Cli.RunAsync(["--help"], stdout, stderr) == 0 && stdout.ToString().Contains("v0.2", StringComparison.Ordinal), "offline CLI help");
            stdout.GetStringBuilder().Clear();
            Check(await Cli.RunAsync(["Password=PRIVATE_ARGUMENT"], stdout, stderr) == 2 && !stderr.ToString().Contains("PRIVATE_ARGUMENT", StringComparison.Ordinal), "raw argument redaction");
            var missingOutput = Path.Combine(temp, "missing-bundle");
            Check(await Cli.RunAsync(["--update-database", "PRIVATE_DATABASE", "--output", missingOutput], stdout, stderr) == 2
                && !Directory.Exists(missingOutput) && !stderr.ToString().Contains("PRIVATE_DATABASE", StringComparison.Ordinal), "missing update bundle refused before reading secrets without leaking name");
        }
        finally { DeleteTemp(temp); }

        var sql = Regex.Replace(CatalogReader.Query, @"--[^\r\n]*", "");
        Check(!Regex.IsMatch(sql, @"\b(INSERT|UPDATE|DELETE|MERGE|EXEC(?:UTE)?|CREATE|ALTER|DROP|TRUNCATE|SELECT\s+\*)\b", RegexOptions.IgnoreCase), "catalog query mutation allowlist");
        var withoutLiterals = Regex.Replace(sql, "'(?:''|[^'])*'", "''");
        Check(!Regex.IsMatch(withoutLiterals, @"\b(definition|filter_definition|default_value|last_value|seed_value|increment_value|extended_properties|sql_modules|partitions)\b", RegexOptions.IgnoreCase), "sensitive catalog fields excluded");
        Check(Regex.Matches(sql, @"\b(?:FROM|JOIN)\s+([^\s]+)", RegexOptions.IgnoreCase).All(m => m.Groups[1].Value.StartsWith("sys.", StringComparison.Ordinal)), "catalog-only sources");
        Check(!Regex.IsMatch(ServerReader.DiscoveryQuery, @"\b(INSERT|UPDATE|DELETE|MERGE|EXEC(?:UTE)?|CREATE|ALTER|DROP|TRUNCATE|USE|SELECT\s+\*)\b", RegexOptions.IgnoreCase), "server discovery cannot mutate or execute stored code");
        Check(Regex.Matches(ServerReader.DiscoveryQuery, @"\b(?:FROM|JOIN)\s+([^\s]+)", RegexOptions.IgnoreCase).All(m => m.Groups[1].Value == "sys.databases"), "server discovery uses only database catalog");
    }

    private static List<DatabaseScan> ServerExample() =>
    [
        new("Application", false, Example(), "Exported."),
        new("Reporting", false, Example(), "Exported."),
        new("Archive", false, null, "Skipped: database is not online.")
    ];

    private static SchemaModel Example()
    {
        var model = new SchemaModel();
        var customers = new Relation(1, "dbo", "Customers", "Table", "NON_TEMPORAL_TABLE");
        customers.Columns.Add(new(1, "TenantId", "int", false, false, false, false, false, false, "NOT_APPLICABLE", null));
        customers.Columns.Add(new(2, "Id", "int", false, false, false, false, false, false, "NOT_APPLICABLE", null));
        var primary = new DbIndex(1, "PK_Customers", "CLUSTERED", true, true, false, false, false);
        primary.Columns.Add(new(1, "TenantId", 1, false, false, 0));
        primary.Columns.Add(new(2, "Id", 2, false, false, 0));
        customers.Indexes.Add(primary);
        var orders = new Relation(2, "sales", "Orders", "Table", "NON_TEMPORAL_TABLE");
        orders.Columns.Add(new(1, "TenantId", "int", false, false, false, false, false, false, "NOT_APPLICABLE", null));
        orders.Columns.Add(new(2, "CustomerId", "int", false, false, false, false, false, false, "NOT_APPLICABLE", null));
        orders.Columns.Add(new(3, "Amount", "decimal(18,2)", false, false, false, false, false, false, "NOT_APPLICABLE", null));
        var foreign = new ForeignKey(3, "FK_Orders_Customers", 1, "dbo", "Customers", "NO_ACTION", "NO_ACTION", false, false);
        foreign.Columns.Add(new(1, "TenantId", "TenantId"));
        foreign.Columns.Add(new(2, "CustomerId", "Id"));
        orders.ForeignKeys.Add(foreign);
        var index = new DbIndex(2, "IX_Orders_Customer", "NONCLUSTERED", false, false, false, false, false);
        index.Columns.Add(new(1, "TenantId", 1, false, false, 0));
        index.Columns.Add(new(2, "CustomerId", 2, true, false, 0));
        index.Columns.Add(new(3, "Amount", 0, false, true, 0));
        orders.Indexes.Add(index);
        model.Relations.AddRange([customers, orders]);
        return model;
    }

    private static void ValidateBundle(Dictionary<string, string> files)
    {
        Check(files["index.md"].StartsWith("---\nokf_version: \"0.2\"\n---\n", StringComparison.Ordinal), "OKF root version");
        foreach (var (path, text) in files)
        {
            Check(!text.Contains('\r'), "portable LF output");
            if (path == "index.md") { }
            else if (Path.GetFileName(path) == "index.md") Check(!text.StartsWith("---", StringComparison.Ordinal), "reserved index has no frontmatter");
            else Check(text.StartsWith("---\ntype: \"", StringComparison.Ordinal) && text.Contains("\n---\n", StringComparison.Ordinal), "OKF concept type and frontmatter");
            foreach (Match link in Regex.Matches(text, @"\]\(([^)]+\.md)\)"))
            {
                var target = Path.GetFullPath(Path.Combine("virtual-bundle", Path.GetDirectoryName(path)!, link.Groups[1].Value));
                var relative = Path.GetRelativePath(Path.GetFullPath("virtual-bundle"), target).Replace('\\', '/');
                Check(files.ContainsKey(relative), "bundle links resolve");
            }
        }
    }

    private static Dictionary<string, string> ReadBundle(string directory) => Directory.GetFiles(directory, "*.md", SearchOption.AllDirectories)
        .ToDictionary(p => Path.GetRelativePath(directory, p), File.ReadAllText, StringComparer.Ordinal);
    private static string NewTemp()
    {
        var path = Path.Combine(Path.GetTempPath(), "dbmapper-selftest-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
    private static void DeleteTemp(string path)
    {
        var resolved = Path.GetFullPath(path);
        if (!resolved.StartsWith(Path.GetFullPath(Path.GetTempPath()), StringComparison.OrdinalIgnoreCase) || !Path.GetFileName(resolved).StartsWith("dbmapper-selftest-", StringComparison.Ordinal))
            throw new CheckFailure("FAIL: unsafe test cleanup path");
        Directory.Delete(resolved, recursive: true);
    }
    private static void Check(bool condition, string label)
    {
        if (!condition) throw new CheckFailure("FAIL: " + label);
        checks++;
    }
    private static void Throws(Action action, string label)
    {
        try { action(); }
        catch (UsageException) { Check(true, label); return; }
        throw new CheckFailure("FAIL: " + label);
    }
    private sealed class CheckFailure(string message) : Exception(message);
}
