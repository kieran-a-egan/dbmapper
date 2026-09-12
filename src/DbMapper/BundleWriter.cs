using System.Security.Cryptography;
using System.Text;

namespace DbMapper;

internal sealed class BundleWriter : IDisposable
{
    private const string Marker = "\n<!-- dbmapper:sha256=";
    private readonly string target;
    private readonly string parent;
    private readonly FileStream gate;

    public BundleWriter(string directory)
    {
        target = Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory));
        parent = Path.GetDirectoryName(target) ?? throw new UsageException("Use a dedicated output subdirectory, not a filesystem root.");
        var relativeCwd = Path.GetRelativePath(target, Directory.GetCurrentDirectory());
        if (relativeCwd == "." || (!Path.IsPathRooted(relativeCwd) && relativeCwd != ".." && !relativeCwd.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal)))
            throw new UsageException("The output cannot be the current directory or one of its ancestors. Use a dedicated bundle subdirectory.");
        CheckParents(target);
        Directory.CreateDirectory(parent);
        var lockName = ".dbmapper-" + Hash(target.ToUpperInvariant())[..16] + ".lock";
        try { gate = new(Path.Combine(parent, lockName), FileMode.CreateNew, FileAccess.Write, FileShare.None, 1, FileOptions.DeleteOnClose); }
        catch (IOException) { throw new UsageException("The output is locked by another export or an existing lock file. No bundle files were changed."); }
        try { ValidateExisting(target); }
        catch { gate.Dispose(); throw; }
    }

    public void Write(IReadOnlyDictionary<string, string> files)
    {
        var stage = Path.Combine(parent, ".dbmapper-" + Guid.NewGuid().ToString("N") + ".stage");
        var backup = Path.Combine(parent, ".dbmapper-" + Guid.NewGuid().ToString("N") + ".backup");
        var installed = false;
        if (Directory.Exists(stage) || Directory.Exists(backup) || File.Exists(stage) || File.Exists(backup))
            throw new UsageException("A temporary output path already exists. Retry the export.");
        try
        {
            Directory.CreateDirectory(stage);
            foreach (var (relative, text) in files)
            {
                var path = Path.GetFullPath(Path.Combine(stage, relative));
                if (!path.StartsWith(stage + Path.DirectorySeparatorChar, StringComparison.Ordinal) || !path.EndsWith(".md", StringComparison.Ordinal))
                    throw new UsageException("An invalid generated path was refused.");
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                using var writer = new StreamWriter(stream, new UTF8Encoding(false));
                writer.Write(Seal(text));
            }
            CheckParents(target);
            ValidateExisting(target); // Catch edits made while catalog queries were running.
            if (Directory.Exists(target)) Directory.Move(target, backup);
            try
            {
                Directory.Move(stage, target);
                installed = true;
            }
            catch
            {
                if (Directory.Exists(backup) && !Directory.Exists(target)) Directory.Move(backup, target);
                throw;
            }
        }
        finally
        {
            CleanTemporary(stage);
            // Retain a backup if rollback was unable to restore the target.
            if (installed) CleanTemporary(backup);
        }
    }

    public void Dispose() => gate.Dispose();

    internal static string Seal(string value) => value + Marker + Hash(value) + " -->\n";

    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    private static void ValidateExisting(string path)
    {
        CheckParents(path);
        if (File.Exists(path)) throw new UsageException("The output path is an existing file. Select a dedicated directory.");
        if (!Directory.Exists(path)) return;
        var pending = new Stack<string>();
        pending.Push(path);
        while (pending.TryPop(out var current))
        {
            foreach (var entry in Directory.EnumerateFileSystemEntries(current))
            {
                var attributes = File.GetAttributes(entry);
                if ((attributes & FileAttributes.ReparsePoint) != 0)
                    throw new UsageException("Output paths must not contain symbolic links or junctions.");
                if ((attributes & FileAttributes.Directory) != 0)
                {
                    pending.Push(entry);
                    continue;
                }
                if (!entry.EndsWith(".md", StringComparison.Ordinal)) Refuse();
                var content = File.ReadAllText(entry, Encoding.UTF8).Replace("\r\n", "\n", StringComparison.Ordinal);
                var marker = content.LastIndexOf(Marker, StringComparison.Ordinal);
                if (marker < 0 || content != Seal(content[..marker])) Refuse();
            }
        }

        static void Refuse() => throw new UsageException("The output contains edited or non-dbmapper files. Preserve those files and choose a new empty output directory.");
    }

    private static void CheckParents(string path)
    {
        for (var current = new DirectoryInfo(path); current is not null; current = current.Parent)
        {
            // Attributes also exposes a dangling link, unlike Directory.Exists.
            try
            {
                if ((File.GetAttributes(current.FullName) & FileAttributes.ReparsePoint) != 0)
                    throw new UsageException("Output paths must not contain symbolic links or junctions.");
            }
            catch (FileNotFoundException) { }
            catch (DirectoryNotFoundException) { }
        }
    }

    private void CleanTemporary(string path)
    {
        if (!Directory.Exists(path)) return;
        try
        {
            // Recursive deletion is restricted to our exact sibling staging/backup tree,
            // after checking every file's ownership checksum and rejecting reparse points.
            if (Path.GetDirectoryName(Path.GetFullPath(path)) != parent || !Path.GetFileName(path).StartsWith(".dbmapper-", StringComparison.Ordinal))
                return;
            ValidateExisting(path);
            Directory.Delete(path, recursive: true);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or UsageException)
        {
            // Preserve unremovable or externally modified leftovers rather than risking user data.
        }
    }
}
