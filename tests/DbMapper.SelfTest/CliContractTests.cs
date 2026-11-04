using DbMapper;
using Microsoft.Extensions.Configuration;

internal static partial class Program
{
    // Characterises the current CLI contract: help/version, argument validation, exit codes and
    // secret-redaction. Nothing here reads the user-secrets store or opens a SQL connection.
    private static async Task CliContractTests()
    {
        const string planted = "PLANTED_A01_PASSWORD";

        var help = await RunCli("--help");
        Check(help.Code == 0 && help.Err.Length == 0, "help exits 0 with empty stderr");
        Check(help.Out.Contains("--sql <file-or-folder>", StringComparison.Ordinal) && help.Out.Contains("--update-database <name>", StringComparison.Ordinal)
            && help.Out.Contains("--all-databases", StringComparison.Ordinal) && help.Out.Contains("--secret-key <key>", StringComparison.Ordinal), "help lists every supported option");
        var shortHelp = await RunCli("-h");
        Check(shortHelp.Code == 0 && shortHelp.Out == help.Out, "-h matches --help");
        var version = await RunCli("--version");
        Check(version.Code == 0 && version.Out.Trim() == "dbmapper 0.3.0" && version.Err.Length == 0, "version exits 0 with pinned version line");
        Check((await RunCli("--help", "--version")).Code == 2, "help and version must be the only argument");
        Check((await RunCli("--version", "--help")).Code == 2, "version then help is refused");
        Check((await RunCli("--help", "--sql", "x")).Code == 2, "help combined with options is refused");

        var unknown = await RunCli("--frobnicate");
        Check(unknown.Code == 2 && unknown.Err.Contains("Unknown argument", StringComparison.Ordinal), "unknown option exits 2 with application message");
        var raw = await RunCli($"Server=localhost;Password={planted}");
        Check(raw.Code == 2 && !Leaks(raw, planted), "raw connection-string argument refused without echo");

        Check((await RunCli("--project")).Code == 2, "option without value refused");
        Check((await RunCli("--output", "--timeout")).Code == 2, "option value cannot be another option");
        Check((await RunCli("--output", " ")).Code == 2, "whitespace option value refused");
        Check((await RunCli("--timeout", "abc")).Code == 2, "non-numeric timeout refused");
        Check((await RunCli("--timeout", "0")).Code == 2 && (await RunCli("--timeout", "301")).Code == 2, "timeout outside 1-300 refused");
        Check(Options.Parse(["--timeout", "1"]).Timeout == 1 && Options.Parse(["--timeout", "300"]).Timeout == 300, "timeout boundaries 1 and 300 accepted");
        Check(Options.Parse([]).Timeout == 30 && Options.Parse([]).Output == Path.GetFullPath("database-context"), "defaults: 30 second timeout and ./database-context");
        var temp = NewTemp();
        try
        {
            var bundle = Path.Combine(temp, "bundle");
            Check((await RunCli("--output", bundle, "--output", bundle)).Code == 2, "repeated --output refused");
            Check((await RunCli("--output", bundle, "--timeout", "5", "--timeout", "6")).Code == 2, "repeated --timeout refused");
            Check((await RunCli("--connection", "A", "--secret-key", "B")).Code == 2, "connection and secret-key together refused");
            Check((await RunCli("--connection", "A", "--connection", "A")).Code == 2, "repeated --connection refused");
            Check((await RunCli("--all-databases", "--all-databases")).Code == 2, "repeated --all-databases refused");
            Check((await RunCli("--sql", "x.sql", "--all-databases")).Code == 2, "--sql cannot combine with --all-databases");
            Check((await RunCli("--sql", "x.sql", "--project", "App.csproj")).Code == 2, "--sql cannot combine with --project");
            Check((await RunCli("--sql", "x.sql", "--timeout", "5")).Code == 2, "--sql cannot combine with --timeout");
            Check((await RunCli("--sql", "x.sql", "--connection", "A")).Code == 2, "--sql cannot combine with --connection");
            Check((await RunCli("--all-databases", "--database", "App")).Code == 2, "all-databases and database selection conflict");
            Check((await RunCli("--all-databases", "--update-database", "App")).Code == 2, "all-databases and update conflict");
            Check((await RunCli("--database", "App", "--database", "App")).Code == 2, "duplicate selected database refused by CLI");

            // Secret-selection failures with no store access: ID validation runs before the store is opened.
            Check((await RunCli("--user-secrets-id", ".", "--output", bundle)).Code == 2, "store ID '.' refused as a path");
            Check((await RunCli("--user-secrets-id", "..", "--output", bundle)).Code == 2, "store ID '..' refused as a path");
            var pathId = await RunCli("--user-secrets-id", "a/b", "--output", bundle);
            Check(pathId.Code == 2 && pathId.Err.Contains("store identifier", StringComparison.Ordinal), "store ID containing a separator refused");
            var missingProject = await RunCli("--connection", planted, "--project", Path.Combine(temp, "missing", "App.csproj"), "--output", bundle);
            Check(missingProject.Code == 2 && !Leaks(missingProject, planted), "missing project refused and connection name not echoed");
            var emptyDirectory = Path.Combine(temp, "empty");
            Directory.CreateDirectory(emptyDirectory);
            Check((await RunCli("--project", emptyDirectory, "--output", bundle)).Code == 2, "directory without a .csproj refused");
            var project = Path.Combine(temp, "App.csproj");
            File.WriteAllText(project, "<Project><PropertyGroup></PropertyGroup></Project>");
            var noId = await RunCli("--project", project, "--output", bundle);
            Check(noId.Code == 2 && noId.Err.Contains("UserSecretsId", StringComparison.Ordinal), "project without UserSecretsId refused");
            File.WriteAllText(project, "<Project><PropertyGroup Condition=\"'$(Configuration)'=='Debug'\"><UserSecretsId>synthetic</UserSecretsId></PropertyGroup></Project>");
            Check((await RunCli("--project", project, "--output", bundle)).Code == 2, "conditional UserSecretsId refused");
            Check(!Directory.Exists(bundle), "refused runs create no bundle directory");

            // Output-path refusals happen before any secret is read.
            var file = Path.Combine(temp, "not-a-directory");
            File.WriteAllText(file, "x");
            var fileOutput = await RunCli("--output", file, "--user-secrets-id", "synthetic");
            Check(fileOutput.Code == 2 && fileOutput.Err.Contains("existing file", StringComparison.Ordinal), "output that is an existing file refused");
            Check((await RunCli("--output", Directory.GetCurrentDirectory(), "--user-secrets-id", "synthetic")).Code == 2, "output equal to current directory refused");

            // Unexpected failures use the generic message and never echo input or driver text.
            var sqlFolder = Path.Combine(temp, "1234");
            Directory.CreateDirectory(sqlFolder);
            var invalid = Path.Combine(sqlFolder, "in.sql");
            File.WriteAllBytes(invalid, [0xC3, 0x28]);
            var generic = await RunCli("--sql", invalid, "--output", bundle);
            Check(generic.Code == 1 && generic.Err.StartsWith("The export failed.", StringComparison.Ordinal) && !generic.Err.Contains(temp, StringComparison.Ordinal), "unexpected failure exits 1 with generic message and no path");

            // Supported offline success path: exit 0, one-line confirmation, nothing on stderr.
            File.WriteAllText(invalid, "CREATE TABLE dbo.Customers (Id int NOT NULL);");
            var success = await RunCli("--sql", sqlFolder, "--output", bundle);
            Check(success.Code == 0 && success.Err.Length == 0 && success.Out.Contains("Updated the OKF v0.2 bundle from the local SQL file.", StringComparison.Ordinal), "offline SQL folder success exits 0 with empty stderr");
            Check(Directory.Exists(bundle) && Directory.GetFiles(bundle, "index.md", SearchOption.TopDirectoryOnly).Length == 1, "offline success writes bundle index");

            // Secret-store failures reported without the planted value (in-memory configuration only).
            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:First"] = "Password=" + planted,
                ["ConnectionStrings:Second"] = "Password=" + planted,
                ["ConnectionStrings:Blank"] = " ",
                ["Database:Empty"] = ""
            }).Build();
            RefusedWithout(() => ProjectSecrets.Select(config, null), "ambiguous secrets refused without value", planted);
            RefusedWithout(() => ProjectSecrets.Select(config, "ConnectionStrings:Blank"), "blank secret refused without value", planted);
            RefusedWithout(() => ProjectSecrets.Select(config, "Database:Empty"), "empty secret refused without value", planted);
            RefusedWithout(() => ProjectSecrets.Select(config, "Missing:Key"), "missing secret refused without value", planted);
            Check(ProjectSecrets.Select(config, "ConnectionStrings:First") == "Password=" + planted, "explicit key returns the configured value only to the caller");
        }
        finally { DeleteTemp(temp); }
    }

    private static async Task<(int Code, string Out, string Err)> RunCli(params string[] args)
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var code = await Cli.RunAsync(args, stdout, stderr);
        return (code, stdout.ToString(), stderr.ToString());
    }

    private static bool Leaks((int Code, string Out, string Err) result, string secret) =>
        result.Out.Contains(secret, StringComparison.Ordinal) || result.Err.Contains(secret, StringComparison.Ordinal);

    private static void RefusedWithout(Action action, string label, string secret)
    {
        try { action(); }
        catch (UsageException e) { Check(!e.Message.Contains(secret, StringComparison.Ordinal), label); return; }
        throw new CheckFailure("FAIL: " + label);
    }
}
