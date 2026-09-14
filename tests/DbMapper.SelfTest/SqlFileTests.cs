using DbMapper;
using Microsoft.Data.SqlClient;

internal static partial class Program
{
    private static async Task SqlFileTests()
    {
        Throws(() => Options.Parse(["--sql", "ticket", "--connection", "Main"]), "SQL mode cannot read connection secrets");
        Throws(() => Options.Parse(["--sql", "ticket", "--all-databases"]), "SQL mode cannot discover databases");
        Throws(() => Options.Parse(["--sql"]), "SQL source required");
        var temp = NewTemp();
        try
        {
            var ticket = Path.Combine(temp, "12345");
            Directory.CreateDirectory(ticket);
            var input = Path.Combine(ticket, "in.sql");
            var rollback = Path.Combine(ticket, "out.sql");
            var output = Path.Combine(temp, "bundle");
            Directory.CreateDirectory(output);
            File.WriteAllText(input, """
                -- PRIVATE_COMMENT must never be retained
                SET ANSI_NULLS ON;
                SET QUOTED_IDENTIFIER ON;
                BEGIN TRANSACTION;
                CREATE TABLE dbo.Customers (
                    Id int IDENTITY(1, 1) NOT NULL CONSTRAINT PK_Customers PRIMARY KEY,
                    Name nvarchar(100) NULL CONSTRAINT DF_Name DEFAULT N'PRIVATE_DEFAULT',
                    Enabled bit NOT NULL,
                    CONSTRAINT CK_Enabled CHECK (Enabled = 1)
                );
                CREATE TABLE sales.Orders (
                    Id int NOT NULL CONSTRAINT PK_Orders PRIMARY KEY,
                    CustomerId int NOT NULL,
                    Amount decimal(18,2) NOT NULL,
                    CONSTRAINT FK_Orders_Customers FOREIGN KEY (CustomerId) REFERENCES dbo.Customers(Id) ON DELETE CASCADE
                );
                CREATE NONCLUSTERED INDEX IX_Orders_Customer ON sales.Orders(CustomerId DESC) INCLUDE (Amount) WHERE Amount > 123456;
                INSERT dbo.Customers (Name, Enabled) VALUES (N'PRIVATE_ROW', 1);
                COMMIT;
                GO
                CREATE OR ALTER PROCEDURE dbo.ReadCustomer @Id int, @Name nvarchar(100) OUTPUT AS
                    CREATE TABLE dbo.BodyOnly (Id int NOT NULL);
                    SELECT N'PRIVATE_BODY';
                GO
                CREATE VIEW dbo.CustomerNames AS SELECT Id, Name FROM dbo.Customers;
                GO
                CREATE FUNCTION dbo.Answer(@Id int) RETURNS int AS BEGIN RETURN 42; END;
                GO
                CREATE TRIGGER dbo.CustomerTrigger ON dbo.Customers AFTER INSERT AS SELECT N'PRIVATE_TRIGGER';
                """);
            File.WriteAllText(rollback, "DROP VIEW dbo.CustomerNames; DROP TABLE sales.Orders; DROP TABLE dbo.Customers; DROP PROCEDURE dbo.ReadCustomer; DROP FUNCTION dbo.Answer;");
            Check(await RunSql(ticket, output) == 0, "initial SQL folder export into empty directory without project or database");
            Dictionary<string, string> first;
            using (var writer = new BundleWriter(output)) first = writer.Read();
            ValidateBundle(first);
            var text = string.Join('\n', first.Values);
            Check(!text.Contains("PRIVATE_", StringComparison.Ordinal) && !text.Contains("123456", StringComparison.Ordinal), "SQL values, comments, expressions and bodies omitted");
            Check(!text.Contains(ticket, StringComparison.Ordinal), "SQL source paths omitted");
            Check(first.Keys.Any(p => p.EndsWith("table-" + OkfBundle.Slug("Customers") + ".md", StringComparison.Ordinal)), "folder uses in.sql and does not run out.sql");
            var schema = SqlModelReader.Read(first);
            Check(schema.Relations.Count == 3 && schema.Routines.Count == 2, "SQL tables, view and routine signatures recovered");
            Check(schema.Relations.Single(r => r.Name == "Orders").ForeignKeys.Single().OnDelete == "CASCADE", "SQL foreign key actions");
            Check(await RunSql(input, output) == 0 && ReadBundle(output).OrderBy(p => p.Key).SequenceEqual(ReadBundleCopy(first).OrderBy(p => p.Key)), "file and folder reapplication is byte-stable");

            // Edit a ticket already reflected in the bundle: restore its baseline, then replay it.
            File.AppendAllText(input, "\nGO\nALTER TABLE dbo.Customers ADD Email nvarchar(200) NULL;\n");
            Check(await RunSql(input, output) == 0, "edited latest script reapplies from its baseline");
            using (var writer = new BundleWriter(output))
            {
                var updated = SqlModelReader.Read(writer.Read());
                Check(updated.Relations.Single(r => r.Name == "Customers").Columns.Count == 4, "edited script adds exactly one column");
            }
            var stable = ReadBundle(output);
            Check(await RunSql(ticket, output) == 0 && stable.OrderBy(p => p.Key).SequenceEqual(ReadBundle(output).OrderBy(p => p.Key)), "edited script repeated without duplicates");
            File.WriteAllText(input, File.ReadAllText(input).Replace("ALTER TABLE dbo.Customers ADD Email nvarchar(200) NULL;", "ALTER TABLE dbo.Customers ADD Phone nvarchar(40) NULL;", StringComparison.Ordinal));
            Check(await RunSql(input, output) == 0, "removing a statement from the latest script replays its original baseline");
            using (var writer = new BundleWriter(output))
            {
                var columns = SqlModelReader.Read(writer.Read()).Relations.Single(r => r.Name == "Customers").Columns;
                Check(columns.Any(c => c.Name == "Phone") && columns.All(c => c.Name != "Email"), "removed SQL changes do not linger in the model");
            }

            var legacy = Path.Combine(temp, "legacy");
            using (var writer = new BundleWriter(legacy)) writer.Write(OkfBundle.Render(Example())
                .ToDictionary(p => p.Key, p => p.Value.Replace("by: dbmapper/" + Cli.Version, "by: dbmapper/0.2.0", StringComparison.Ordinal)
                    .Replace("resource: \"SQL Server schema metadata", "resource: \"SQL Server catalog metadata", StringComparison.Ordinal)));
            var alter = Path.Combine(temp, "alter.sql");
            File.WriteAllText(alter, """
                ALTER TABLE dbo.Customers ADD Email nvarchar(200) NULL;
                ALTER TABLE dbo.Customers ALTER COLUMN Email nvarchar(300) NOT NULL;
                ALTER TABLE sales.Orders NOCHECK CONSTRAINT FK_Orders_Customers;
                """);
            Check(await RunSql(alter, legacy) == 0, "offline alteration reads an existing catalog bundle");
            using (var writer = new BundleWriter(legacy))
            {
                var updated = SqlModelReader.Read(writer.Read());
                var email = updated.Relations.Single(r => r.Name == "Customers").Columns.Single(c => c.Name == "Email");
                Check(email.Type == "nvarchar(300)" && !email.Nullable, "SQL ALTER COLUMN type and nullability");
                Check(updated.Relations.Single(r => r.Name == "Orders").ForeignKeys.Single().Disabled, "SQL NOCHECK state");
            }

            // Unsupported syntax must never commit the supported prefix of a script.
            var bad = Path.Combine(temp, "bad.sql");
            foreach (var tail in new[] { "EXEC(N'PRIVATE_DYNAMIC');", "IF 1 = 1 ALTER TABLE dbo.Customers ADD Unsafe int NULL;", "SELECT * INTO dbo.Unsafe FROM dbo.Customers;", "ROLLBACK;", "ALTER TABLE dbo.Customers ADD Inferred AS (1+2);", "CREATE TABLE dbo.Broken (", "CREATE INDEX IX_Partitioned ON dbo.Customers(Email) ON PartitionScheme(Email);", "SELECT 1;\nGO 2" })
            {
                File.WriteAllText(bad, "ALTER TABLE dbo.Customers ADD DoNotKeep int NULL;\n" + tail);
                var before = ReadBundle(legacy);
                Check(await RunSql(bad, legacy) == 2, "unsupported or malformed SQL rejected");
                Check(before.OrderBy(p => p.Key).SequenceEqual(ReadBundle(legacy).OrderBy(p => p.Key)), "SQL failure preserves entire bundle");
            }
            var beforeCancel = ReadBundle(legacy);
            Check(await RunSql(alter, legacy, cancellationToken: new CancellationToken(true)) == 130
                && beforeCancel.OrderBy(p => p.Key).SequenceEqual(ReadBundle(legacy).OrderBy(p => p.Key)), "cancelled SQL update preserves bundle");

            var server = Path.Combine(temp, "server");
            var serverBefore = OkfBundle.RenderServer(ServerExample());
            using (var writer = new BundleWriter(server)) writer.Write(serverBefore);
            File.WriteAllText(alter, "USE Application; ALTER TABLE dbo.Customers ADD Email nvarchar(200) NULL;");
            Check(await RunSql(alter, server, "Application") == 0, "SQL targets one existing server database");
            var prefix = "databases/db-" + OkfBundle.Slug("Application") + "/";
            using (var writer = new BundleWriter(server))
            {
                var after = writer.Read();
                ValidateBundle(after);
                Check(serverBefore.Where(p => !p.Key.StartsWith(prefix, StringComparison.Ordinal)).All(p => after[p.Key] == p.Value), "SQL targeted update preserves other databases and scan status");
                Check(after[prefix + "database.md"].Contains("Scope", StringComparison.Ordinal) && !after.Values.Any(v => v.Contains("prediction", StringComparison.OrdinalIgnoreCase)), "SQL uses ordinary bundle status");
            }
            Check(await RunSql(alter, server) == 2, "server SQL requires explicit database target");
            Check(await RunSql(alter, server, "Archive") == 2, "unscanned database cannot be treated as empty baseline");
            File.WriteAllText(alter, "USE Other; ALTER TABLE dbo.Customers ADD Email nvarchar(200) NULL;");
            var serverStable = ReadBundle(server);
            Check(await RunSql(alter, server, "Application") == 2 && serverStable.OrderBy(p => p.Key).SequenceEqual(ReadBundle(server).OrderBy(p => p.Key)), "wrong USE scope preserves server bundle");

            // Explicit out.sql is opt-in and uses the same schema updater.
            Check(await RunSql(rollback, output) == 0, "explicit rollback file supported");
            using (var writer = new BundleWriter(output))
                Check(SqlModelReader.Read(writer.Read()).Relations.Count == 0, "rollback removes objects and stale pages");
            File.AppendAllText(input, "\n-- another edit\n");
            Check(await RunSql(input, output) == 2, "editing an earlier script with later updates is refused");

            var guarded = Path.Combine(temp, "guard.sql");
            File.WriteAllText(guarded, "IF OBJECT_ID(N'dbo.Customers', N'U') IS NULL BEGIN CREATE TABLE dbo.Customers (Id int NOT NULL); END;");
            Check(await RunSql(guarded, output) == 0, "object existence guards use local metadata");

            using (var writer = new BundleWriter(legacy)) writer.Write(OkfBundle.Render(Example()));
            Check(!File.Exists(Path.Combine(legacy, "sql-history.md")), "catalog refresh resets local replay history");
            File.AppendAllText(Path.Combine(legacy, "index.md"), "User edit\n");
            Check(await RunSql(input, legacy) == 2, "offline mode preserves edited bundle files");
            Check(await RunSql(Path.Combine(temp, "missing"), Path.Combine(temp, "missing-output")) == 2, "missing SQL folder rejected");
        }
        finally { DeleteTemp(temp); }
    }

    private static Dictionary<string, string> ReadBundleCopy(Dictionary<string, string> files) => files.ToDictionary(
        p => p.Key.Replace('/', Path.DirectorySeparatorChar), p => BundleWriter.Seal(p.Value), StringComparer.Ordinal);

    private static async Task<int> RunSql(string path, string output, string? database = null, CancellationToken cancellationToken = default)
    {
        var args = new List<string> { "--sql", path, "--output", output };
        if (database is not null) args.AddRange(["--update-database", database]);
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var result = await Cli.RunAsync(args.ToArray(), stdout, stderr, cancellationToken);
        Check(!stdout.ToString().Contains("PRIVATE_", StringComparison.Ordinal) && !stderr.ToString().Contains("PRIVATE_", StringComparison.Ordinal), "SQL diagnostics omit source text");
        if (result is not (0 or 2 or 130)) throw new CheckFailure("Unexpected offline exit: " + result + ". " + stderr);
        return result;
    }

    private static async Task OfflineSqlIntegration(SqlConnection admin, string adminSecret, string temp)
    {
        Console.WriteLine("Comparing offline SQL metadata with an actual SQL Server catalog refresh...");
        var database = "DbMapperTest_OfflineSql_" + Guid.NewGuid().ToString("N");
        await Execute(admin, $"CREATE DATABASE [{database}];");
        try
        {
            await using var fixture = new SqlConnection(new SqlConnectionStringBuilder(adminSecret) { InitialCatalog = database }.ConnectionString);
            await fixture.OpenAsync();
            var batches = new[]
            {
                """
                CREATE TABLE dbo.Parents (
                    Id int IDENTITY(1,1) NOT NULL CONSTRAINT PK_Parents PRIMARY KEY,
                    Name nvarchar(50) COLLATE Latin1_General_100_CI_AS NULL,
                    Flag bit NOT NULL CONSTRAINT DF_Parents_Flag DEFAULT 1,
                    CONSTRAINT CK_Parents_Flag CHECK (Flag IN (0,1))
                );
                CREATE TABLE dbo.Children (
                    Id int NOT NULL CONSTRAINT PK_Children PRIMARY KEY NONCLUSTERED,
                    ParentId int NOT NULL,
                    Amount decimal(18,2) NOT NULL,
                    CONSTRAINT FK_Children_Parents FOREIGN KEY (ParentId) REFERENCES dbo.Parents(Id) ON DELETE CASCADE
                );
                CREATE INDEX IX_Children_Parent ON dbo.Children(ParentId DESC) INCLUDE(Amount) WHERE Amount > 0;
                """,
                "CREATE VIEW dbo.ParentNames AS SELECT Id, Name FROM dbo.Parents;",
                "CREATE PROCEDURE dbo.ReadParent @Id int, @Total decimal(18,2) OUTPUT AS SELECT @Total = 1;",
                "CREATE FUNCTION dbo.Value(@Input int) RETURNS int AS BEGIN RETURN @Input; END;",
                "CREATE TRIGGER dbo.ParentGuard ON dbo.Parents AFTER INSERT AS SELECT 1;"
            };
            var source = Path.Combine(temp, "catalog-compare.sql");
            var output = Path.Combine(temp, "catalog-compare");
            File.WriteAllText(source, string.Join("\nGO\n", batches));
            Check(await RunSql(source, output) == 0, "offline initial script before any database DDL");
            foreach (var batch in batches) await Execute(fixture, batch);
            await Compare();
            var changes = new[]
            {
                """
                ALTER TABLE dbo.Parents ADD Email nvarchar(80) COLLATE Latin1_General_100_CI_AS NULL;
                ALTER TABLE dbo.Parents ALTER COLUMN Email nvarchar(120) COLLATE Latin1_General_100_CI_AS NOT NULL;
                ALTER TABLE dbo.Children NOCHECK CONSTRAINT FK_Children_Parents;
                ALTER INDEX IX_Children_Parent ON dbo.Children DISABLE;
                ALTER TABLE dbo.Children ADD Temporary int NULL;
                ALTER TABLE dbo.Children DROP COLUMN Temporary;
                """,
                "CREATE OR ALTER PROCEDURE dbo.ReadParent @Id bigint, @Total decimal(20,3) OUTPUT AS SELECT @Total = 2;"
            };
            source = Path.Combine(temp, "catalog-compare-alter.sql");
            File.WriteAllText(source, string.Join("\nGO\n", changes));
            Check(await RunSql(source, output) == 0, "offline ALTER before database DDL");
            foreach (var batch in changes) await Execute(fixture, batch);
            await Compare();

            async Task Compare()
            {
                var live = OkfBundle.Render(await CatalogReader.ReadAsync(adminSecret, 10, CancellationToken.None, database));
                using var writer = new BundleWriter(output);
                var offline = OkfBundle.Render(SqlModelReader.Read(writer.Read()));
                Check(live.Count == offline.Count, "offline/live object file counts match");
                foreach (var (path, content) in live)
                {
                    if (offline.TryGetValue(path, out var result) && content != result)
                    {
                        foreach (var mismatch in content.Split('\n').Zip(result.Split('\n')).Where(pair => pair.First != pair.Second))
                            Console.Error.WriteLine("Synthetic catalog metadata: " + mismatch.First + "\nOffline metadata: " + mismatch.Second);
                    }
                    Check(offline.TryGetValue(path, out var actual) && content == actual, "offline/live metadata match: " + path);
                }
            }
        }
        finally { await Execute(admin, $"ALTER DATABASE [{database}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{database}];"); }
    }
}
