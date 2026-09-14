using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace DbMapper;

// Read the existing metadata-only format, including bundles created before offline updates.
internal static class SqlModelReader
{
    public static SchemaModel Read(IReadOnlyDictionary<string, string> files)
    {
        if (!files.ContainsKey("database.md") || !files.ContainsKey("CLAUDE.md")) throw Unsupported();
        var model = new SchemaModel();
        var schemas = files.Where(p => p.Key.EndsWith("/schema.md", StringComparison.Ordinal))
            .ToDictionary(p => Path.GetDirectoryName(p.Key)!, p => Title(p.Value), StringComparer.Ordinal);
        var objects = files.Where(p => p.Key.StartsWith("schemas/", StringComparison.Ordinal)
            && (p.Value.StartsWith("---\ntype: \"SQL Server Table\"", StringComparison.Ordinal)
                || p.Value.StartsWith("---\ntype: \"SQL Server View\"", StringComparison.Ordinal)
                || p.Value.StartsWith("---\ntype: \"SQL Server Routine\"", StringComparison.Ordinal)))
            .OrderBy(p => p.Key, StringComparer.Ordinal).ToList();
        var relations = new Dictionary<string, Relation>();
        var id = 0;
        foreach (var (path, text) in objects)
        {
            if (!schemas.TryGetValue(Path.GetDirectoryName(path)!, out var schema)) throw Unsupported();
            var title = Title(text);
            if (!title.StartsWith(schema + ".", StringComparison.Ordinal)) throw Unsupported();
            var name = title[(schema.Length + 1)..];
            if (text.StartsWith("---\ntype: \"SQL Server Routine\"", StringComparison.Ordinal))
            {
                var routine = new Routine(++id, schema, name, Decode(Match(text, @"\nKind: <code>(.*?)</code>\.")));
                foreach (var row in Rows(Section(text, "Schema")))
                    routine.Parameters.Add(new(Number(row[0]), Number(row[0]) == 0 ? "" : Decode(row[1]), Decode(row[2]), Yes(row[3]), Yes(row[4])));
                model.Routines.Add(routine);
                continue;
            }
            var relation = new Relation(++id, schema, name,
                Match(text, @"\nKind: (Table|View)\."), Decode(Match(text, @"Temporal role: <code>(.*?)</code>\.")));
            foreach (var row in Rows(Section(text, "Schema")))
                relation.Columns.Add(new(Number(row[0]), Decode(row[1]), Decode(row[2]), Yes(row[3]), Yes(row[4]), Yes(row[5]),
                    Yes(row[6]), Yes(row[7]), Yes(row[8]), Decode(row[9]), row[10] == "—" ? null : Decode(row[10])));
            foreach (var block in Blocks(Section(text, "Keys and indexes")))
            {
                var index = new DbIndex(relation.Indexes.Count + 1, Decode(block.Name), Decode(Match(block.Text, @"Type: <code>(.*?)</code>;")),
                    Flag(block.Text, "unique"), Flag(block.Text, "primary key"), Flag(block.Text, "unique constraint"), Flag(block.Text, "filtered"), Flag(block.Text, "disabled"));
                foreach (var row in Rows(block.Text))
                    index.Columns.Add(new(Number(row[0]), Decode(row[1]), Number(row[2]), row[3] == "DESC", Yes(row[4]), Number(row[5])));
                relation.Indexes.Add(index);
            }
            foreach (var row in Rows(Section(text, "Check constraints"))) relation.Checks.Add(new(Decode(row[0]), Yes(row[1]), Yes(row[2])));
            foreach (var row in Rows(Section(text, "Triggers"))) relation.Triggers.Add(new(Decode(row[0]), Yes(row[1]), Yes(row[2])));
            model.Relations.Add(relation);
            relations.Add(path, relation);
        }
        foreach (var (path, relation) in relations)
        {
            foreach (var block in Blocks(Section(files[path], "Foreign keys")))
            {
                var label = Match(block.Text, @"References (.*?)\. On delete:");
                var link = Regex.Match(label, @"^\[(.*?)\]\([^)]*\)$");
                if (link.Success) label = link.Groups[1].Value;
                var targets = model.Relations.Where(r => OkfBundle.Text(r.Schema) + "." + OkfBundle.Text(r.Name) == label).ToList();
                if (targets.Count != 1) throw Unsupported();
                var target = targets[0];
                var key = new ForeignKey(++id, Decode(block.Name), target.Id, target.Schema, target.Name,
                    Decode(Match(block.Text, @"On delete: <code>(.*?)</code>;")), Decode(Match(block.Text, @"on update: <code>(.*?)</code>;")),
                    Flag(block.Text, "disabled"), Flag(block.Text, "untrusted"));
                foreach (var row in Rows(block.Text)) key.Columns.Add(new(Number(row[0]), Decode(row[1]), Decode(row[2])));
                relation.ForeignKeys.Add(key);
            }
        }
        // Refuse a format we cannot recover losslessly, rather than dropping metadata during an update.
        var rendered = OkfBundle.Render(model);
        foreach (var (path, text) in files.Where(p => p.Key.StartsWith("schemas/", StringComparison.Ordinal)))
            if (!rendered.TryGetValue(path, out var roundTrip) || NormalizeHeader(roundTrip) != NormalizeHeader(text)) throw Unsupported();
        if (rendered.Keys.Any(path => path.StartsWith("schemas/", StringComparison.Ordinal) && !files.ContainsKey(path))) throw Unsupported();
        return model;
    }

    private static string Title(string text) => JsonSerializer.Deserialize<string>(Match(text, @"(?m)^title: (.+)$")) ?? throw Unsupported();
    private static string NormalizeHeader(string text) => Regex.Replace(text, @"(?m)^  by: dbmapper/\d+\.\d+\.\d+$", "  by: dbmapper")
        .Replace("resource: \"SQL Server catalog metadata for this scope;", "resource: \"SQL Server schema metadata for this scope;", StringComparison.Ordinal);
    private static string Match(string text, string pattern)
    {
        var match = Regex.Match(text, pattern);
        return match.Success ? match.Groups[1].Value : throw Unsupported();
    }
    private static bool Flag(string text, string name) => Yes(Match(text, Regex.Escape(name) + @": (yes|no)[;.]"));
    private static bool Yes(string value) => value switch { "yes" => true, "no" => false, _ => throw Unsupported() };
    private static int Number(string value) => value == "—" ? 0 : int.Parse(value, System.Globalization.CultureInfo.InvariantCulture);
    private static string Decode(string value)
    {
        if (value.StartsWith("<code>", StringComparison.Ordinal) && value.EndsWith("</code>", StringComparison.Ordinal)) value = value[6..^7];
        value = Regex.Replace(value, @"\\u([0-9A-F]{4})", m => ((char)Convert.ToInt32(m.Groups[1].Value, 16)).ToString());
        return WebUtility.HtmlDecode(value);
    }
    private static string Section(string text, string heading)
    {
        var start = text.IndexOf("\n# " + heading + "\n", StringComparison.Ordinal);
        if (start < 0) throw Unsupported();
        start += heading.Length + 4;
        var end = text.IndexOf("\n# ", start, StringComparison.Ordinal);
        return end < 0 ? text[start..] : text[start..end];
    }
    private static IEnumerable<string[]> Rows(string text) => text.Split('\n')
        .Where(line => line.StartsWith("| ", StringComparison.Ordinal)).Skip(2)
        .Select(line => line[2..^2].Split(" | ", StringSplitOptions.None));
    private static IEnumerable<(string Name, string Text)> Blocks(string text)
    {
        foreach (var block in text.Split("\n## ", StringSplitOptions.None).Skip(1))
        {
            var end = block.IndexOf('\n');
            if (end < 0) throw Unsupported();
            yield return (block[..end], block[(end + 1)..]);
        }
    }
    private static UsageException Unsupported() => new("The existing schema cannot be read losslessly for an offline update. Refresh this scope with the current dbmapper first. No bundle files were changed.");
}
