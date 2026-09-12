using System.Text;

namespace DbMapper;

internal static partial class OkfBundle
{
    public static Dictionary<string, string> RenderServer(IReadOnlyList<DatabaseScan> databases)
    {
        var files = Render(new SchemaModel());
        files.Remove("database.md");
        files["CLAUDE.md"] = files["CLAUDE.md"].Replace("[database.md](database.md)", "[server.md](server.md)", StringComparison.Ordinal) + """

            # Server-wide context

            Choose a database from the root index before selecting schemas. Database names are exported as structural identifiers; each database has its own directory. The same schema/table name can exist in multiple databases with different structure. Never merge their objects or infer cross-database relationships. Follow the selected database's own CLAUDE.md and links for its local context.

            Read server.md for scan coverage. A skipped/failed database has a status page and no current schema files. An incomplete scan is not evidence that its missing objects were dropped. System databases and snapshots are included if visible; their exported objects follow the same catalog filters as application databases.
            """ + "\n";
        var exported = databases.Count(d => d.Schema is not null);
        var status = exported == databases.Count ? "All discovered databases exported." : "Incomplete: some discovered databases were not exported.";
        var index = new StringBuilder(IndexHeader + "# Server database context\n\n");
        index.AppendLine("* [Scan report](server.md) - Database coverage, failures and limitations.");
        index.AppendLine("* [Claude Code guide](CLAUDE.md) - Choose a database before reading its schema.");
        index.AppendLine("\n# Databases\n");
        var directoryIndex = new StringBuilder("# Databases\n\n");
        var report = new StringBuilder(Header("SQL Server Scan", "Server database scan", "Per-database export status and scope limitations.") + $"""
            # Scan result

            {status} Exported {exported} of {databases.Count} discovered databases.

            # Scope

            Discovery reads sys.databases in master using the same server, authentication and TLS settings as the selected user secret. Each visible online and accessible database is inspected sequentially with the existing fixed metadata-only queries. User databases, system databases and visible snapshots are included. No database is brought online or altered, and no application data or stored code is read/executed. Database names are intentionally included as structural identifiers; server addresses, credentials and raw error messages remain excluded.

            # Coverage limitations

            Only databases visible to this login can be listed. VIEW ANY DATABASE is required, but SQL Server can still hide offline databases from low-privilege logins. Discovery and individual scans are not a server-wide consistent snapshot: databases may be created, dropped, renamed or become unavailable during the run. Exit 0 means every discovered database was exported, not that hidden databases cannot exist.

            Each successful database still requires VIEW DEFINITION and follows its own database.md coverage limitations. Microsoft-shipped objects are filtered out, including inside system databases. No cross-database dependencies or relationships are inferred. Review database names along with all other identifiers before committing.

            # Incomplete exports

            Skipped or failed databases receive a status page with no schema files. An incomplete run writes the accessible results and returns exit code 4. Refresh replaces the previous generated snapshot, including removal of older pages for databases that could not be scanned this time. Do not interpret those missing pages as schema deletions. Discovery failure, cancellation and unexpected application failure leave the existing bundle unchanged.

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
                foreach (var (path, content) in Render(database.Schema, database.Name)) Add(directory + "/" + path, content);
                files[directory + "/index.md"] += "\n[All databases](../../index.md) · [Server scan report](../../server.md)\n";
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
}
