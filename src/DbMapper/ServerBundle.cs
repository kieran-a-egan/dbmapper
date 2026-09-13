using System.Text;

namespace DbMapper;

internal static partial class OkfBundle
{
    public static Dictionary<string, string> RenderServer(IReadOnlyList<DatabaseScan> databases, bool selectedOnly = false)
    {
        var files = Render(new SchemaModel());
        files.Remove("database.md");
        files["CLAUDE.md"] = files["CLAUDE.md"].Replace("[database.md](database.md)", "[server.md](server.md)", StringComparison.Ordinal) + """

            # Server-wide context

            Choose a database from the root index before selecting schemas. Database names are exported as structural identifiers; each database has its own directory. The same schema/table name can exist in multiple databases with different structure. Never merge their objects or infer cross-database relationships. Follow the selected database's own CLAUDE.md and links for its local context.

            Read server.md for scan coverage. A skipped/failed database has a status page and no current schema files. An incomplete scan is not evidence that its missing objects were dropped. Full scans include visible system databases and snapshots; selected scans include only the requested names. Their exported objects follow the same catalog filters as application databases.

            After completing schema work, refresh the affected database with `dbmapper --update-database <exact-name> --output <bundle-directory>` from the application project, then review the generated diff before committing. Other databases retain their previous scan results; a targeted update does not verify their freshness.
            """ + "\n";
        var exported = databases.Count(d => d.Schema is not null);
        var index = new StringBuilder(IndexHeader + "# Server database context\n\n");
        index.AppendLine("* [Scan report](server.md) - Database coverage, failures and limitations.");
        index.AppendLine("* [Claude Code guide](CLAUDE.md) - Choose a database before reading its schema.");
        index.AppendLine("\n# Databases\n");
        var directoryIndex = new StringBuilder("# Databases\n\n");
        var report = new StringBuilder(Header("SQL Server Scan", "Server database scan", "Per-database export status and scope limitations.") + $"""
            # Scan result

            {ScanResult(exported, databases.Count, selectedOnly)}

            # Scope

            Discovery reads sys.databases in master using the same server, authentication and TLS settings as the selected user secret. Online and accessible databases within the scan scope are inspected sequentially with the existing fixed metadata-only queries. The scope can include user databases, system databases and visible snapshots. No database is brought online or altered, and no application data or stored code is read/executed. Database names are intentionally included as structural identifiers; server addresses, credentials and raw error messages remain excluded.

            {(selectedOnly ? "This bundle contains only the explicitly selected databases. Other discovered databases were not scanned and are outside its scope." : "This bundle includes every database returned by discovery.")}

            # Coverage limitations

            Only databases visible to this login can be listed. VIEW ANY DATABASE is required for discovery, but SQL Server can still hide offline databases from low-privilege logins. Discovery and individual scans are not a server-wide consistent snapshot: databases may be created, dropped, renamed or become unavailable during the run. Exit 0 for a full or selected scan means every database in its scope was exported, not that hidden databases cannot exist.

            Each successful database still requires VIEW DEFINITION and follows its own database.md coverage limitations. Microsoft-shipped objects are filtered out, including inside system databases. No cross-database dependencies or relationships are inferred. Review database names along with all other identifiers before committing.

            # Incomplete exports

            Skipped or failed databases receive a status page with no schema files. An incomplete run writes the accessible results and returns exit code 4. Refresh replaces the previous generated snapshot, including removal of older pages for databases that could not be scanned this time. Do not interpret those missing pages as schema deletions. Discovery failure, cancellation and unexpected application failure leave the existing bundle unchanged.

            A targeted update refreshes only the named database and its status in this report. Other databases retain their previous results and may be stale. A failed targeted update preserves the entire previous bundle. Exit 0 for a targeted update means that database was refreshed, even if other listed databases still have incomplete results. Review the diff after schema work and before committing.

            # Databases

            | Database | Kind | Status |
            | --- | --- | --- |
            """ + "\n");

        foreach (var database in databases.OrderBy(d => d.Name, StringComparer.Ordinal))
        {
            var directory = "databases/db-" + Slug(database.Name);
            var label = Text(database.Name);
            index.AppendLine($"* [{label}]({directory}/index.md) - {Text(database.Status)}");
            directoryIndex.AppendLine($"* [{label}]({directory["databases/".Length..]}/index.md) - {Text(database.Status)}");
            report.AppendLine($"| [{label}]({directory}/index.md) | {(database.IsSystem ? "System" : "User / snapshot")} | {Text(database.Status)} |");
            if (database.Schema is not null)
            {
                foreach (var (path, content) in RenderServerDatabase(database.Schema, database.Name)) Add(directory + "/" + path, content);
            }
            else
            {
                Add(directory + "/index.md", $"# {label}\n\n* [Scan status](database.md) - {Text(database.Status)}\n\n[All databases](../../index.md)\n");
                Add(directory + "/database.md", Header("SQL Server Database", database.Name, "No schema was exported for this database.") +
                    $"# Scan status\n\n{Text(database.Status)}\n\nNo schema files are included for this database in the current snapshot. This does not indicate an empty database.\n\n[Server scan report](../../server.md) · [Database index](index.md)\n");
            }
        }
        files["index.md"] = index.ToString();
        Add("databases/index.md", directoryIndex.ToString());
        Add("server.md", report.ToString());
        return files.ToDictionary(p => p.Key, p => p.Value.Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd() + "\n", StringComparer.OrdinalIgnoreCase);

        void Add(string path, string content)
        {
            if (!files.TryAdd(path, content))
                throw new UsageException("Two databases produced the same output path. The export was refused.");
        }
    }

    internal static void RequireServerDatabase(IReadOnlyDictionary<string, string> files, string database)
    {
        var directory = "databases/db-" + Slug(database);
        if (!new[] { "index.md", "server.md", "CLAUDE.md", "databases/index.md", directory + "/index.md", directory + "/database.md" }.All(files.ContainsKey)
            || !files["server.md"].Split('\n').Any(line => line.StartsWith($"| [{Text(database)}]({directory}/index.md) | ", StringComparison.Ordinal)))
            throw new UsageException("--update-database requires a database already listed in an existing server bundle. Use its exact name, including case. Create or replace the bundle with --all-databases or --database first.");
    }

    public static Dictionary<string, string> UpdateServerDatabase(IReadOnlyDictionary<string, string> existing, string database, SchemaModel schema)
    {
        RequireServerDatabase(existing, database);
        var directory = "databases/db-" + Slug(database);
        var label = Text(database);
        var files = existing.Where(p => !p.Key.StartsWith(directory + "/", StringComparison.Ordinal))
            .ToDictionary(p => p.Key, p => p.Value, StringComparer.OrdinalIgnoreCase);
        foreach (var (path, content) in RenderServerDatabase(schema, database)) files.Add(directory + "/" + path, content);

        // Update only the generated navigation/status lines. The report already holds
        // the kind and outcome for every database, including bundles from version 0.2.0.
        ReplaceLine("index.md", $"* [{label}]({directory}/index.md) - ", _ => $"* [{label}]({directory}/index.md) - Exported.");
        ReplaceLine("databases/index.md", $"* [{label}]({directory["databases/".Length..]}/index.md) - ",
            _ => $"* [{label}]({directory["databases/".Length..]}/index.md) - Exported.");
        ReplaceLine("server.md", $"| [{label}]({directory}/index.md) | ", line => line[..line.LastIndexOf(" | ", StringComparison.Ordinal)] + " | Exported. |");

        var report = files["server.md"];
        var rows = report.Split('\n').Where(line => line.StartsWith("| [", StringComparison.Ordinal)).ToList();
        const string heading = "# Scan result\n\n";
        var start = report.IndexOf(heading, StringComparison.Ordinal);
        var end = start < 0 ? -1 : report.IndexOf("\n\n# Scope\n", start + heading.Length, StringComparison.Ordinal);
        if (start < 0 || end < 0) throw Unsupported();
        start += heading.Length;
        var selectedOnly = report[start..end].EndsWith("selected databases.", StringComparison.Ordinal);
        files["server.md"] = report[..start] + ScanResult(rows.Count(row => row.EndsWith(" | Exported. |", StringComparison.Ordinal)), rows.Count, selectedOnly) + report[end..];
        return files;

        void ReplaceLine(string path, string prefix, Func<string, string> replace)
        {
            var lines = files[path].Split('\n');
            var matches = Enumerable.Range(0, lines.Length).Where(i => lines[i].StartsWith(prefix, StringComparison.Ordinal)).ToList();
            if (matches.Count != 1) throw Unsupported();
            lines[matches[0]] = replace(lines[matches[0]]);
            files[path] = string.Join('\n', lines);
        }

        static UsageException Unsupported() => new("The existing server report is incomplete or uses an unsupported format. Refresh with --all-databases or --database first. The existing bundle was preserved.");
    }

    private static string ScanResult(int exported, int count, bool selectedOnly)
    {
        var scope = selectedOnly ? "selected" : "discovered";
        var status = exported == count ? $"All {scope} databases exported." : $"Incomplete: some {scope} databases were not exported.";
        return $"{status} Exported {exported} of {count} {scope} databases.";
    }

    private static Dictionary<string, string> RenderServerDatabase(SchemaModel schema, string database)
    {
        var files = Render(schema, database);
        files["index.md"] += "\n[All databases](../../index.md) · [Server scan report](../../server.md)\n";
        return files;
    }
}
