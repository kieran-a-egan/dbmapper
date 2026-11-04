using System.Text.Json;
using DbMapper;

internal static partial class Program
{
    private const string DefinitionText = "CREATE VIEW dbo.Sample AS SELECT 1 AS Id;\r\n/*\r\n````\r\n<script>[link](missing.md)</script>\r\n<!-- dbmapper:sha256=fake -->\r\n*/";

    private static SchemaModel DefinitionExample() => new()
    {
        Relations = [new(1, "dbo", "Sample", "View", "NOT_APPLICABLE")],
        Definitions = new() { [(1, null)] = DefinitionText }
    };

    private static async Task DefinitionTests()
    {
        Check(!Options.Parse([]).IncludeDefinitions, "definitions off by default");
        foreach (var mode in new string[][] { [], ["--all-databases"], ["--database", "Application"], ["--update-database", "Application"] })
            Check(Options.Parse(["--include-definitions", .. mode]).IncludeDefinitions, "definition flag accepted in live mode");
        Throws(() => Options.Parse(["--include-definitions", "--include-definitions"]), "duplicate definition flag refused");
        Throws(() => Options.Parse(["--sql", "in.sql", "--include-definitions"]), "offline definition flag refused");

        var model = DefinitionExample();
        var single = OkfBundle.Render(model);
        Check(single.Values.Any(page => page.Contains("\n`````sql\n" + DefinitionText.Replace("\r\n", "\n", StringComparison.Ordinal) + "\n`````\n", StringComparison.Ordinal)),
            "SQL remains literal inside a fence longer than any embedded fence");
        Check(!JsonSerializer.Serialize(model).Contains("CREATE VIEW", StringComparison.Ordinal), "definitions excluded from metadata serialization");

        var metadataServer = OkfBundle.RenderServer(ServerExample());
        var mixed = OkfBundle.UpdateServerDatabase(metadataServer, "Application", model);
        Check(mixed["CLAUDE.md"].Contains("definition coverage", StringComparison.Ordinal)
            && mixed["server.md"].Contains("opted-in definitions", StringComparison.Ordinal), "server guides describe mixed coverage");
        Check(OkfBundle.UpdateServerDatabase(mixed, "Application", Example()).OrderBy(p => p.Key)
            .SequenceEqual(metadataServer.OrderBy(p => p.Key)), "refresh without opt-in restores original metadata-only bundle");

        var temp = NewTemp();
        try
        {
            var script = Path.Combine(temp, "in.sql");
            File.WriteAllText(script, "CREATE TABLE dbo.OfflineTest (Id int NOT NULL);");
            var singlePath = Path.Combine(temp, "single");
            using (var writer = new BundleWriter(singlePath))
            {
                writer.Write(single);
                Check(writer.Read().OrderBy(p => p.Key).SequenceEqual(single.OrderBy(p => p.Key)), "footer-like SQL comment survives write and read");
            }
            var mixedPath = Path.Combine(temp, "mixed");
            using (var writer = new BundleWriter(mixedPath)) writer.Write(mixed);
            var singleBefore = ReadBundle(singlePath);
            var mixedBefore = ReadBundle(mixedPath);
            Check(await Cli.RunAsync(["--sql", script, "--output", singlePath], TextWriter.Null, TextWriter.Null) == 2
                && singleBefore.OrderBy(p => p.Key).SequenceEqual(ReadBundle(singlePath).OrderBy(p => p.Key)), "offline refusal preserves definition bundle");
            Check(await Cli.RunAsync(["--sql", script, "--output", mixedPath, "--update-database", "Application"], TextWriter.Null, TextWriter.Null) == 2
                && mixedBefore.OrderBy(p => p.Key).SequenceEqual(ReadBundle(mixedPath).OrderBy(p => p.Key)), "offline refusal preserves targeted definition subtree and server bundle");
            Check(await Cli.RunAsync(["--sql", script, "--output", mixedPath, "--update-database", "Reporting"], TextWriter.Null, TextWriter.Null) == 0,
                "offline update is allowed for another database in the mixed bundle");
            var application = Path.Combine("databases", "db-" + OkfBundle.Slug("Application")) + Path.DirectorySeparatorChar;
            Check(mixedBefore.Where(p => p.Key.StartsWith(application, StringComparison.Ordinal)).OrderBy(p => p.Key)
                .SequenceEqual(ReadBundle(mixedPath).Where(p => p.Key.StartsWith(application, StringComparison.Ordinal)).OrderBy(p => p.Key)),
                "offline update leaves the other database's definition pages intact");
        }
        finally { DeleteTemp(temp); }
    }
}
