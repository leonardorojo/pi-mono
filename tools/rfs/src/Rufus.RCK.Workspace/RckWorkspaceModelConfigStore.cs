using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Rufus.RCK.Workspace;

public sealed record RckWorkspaceModelConfigReadResult(
    bool Success,
    string? ErrorMessage,
    string? RepoRoot,
    string? ConfigPath,
    bool ConfigExists,
    string? DefaultModel)
{
    public bool HasConfiguredDefaultModel => !string.IsNullOrWhiteSpace(DefaultModel);
}

public sealed record RckWorkspaceModelConfigWriteResult(
    bool Success,
    string? ErrorMessage,
    string? RepoRoot,
    string? ConfigPath,
    string? DefaultModel);

public static class RckWorkspaceModelConfigStore
{
    private static readonly JsonSerializerOptions IndentedJsonOptions = new() { WriteIndented = true };
    private static readonly UTF8Encoding Utf8NoBom = new(false);

    public static RckWorkspaceModelConfigReadResult Read(string? startingDirectory = null)
    {
        var repoRoot = FindRepoRoot(startingDirectory ?? Directory.GetCurrentDirectory());
        if (repoRoot is null)
        {
            return new RckWorkspaceModelConfigReadResult(
                false,
                "rfs model: repository root not found.",
                null,
                null,
                false,
                null);
        }

        var paths = new RckWorkspacePaths(repoRoot);
        if (!File.Exists(paths.ConfigPath))
        {
            return new RckWorkspaceModelConfigReadResult(true, null, repoRoot, paths.ConfigPath, false, null);
        }

        try
        {
            var config = ReadConfig(paths.ConfigPath);
            var defaultModel = ReadDefaultModel(config);
            return new RckWorkspaceModelConfigReadResult(true, null, repoRoot, paths.ConfigPath, true, defaultModel);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException)
        {
            return new RckWorkspaceModelConfigReadResult(
                false,
                $"rfs model: failed to read {paths.ConfigPath}: {ex.Message}",
                repoRoot,
                paths.ConfigPath,
                true,
                null);
        }
    }

    public static RckWorkspaceModelConfigWriteResult SetDefaultModel(string model, string? startingDirectory = null)
    {
        var trimmedModel = model.Trim();
        if (string.IsNullOrWhiteSpace(trimmedModel))
        {
            return new RckWorkspaceModelConfigWriteResult(false, "rfs model set: missing model.", null, null, null);
        }

        var repoRoot = FindRepoRoot(startingDirectory ?? Directory.GetCurrentDirectory());
        if (repoRoot is null)
        {
            return new RckWorkspaceModelConfigWriteResult(false, "rfs model set: repository root not found.", null, null, null);
        }

        var paths = new RckWorkspacePaths(repoRoot);
        try
        {
            Directory.CreateDirectory(paths.WorkspaceDirectory);

            JsonObject config;
            if (File.Exists(paths.ConfigPath))
            {
                config = ReadConfig(paths.ConfigPath);
            }
            else
            {
                config = new JsonObject();
            }

            if (!config.ContainsKey("schemaVersion"))
            {
                config["schemaVersion"] = 1;
            }

            var llm = config["llm"] as JsonObject ?? new JsonObject();
            llm["defaultModel"] = trimmedModel;
            config["llm"] = llm;

            File.WriteAllText(paths.ConfigPath, JsonSerializer.Serialize(config, IndentedJsonOptions) + Environment.NewLine, Utf8NoBom);

            return new RckWorkspaceModelConfigWriteResult(true, null, repoRoot, paths.ConfigPath, trimmedModel);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException)
        {
            return new RckWorkspaceModelConfigWriteResult(
                false,
                $"rfs model set: failed to update {paths.ConfigPath}: {ex.Message}",
                repoRoot,
                paths.ConfigPath,
                null);
        }
    }

    public static string? TryReadDefaultModel(string? startingDirectory = null)
    {
        var result = Read(startingDirectory);
        return result.Success ? result.DefaultModel : null;
    }

    private static JsonObject ReadConfig(string configPath)
    {
        var node = JsonNode.Parse(File.ReadAllText(configPath))
            ?? throw new JsonException("Config file is empty.");

        if (node is not JsonObject config)
        {
            throw new JsonException("Config file must contain a JSON object.");
        }

        return config;
    }

    private static string? ReadDefaultModel(JsonObject config)
    {
        if (!config.TryGetPropertyValue("llm", out var llmNode) || llmNode is not JsonObject llm)
        {
            return null;
        }

        if (!llm.TryGetPropertyValue("defaultModel", out var defaultModelNode) || defaultModelNode is not JsonValue defaultModelValue)
        {
            return null;
        }

        if (!defaultModelValue.TryGetValue<string>(out var defaultModel))
        {
            return null;
        }

        return string.IsNullOrWhiteSpace(defaultModel) ? null : defaultModel.Trim();
    }

    private static string? FindRepoRoot(string startingDirectory)
    {
        var current = new DirectoryInfo(startingDirectory);

        while (current is not null)
        {
            var gitEntry = Path.Combine(current.FullName, ".git");
            if (Directory.Exists(gitEntry) || File.Exists(gitEntry))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        return null;
    }
}
