using Microsoft.Data.SqlClient;

namespace DbMapper;

internal static class CatalogReader
{
    internal static string PrepareConnection(string value, int timeout, string? database = null)
    {
        SqlConnectionStringBuilder builder;
        try { builder = new(value); }
        catch (ArgumentException) { throw new UsageException("The selected secret is not a supported SQL Server connection string. Its value is withheld."); }
        if (database is not null) builder.InitialCatalog = database;
        if (string.IsNullOrWhiteSpace(builder.DataSource) || string.IsNullOrWhiteSpace(builder.InitialCatalog))
            throw new UsageException("The connection secret must explicitly specify both Server and Database (Initial Catalog).");
        if (!string.IsNullOrEmpty(builder.AttachDBFilename) || builder.UserInstance)
            throw new UsageException("AttachDBFilename and User Instance connections are refused because opening them can change database state.");
        builder.ApplicationName = "dbmapper";
        builder.ApplicationIntent = ApplicationIntent.ReadOnly; // Routing hint; fixed queries and SQL permissions enforce the actual boundary.
        builder.PersistSecurityInfo = false;
        builder.Pooling = false;
        builder.Enlist = false;
        builder.MultipleActiveResultSets = false;
        builder.ConnectRetryCount = 0;
        builder.ConnectTimeout = timeout;
        return builder.ConnectionString;
    }

    internal static string Query
    {
        get
        {
            using var stream = typeof(CatalogReader).Assembly.GetManifestResourceStream("DbMapper.Catalog.sql")!;
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }
    }

    public static async Task<SchemaModel> ReadAsync(string connectionString, int timeout, CancellationToken cancellationToken, string? database = null)
    {
        await using var connection = new SqlConnection(PrepareConnection(connectionString, timeout, database));
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = Query;
        command.CommandTimeout = timeout;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken) || reader.GetInt32(0) != 1)
            throw new UsageException("Database VIEW DEFINITION permission is required. The tool refuses a silently partial metadata export.");

        var model = new SchemaModel();
        var relations = new Dictionary<int, Relation>();
        var indexes = new Dictionary<(int, int), DbIndex>();
        var foreignKeys = new Dictionary<int, ForeignKey>();
        var routines = new Dictionary<int, Routine>();
        int Number(int ordinal) => Convert.ToInt32(reader.GetValue(ordinal), System.Globalization.CultureInfo.InvariantCulture);
        string Text(int ordinal) => reader.GetString(ordinal);
        bool Flag(int ordinal) => reader.GetBoolean(ordinal);

        async Task Next()
        {
            if (!await reader.NextResultAsync(cancellationToken))
                throw new UsageException("SQL Server returned an incomplete catalog result. The existing bundle has been preserved.");
        }

        await Next();
        while (await reader.ReadAsync(cancellationToken))
        {
            var relation = new Relation(Number(0), Text(1), Text(2), Text(3), Text(4));
            relations.Add(relation.Id, relation);
            model.Relations.Add(relation);
        }
        await Next();
        while (await reader.ReadAsync(cancellationToken))
            relations[Number(0)].Columns.Add(new(Number(1), Text(2), Text(3), Flag(4), Flag(5), Flag(6), Flag(7), Flag(8), Flag(9), Text(10), reader.IsDBNull(11) ? null : Text(11)));
        await Next();
        while (await reader.ReadAsync(cancellationToken))
        {
            var index = new DbIndex(Number(1), Text(2), Text(3), Flag(4), Flag(5), Flag(6), Flag(7), Flag(8));
            relations[Number(0)].Indexes.Add(index);
            indexes.Add((Number(0), index.Id), index);
        }
        await Next();
        while (await reader.ReadAsync(cancellationToken))
            indexes[(Number(0), Number(1))].Columns.Add(new(Number(2), Text(3), Number(4), Flag(5), Flag(6), Number(7)));
        await Next();
        while (await reader.ReadAsync(cancellationToken))
        {
            var id = Number(1);
            if (!foreignKeys.TryGetValue(id, out var key))
            {
                key = new(id, Text(2), Number(3), Text(4), Text(5), Text(6), Text(7), Flag(8), Flag(9));
                relations[Number(0)].ForeignKeys.Add(key);
                foreignKeys.Add(id, key);
            }
            key.Columns.Add(new(Number(10), Text(11), Text(12)));
        }
        await Next();
        while (await reader.ReadAsync(cancellationToken))
            relations[Number(0)].Checks.Add(new(Text(1), Flag(2), Flag(3)));
        await Next();
        while (await reader.ReadAsync(cancellationToken))
            relations[Number(0)].Triggers.Add(new(Text(1), Flag(2), Flag(3)));
        await Next();
        while (await reader.ReadAsync(cancellationToken))
        {
            var routine = new Routine(Number(0), Text(1), Text(2), Text(3));
            routines.Add(routine.Id, routine);
            model.Routines.Add(routine);
        }
        await Next();
        while (await reader.ReadAsync(cancellationToken))
            routines[Number(0)].Parameters.Add(new(Number(1), Text(2), Text(3), Flag(4), Flag(5)));
        if (await reader.NextResultAsync(cancellationToken))
            throw new UsageException("Unexpected catalog results were returned. The export was refused.");
        return model;
    }
}
