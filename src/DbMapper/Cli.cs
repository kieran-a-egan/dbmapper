using Microsoft.Data.SqlClient;

namespace DbMapper;

internal sealed class UsageException(string message) : Exception(message);

internal sealed record Options(string? Project, string? SecretsId, string? SecretKey, string Output, int Timeout, bool AllDatabases)
{
    public static Options Parse(string[] args)
    {
        string? project = null, id = null, key = null;
        var output = "database-context";
        var timeout = 30;
        var allDatabases = false;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < args.Length; i++)
        {
            var option = args[i];
            if (option == "--all-databases")
            {
                if (!seen.Add(option)) throw new UsageException("Each option may be supplied once.");
                allDatabases = true;
                continue;
            }
            if (option is not ("--project" or "--user-secrets-id" or "--connection" or "--secret-key" or "--output" or "--timeout"))
                throw new UsageException("Unknown argument. Run dbmapper --help; connection strings are never accepted as arguments.");
            if (!seen.Add(option) || ++i == args.Length || string.IsNullOrWhiteSpace(args[i]) || args[i].StartsWith("--", StringComparison.Ordinal))
                throw new UsageException("Each option requires one non-empty value and may be supplied once.");
            var value = args[i];
            switch (option)
            {
                case "--project": project = value; break;
                case "--user-secrets-id": id = value; break;
                case "--connection": key = "ConnectionStrings:" + value; break;
                case "--secret-key": key = value; break;
                case "--output": output = value; break;
                case "--timeout":
                    if (!int.TryParse(value, out timeout) || timeout is < 1 or > 300)
                        throw new UsageException("--timeout must be between 1 and 300 seconds.");
                    break;
            }
        }
        if (seen.Contains("--connection") && seen.Contains("--secret-key"))
            throw new UsageException("Choose --connection or --secret-key, not both.");
        return new(project, id, key, Path.GetFullPath(output), timeout, allDatabases);
    }
}

internal static class Cli
{
    public const string Version = "0.2.0";
    public const string Help = """
        dbmapper - SQL Server metadata to Google Open Knowledge Format v0.2

        Run inside a .NET project with an existing UserSecretsId and connection secret:
          dbmapper
          dbmapper --connection DefaultConnection
          dbmapper --project src/Web/Web.csproj --secret-key Database:ConnectionString
          dbmapper --connection DefaultConnection --all-databases

        --project <path>         .csproj or directory (default: current directory)
        --connection <name>      Select ConnectionStrings:<name> from user secrets
        --secret-key <key>       Select a full user-secret key instead
        --user-secrets-id <id>   Explicit store ID for inherited/conditional project settings
        --output <directory>    Bundle directory (default: ./database-context)
        --timeout <seconds>     Connect/query timeout, 1-300 (default: 30)
        --all-databases         Scan visible user/system databases; export database names
        --help                  Show help without reading secrets or connecting
        --version               Show version

        With no key option, exactly one ConnectionStrings entry must exist.
        By default only the database explicitly named in that secret is inspected.
        --all-databases discovers via master, using the same server and credentials.
        Unscannable databases are reported in server.md; incomplete exports exit 4.
        Reads fixed system catalog queries; never reads table/view rows or definitions.
        Requires database VIEW DEFINITION permission. Review identifiers before commit.
        Refresh replaces only intact dbmapper output; edited/foreign files are refused.
        """;

    public static async Task<int> RunAsync(string[] args, TextWriter output, TextWriter error, CancellationToken cancellationToken = default)
    {
        try
        {
            if (args is ["--help"] or ["-h"])
            {
                await output.WriteLineAsync(Help);
                return 0;
            }
            if (args is ["--version"])
            {
                await output.WriteLineAsync("dbmapper " + Version);
                return 0;
            }
            var options = Options.Parse(args);
            using var bundle = new BundleWriter(options.Output);
            var connectionString = ProjectSecrets.Read(options);
            if (options.AllDatabases)
            {
                var databases = await ServerReader.ReadAsync(connectionString, options.Timeout, cancellationToken);
                var serverFiles = OkfBundle.RenderServer(databases);
                cancellationToken.ThrowIfCancellationRequested();
                bundle.Write(serverFiles);
                var exported = databases.Count(d => d.Schema is not null);
                await output.WriteLineAsync($"Exported {exported} of {databases.Count} discovered databases to {serverFiles.Count} OKF v0.2 Markdown files.");
                await output.WriteLineAsync("Start at index.md and CLAUDE.md. Database names are included; review identifiers before committing.");
                if (exported != databases.Count)
                {
                    await error.WriteLineAsync("The server export is incomplete. See server.md for each database's status; unavailable schema files are omitted from this snapshot.");
                    return 4;
                }
                return 0;
            }
            var model = await CatalogReader.ReadAsync(connectionString, options.Timeout, cancellationToken);
            var files = OkfBundle.Render(model);
            cancellationToken.ThrowIfCancellationRequested();
            bundle.Write(files);
            await output.WriteLineAsync($"Exported {model.Relations.Count} tables/views and {model.Routines.Count} routines to {files.Count} OKF v0.2 Markdown files.");
            await output.WriteLineAsync("Start at index.md and CLAUDE.md in the output directory. Review schema identifiers before committing.");
            return 0;
        }
        catch (UsageException e)
        {
            await error.WriteLineAsync(e.Message); // Only application-authored messages, never input or driver exception text.
            return 2;
        }
        catch (OperationCanceledException)
        {
            await error.WriteLineAsync("Cancelled; the existing bundle was not replaced.");
            return 130;
        }
        catch (SqlException e)
        {
            var hint = e.Number switch
            {
                -2 => "A database operation timed out. Check availability or increase --timeout.",
                18456 => "Database authentication failed. Check the selected user secret and login permissions.",
                4060 => "The selected database could not be opened. Check database access.",
                229 or 916 => "Metadata access was denied. Ask the database administrator to check permissions.",
                _ => "The SQL Server connection or catalog query failed. Check availability, TLS and metadata permissions."
            };
            await error.WriteLineAsync(hint + " Server details are withheld to protect secrets.");
            return 3;
        }
        catch (Exception)
        {
            await error.WriteLineAsync("The export failed. Check the project, user-secret configuration and output permissions. Error details are withheld to protect secrets.");
            return 1;
        }
    }
}
