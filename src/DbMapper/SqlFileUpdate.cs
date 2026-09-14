using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace DbMapper;

internal static class SqlFileUpdate
{
    private const string HistoryFile = "sql-history.md";
    private const string JsonStart = "```json\n";
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private sealed record AppliedScript(string Source, string Content, SchemaModel Before);
    private sealed record History(int Version, List<AppliedScript> Scripts);

    public static Dictionary<string, string> Apply(Options options, Dictionary<string, string>? existing, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (existing?.Count == 0) existing = null;
        var path = Path.GetFullPath(options.Sql!);
        if (Directory.Exists(path)) path = Path.Combine(path, "in.sql");
        if (!path.EndsWith(".sql", StringComparison.OrdinalIgnoreCase) || !File.Exists(path))
            throw new UsageException("--sql requires an existing .sql file or a ticket folder containing in.sql.");
        var script = File.ReadAllText(path, new UTF8Encoding(false, true)).Replace("\r\n", "\n", StringComparison.Ordinal);
        var source = Hash(Path.GetRelativePath(Directory.GetCurrentDirectory(), path).Replace('\\', '/'));
        var hash = Hash(script);
        var prefix = "";
        if (options.UpdateDatabase is not null)
        {
            if (existing is null) throw new UsageException("--sql with --update-database requires an existing server bundle.");
            OkfBundle.RequireServerDatabase(existing, options.UpdateDatabase);
            prefix = "databases/db-" + OkfBundle.Slug(options.UpdateDatabase) + "/";
        }
        else if (existing?.ContainsKey("server.md") == true)
            throw new UsageException("Use --update-database with --sql to choose one database in the existing server bundle.");
        var scope = existing?.Where(p => p.Key.StartsWith(prefix, StringComparison.Ordinal))
            .ToDictionary(p => p.Key[prefix.Length..], p => p.Value, StringComparer.OrdinalIgnoreCase);
        var history = new History(1, []);
        if (scope?.TryGetValue(HistoryFile, out var stored) == true)
        {
            var start = stored.IndexOf(JsonStart, StringComparison.Ordinal);
            var end = stored.LastIndexOf("\n```", StringComparison.Ordinal);
            if (start < 0 || end < start + JsonStart.Length) throw HistoryError();
            try { history = JsonSerializer.Deserialize<History>(stored[(start + JsonStart.Length)..end], JsonOptions) ?? throw HistoryError(); }
            catch (JsonException) { throw HistoryError(); }
            if (history.Version != 1 || history.Scripts is null) throw HistoryError();
        }
        var previous = history.Scripts.FindIndex(s => s.Source == source);
        if (previous >= 0 && history.Scripts[previous].Content == hash) return existing!;
        if (previous >= 0 && previous != history.Scripts.Count - 1)
            throw new UsageException("This script has later SQL updates in the bundle. Restore the bundle from before this script and reapply the scripts in order, or refresh from the database first.");
        var before = previous >= 0 ? history.Scripts[previous].Before : scope is null ? new SchemaModel() : SqlModelReader.Read(scope);
        // The saved baseline contains only the same structural metadata as the bundle, never SQL text.
        var model = JsonSerializer.Deserialize<SchemaModel>(JsonSerializer.Serialize(before, JsonOptions), JsonOptions)!;
        SqlScript.Apply(model, script, options.UpdateDatabase, cancellationToken);
        if (previous >= 0) history.Scripts.RemoveAt(previous);
        // ponytail: one metadata snapshot per script; database refresh clears it. Use deltas if history size becomes material.
        history.Scripts.Add(new(source, hash, before));
        var files = options.UpdateDatabase is null ? OkfBundle.Render(model) : OkfBundle.UpdateServerDatabase(existing!, options.UpdateDatabase, model);
        files[prefix + HistoryFile] = OkfBundle.Header("SQL Update History", "Local SQL update history",
            "Metadata baselines for repeatable local SQL updates. SQL text and file paths are omitted.") + """
            # SQL update history

            This generated history lets dbmapper reapply an edited script without duplicating its earlier changes. It contains source/content fingerprints and structural metadata only. Run from the same project directory each time. A database refresh resets this history.

            [Database overview](database.md)

            """ + "\n" + JsonStart + JsonSerializer.Serialize(history, JsonOptions) + "\n```\n";
        files[prefix + HistoryFile] = files[prefix + HistoryFile].Replace("\r\n", "\n", StringComparison.Ordinal);
        files[prefix + "index.md"] += "\n* [SQL update history](sql-history.md) - Metadata used to reapply local scripts.\n";
        cancellationToken.ThrowIfCancellationRequested();
        return files;
    }

    private static string Hash(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();
    private static UsageException HistoryError() => new("The SQL update history is unsupported. Restore the bundle or refresh this database before applying SQL files.");
}
