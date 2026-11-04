using DbMapper;

internal static partial class Program
{
    // Offline SQL replay and bundle-writer safety. Every case runs without secrets or a SQL connection.
    // Exit 4 (partial server export) and exit 3 (SQL errors) need a live catalog and are not covered here.
    private static async Task OfflineSafetyTests()
    {
        var temp = NewTemp();
        try
        {
            var script = Path.Combine(temp, "ordering.sql");
            var output = Path.Combine(temp, "bundle");

            // Script order: a statement that depends on a later CREATE is refused and nothing is written.
            File.WriteAllText(script, "ALTER TABLE dbo.Later ADD Id int NULL;\nGO\nCREATE TABLE dbo.Later (Id int NOT NULL);");
            Check(await RunSql(script, output) == 2 && !Directory.Exists(output), "ALTER before its CREATE is refused and creates no bundle");

            // Batches in one script apply in written order.
            File.WriteAllText(script, "CREATE TABLE dbo.Later (Id int NOT NULL);\nGO\nALTER TABLE dbo.Later ADD Name nvarchar(20) NULL;");
            Check(await RunSql(script, output) == 0, "CREATE then ALTER in one script applies in order");
            using (var writer = new BundleWriter(output))
                Check(SqlModelReader.Read(writer.Read()).Relations.Single(r => r.Name == "Later").Columns.Count == 2, "ordered script yields both columns");

            // A rejected script that follows a good one leaves the previously written bundle byte-identical.
            var sealedBefore = ReadBundle(output);
            File.WriteAllText(script, "ALTER TABLE dbo.Later ADD Extra int NULL;\nGO\nALTER TABLE dbo.Missing ADD Extra int NULL;");
            Check(await RunSql(script, output) == 2 && sealedBefore.OrderBy(p => p.Key).SequenceEqual(ReadBundle(output).OrderBy(p => p.Key)), "later failing batch preserves prior bundle");

            // Parser rejection on a fresh output leaves no directory behind.
            var fresh = Path.Combine(temp, "fresh");
            File.WriteAllText(script, "CREATE TABLE dbo.Broken (Id int NOT NULL);\nGO\nEXEC(N'PRIVATE_EXEC');");
            Check(await RunSql(script, fresh) == 2 && !Directory.Exists(fresh), "rejected fresh script creates no output directory");

            // Foreign and edited files: refused whole, and the foreign content is kept byte-for-byte.
            File.WriteAllText(script, "CREATE TABLE dbo.Later (Id int NOT NULL);");
            var foreign = Path.Combine(temp, "foreign");
            Directory.CreateDirectory(foreign);
            var notes = Path.Combine(foreign, "notes.txt");
            File.WriteAllText(notes, "Keep these notes");
            Check(await RunSql(script, foreign) == 2 && File.ReadAllText(notes) == "Keep these notes" && Directory.GetFiles(foreign).Length == 1,
                "non-markdown foreign file refused and kept");
            var edited = Path.Combine(temp, "edited");
            Check(await RunSql(script, edited) == 0, "fresh bundle written for edit test");
            var index = Path.Combine(edited, "index.md");
            File.AppendAllText(index, "Handwritten\n");
            var editedBefore = ReadBundle(edited);
            Check(await RunSql(script, edited) == 2 && editedBefore.OrderBy(p => p.Key).SequenceEqual(ReadBundle(edited).OrderBy(p => p.Key)), "edited generated file refused and whole bundle kept");

            if (!OperatingSystem.IsWindows())
            {
                var link = Path.Combine(temp, "linked-output");
                Directory.CreateSymbolicLink(link, foreign);
                Directory.CreateSymbolicLink(Path.Combine(output, "linked-child"), foreign);
                foreach (var candidate in new[] { link, Path.Combine(link, "nested"), output })
                {
                    var refused = await RunCli("--sql", script, "--output", candidate);
                    Check(refused.Code == 2 && refused.Err.Contains("symbolic links or junctions", StringComparison.Ordinal),
                        "Unix symbolic link refused");
                }
                Check(File.ReadAllText(notes) == "Keep these notes", "linked target kept");
            }

            // Locking: a concurrent export holding the output gate refuses the replay and creates nothing.
            var locked = Path.Combine(temp, "locked");
            using (var holder = new BundleWriter(locked))
                Check(await RunSql(script, locked) == 2 && !Directory.Exists(locked), "output locked by another writer refuses offline replay");

            // Cancellation before any write leaves a targeted server bundle intact.
            var server = Path.Combine(temp, "server");
            using (var writer = new BundleWriter(server)) writer.Write(OkfBundle.RenderServer(ServerExample()));
            var serverBefore = ReadBundle(server);
            File.WriteAllText(script, "USE Application; ALTER TABLE dbo.Customers ADD Email nvarchar(200) NULL;");
            Check(await RunSql(script, server, "Application", new CancellationToken(true)) == 130
                && serverBefore.OrderBy(p => p.Key).SequenceEqual(ReadBundle(server).OrderBy(p => p.Key)), "cancelled targeted offline update preserves server bundle");

            // An edited page in one database refuses a targeted update of a different database.
            var reporting = Path.Combine(server, "databases", "db-" + OkfBundle.Slug("Reporting"), "index.md");
            File.AppendAllText(reporting, "Local edit\n");
            var serverEdited = ReadBundle(server);
            Check(await RunSql(script, server, "Application") == 2 && serverEdited.OrderBy(p => p.Key).SequenceEqual(ReadBundle(server).OrderBy(p => p.Key)),
                "edited sibling database refuses targeted offline update");
        }
        finally { DeleteTemp(temp); }
    }
}
