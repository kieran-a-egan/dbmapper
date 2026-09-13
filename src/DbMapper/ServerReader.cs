using Microsoft.Data.SqlClient;

namespace DbMapper;

internal sealed record DatabaseScan(string Name, bool IsSystem, SchemaModel? Schema, string Status);

internal static class ServerReader
{
    internal const string DiscoveryQuery = """
        SET NOCOUNT ON;
        SET LOCK_TIMEOUT 5000;
        SET DEADLOCK_PRIORITY LOW;

        SELECT CAST(SERVERPROPERTY('EngineEdition') AS int),
               CAST(COALESCE(HAS_PERMS_BY_NAME(NULL, NULL, 'VIEW ANY DATABASE'), 0) AS int);

        SELECT name, state, COALESCE(HAS_DBACCESS(name), 0),
               CAST(CASE WHEN database_id <= 4 THEN 1 ELSE 0 END AS bit)
        FROM sys.databases
        ORDER BY name;
        """;

    public static async Task<List<DatabaseScan>> ReadAsync(string connectionString, int timeout, CancellationToken cancellationToken,
        IReadOnlyList<string>? databases = null)
    {
        // Discover once, then use one fresh connection at a time. Names go through
        // SqlConnectionStringBuilder, never SQL interpolation or USE statements.
        var targets = new List<(string Name, bool Online, bool Access, bool System)>();
        await using (var connection = new SqlConnection(CatalogReader.PrepareConnection(connectionString, timeout, "master")))
        {
            await connection.OpenAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = DiscoveryQuery;
            command.CommandTimeout = timeout;
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
                throw new UsageException("SQL Server did not return discovery metadata. The existing bundle was preserved.");
            if (reader.GetInt32(0) is not (1 or 2 or 3 or 4 or 8))
                throw new UsageException("Database discovery supports SQL Server and Azure SQL Managed Instance. Use single-database mode for Azure SQL Database and other endpoints.");
            if (reader.GetInt32(1) != 1)
                throw new UsageException("Server VIEW ANY DATABASE permission is required for database discovery. The existing bundle was preserved.");
            if (!await reader.NextResultAsync(cancellationToken))
                throw new UsageException("SQL Server returned an incomplete database listing. The existing bundle was preserved.");
            while (await reader.ReadAsync(cancellationToken))
                targets.Add((reader.GetString(0), reader.GetByte(1) == 0, reader.GetInt32(2) == 1, reader.GetBoolean(3)));
        }
        if (targets.Count == 0)
            throw new UsageException("No databases were visible to the selected login. The existing bundle was preserved.");
        if (databases is not null)
        {
            var selected = SelectNames(targets.Select(t => t.Name), databases);
            targets = targets.Where(t => selected.Contains(t.Name)).ToList();
        }

        var results = new List<DatabaseScan>();
        foreach (var target in targets.OrderBy(t => t.Name, StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!target.Online)
            {
                results.Add(new(target.Name, target.System, null, "Skipped: database is not online."));
                continue;
            }
            if (!target.Access)
            {
                results.Add(new(target.Name, target.System, null, "Skipped: database access is unavailable to this login."));
                continue;
            }
            try
            {
                var schema = await CatalogReader.ReadAsync(connectionString, timeout, cancellationToken, target.Name);
                results.Add(new(target.Name, target.System, schema, "Exported."));
            }
            catch (UsageException)
            {
                results.Add(new(target.Name, target.System, null, "Skipped: metadata precondition failed; check database VIEW DEFINITION permission."));
            }
            catch (SqlException)
            {
                cancellationToken.ThrowIfCancellationRequested();
                results.Add(new(target.Name, target.System, null, "Failed: connection or catalog query failed; check access, availability, TLS and timeout."));
            }
        }
        return results;
    }

    internal static HashSet<string> SelectNames(IEnumerable<string> available, IReadOnlyList<string> requested)
    {
        var selected = requested.ToHashSet(StringComparer.Ordinal);
        if (selected.Count == 0 || selected.Count != requested.Count || !selected.IsSubsetOf(available))
            throw new UsageException("A database selection is empty, repeated, or not visible. Use exact database names, including case. The existing bundle was preserved.");
        return selected;
    }
}
