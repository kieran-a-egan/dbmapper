using System.Xml;
using System.Xml.Linq;
using Microsoft.Extensions.Configuration;

namespace DbMapper;

internal static class ProjectSecrets
{
    public static string Read(Options options)
    {
        var id = options.SecretsId ?? FindId(options.Project);
        if (id is "." or ".." || id.IndexOfAny(['/', '\\']) >= 0 || id.Any(char.IsControl))
            throw new UsageException("The user-secrets ID must be a store identifier, not a path.");
        var configuration = new ConfigurationBuilder().AddUserSecrets(id, reloadOnChange: false).Build();
        using var lifetime = configuration as IDisposable;
        return Select(configuration, options.SecretKey);
    }

    internal static string Select(IConfiguration configuration, string? key)
    {
        if (key is null)
        {
            var connections = configuration.GetSection("ConnectionStrings").GetChildren()
                .Where(c => !string.IsNullOrWhiteSpace(c.Value)).ToList();
            if (connections.Count != 1)
                throw new UsageException("User secrets must contain exactly one ConnectionStrings entry, or select one with --connection or --secret-key.");
            key = connections[0].Path;
        }
        var value = configuration[key];
        if (string.IsNullOrWhiteSpace(value))
            throw new UsageException("The selected user-secret key is missing or empty. No other configuration sources are used.");
        return value;
    }

    internal static string FindId(string? project)
    {
        var path = Path.GetFullPath(project ?? Directory.GetCurrentDirectory());
        if (Directory.Exists(path))
        {
            var projects = Directory.GetFiles(path, "*.csproj", SearchOption.TopDirectoryOnly);
            if (projects.Length != 1)
                throw new UsageException("The project directory must contain exactly one .csproj. Select a project with --project.");
            path = projects[0];
        }
        if (!File.Exists(path) || !path.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
            throw new UsageException("--project must identify an existing .csproj or its directory.");
        using var reader = XmlReader.Create(path, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null });
        var document = XDocument.Load(reader);
        var ids = document.Descendants().Where(e => e.Name.LocalName == "UserSecretsId").ToList();
        if (ids.Count != 1 || ids[0].AncestorsAndSelf().Any(e => e.Attribute("Condition") is not null))
            throw new UsageException("A single unconditional UserSecretsId is required in the project. For inherited/conditional settings, use --user-secrets-id.");
        var id = ids[0].Value.Trim();
        if (string.IsNullOrEmpty(id) || id.Contains("$(", StringComparison.Ordinal) || id.Contains("@(", StringComparison.Ordinal))
            throw new UsageException("UserSecretsId must be a literal value. Supply --user-secrets-id for evaluated settings.");
        return id;
    }
}
