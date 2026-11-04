using System.Text.RegularExpressions;
using DbMapper;

internal static partial class Program
{
    // Byte-exact golden trees under tests/fixtures/okf/. Each golden is the sealed on-disk form written by
    // BundleWriter (LF content plus the integrity footer). Regenerate deliberately with DBMAPPER_UPDATE_GOLDENS=1
    // and review the diff; a normal run only compares.
    private static void BundleGoldenTests()
    {
        var root = FindRepositoryRoot();
        var fixtures = Path.Combine(root, "tests", "fixtures", "okf");
        var server = OkfBundle.RenderServer(ServerExample());
        var cases = new (string Name, Dictionary<string, string> Files)[]
        {
            ("single-database", OkfBundle.Render(Example())),
            ("server-wide", server),
            ("selected", OkfBundle.RenderServer(ServerExample().Take(2).ToList(), selectedOnly: true)),
            ("targeted-update", OkfBundle.UpdateServerDatabase(server, "Archive", Example()))
        };
        var updating = Environment.GetEnvironmentVariable("DBMAPPER_UPDATE_GOLDENS") == "1";
        var temp = NewTemp();
        try
        {
            foreach (var (name, files) in cases)
            {
                foreach (var text in files.Values)
                {
                    Check(!text.Contains('\r'), name + " renders LF only");
                    Check(!text.Contains(root, StringComparison.OrdinalIgnoreCase) && !Regex.IsMatch(text, @"[A-Za-z]:\\"), name + " has no absolute paths");
                    Check(!Regex.IsMatch(text, @"\b\d{4}-\d{2}-\d{2}[T ]\d{2}:\d{2}"), name + " has no timestamps");
                    Check(!Regex.IsMatch(text, @"(?i)password|pwd=|server=|data source=|token="), name + " has no credential-like text");
                }

                var scratch = Path.Combine(temp, name);
                using (var writer = new BundleWriter(scratch)) writer.Write(files);
                var golden = Path.Combine(fixtures, name);
                if (updating)
                {
                    if (Directory.Exists(golden)) Directory.Delete(golden, recursive: true);
                    CopyTree(scratch, golden);
                    continue;
                }

                var actual = RelativeFiles(scratch);
                var expected = Directory.Exists(golden) ? RelativeFiles(golden) : new HashSet<string>(StringComparer.Ordinal);
                Check(actual.Count > 0 && expected.SetEquals(actual), name + " file set matches golden tree");
                foreach (var relative in actual)
                {
                    var expectedBytes = File.ReadAllBytes(Path.Combine(golden, relative));
                    Check(expectedBytes.AsSpan().SequenceEqual(File.ReadAllBytes(Path.Combine(scratch, relative))), name + " byte-exact: " + relative);
                    Check(!expectedBytes.Contains((byte)'\r'), name + " golden is LF on disk: " + relative);
                    Check(Regex.IsMatch(System.Text.Encoding.UTF8.GetString(expectedBytes), @"\n<!-- dbmapper:sha256=[0-9a-f]{64} -->\n\z"), name + " golden ends with integrity footer: " + relative);
                }

                // Simulate a CRLF checkout: reading must strip the footer and normalise line endings to the same content.
                var crlf = Path.Combine(temp, name + "-crlf");
                foreach (var relative in actual)
                {
                    var destination = Path.Combine(crlf, relative);
                    Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                    File.WriteAllText(destination, File.ReadAllText(Path.Combine(golden, relative)).Replace("\n", "\r\n", StringComparison.Ordinal));
                }
                using (var writer = new BundleWriter(crlf))
                    Check(writer.Read().OrderBy(p => p.Key).SequenceEqual(files.OrderBy(p => p.Key)), name + " CRLF checkout reads back to identical content");
            }
            Check(OkfBundle.RenderServer(ServerExample()).OrderBy(p => p.Key).SequenceEqual(server.OrderBy(p => p.Key)), "server render is repeatable");
        }
        finally { DeleteTemp(temp); }
    }

    private static HashSet<string> RelativeFiles(string directory) => Directory.GetFiles(directory, "*", SearchOption.AllDirectories)
        .Select(p => Path.GetRelativePath(directory, p).Replace('\\', '/')).ToHashSet(StringComparer.Ordinal);

    private static void CopyTree(string source, string destination)
    {
        foreach (var relative in RelativeFiles(source))
        {
            var target = Path.Combine(destination, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(Path.Combine(source, relative), target, overwrite: true);
        }
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (Directory.Exists(Path.Combine(directory.FullName, "tests", "fixtures"))) return directory.FullName;
        throw new CheckFailure("FAIL: tests/fixtures not found above the test binary");
    }
}
