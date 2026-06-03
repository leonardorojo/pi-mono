using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Rufus.RCK.Workspace;

public sealed record RfsCompleteModelProfileResult(
    bool Success,
    string? ErrorMessage,
    string? RepoRoot,
    string? ConfigPath,
    string? ProfileName,
    RfsCompleteModelProfile? AppliedProfile);

public sealed record RfsCompleteModelProfile(
    string Name,
    string DefaultModel,
    string IntentModel,
    string TraceSliceProposalModel,
    string ConversationalMemoryModel,
    string PrincipalAnswerModel,
    string Description);

public static class RfsCompleteModelProfileStore
{
    private static readonly JsonSerializerOptions IndentedJsonOptions = new() { WriteIndented = true };
    private static readonly UTF8Encoding Utf8NoBom = new(false);

    internal static readonly RfsCompleteModelProfile[] Profiles =
    [
        new(
            Name: "test",
            DefaultModel: "deepseek-chat",
            IntentModel: "deepseek-chat",
            TraceSliceProposalModel: "deepseek-chat",
            ConversationalMemoryModel: "deepseek-chat",
            PrincipalAnswerModel: "deepseek-chat",
            Description: "Cheap/local-oriented profile for testing Complete mode"),
        new(
            Name: "balanced",
            DefaultModel: "gpt-5.4-mini",
            IntentModel: "claude-haiku-4.5",
            TraceSliceProposalModel: "gpt-5.4-mini",
            ConversationalMemoryModel: "gpt-5.4-mini",
            PrincipalAnswerModel: "gpt-5.4-mini",
            Description: "Balanced profile for normal Complete mode usage"),
    ];

    public static IReadOnlyList<RfsCompleteModelProfile> GetAvailableProfiles()
        => Profiles;

    public static string FormatProfileUsageChoices()
        => string.Join("|", Profiles.Select(profile => profile.Name));

    public static IReadOnlyList<string> GetAvailableProfileNames()
        => Profiles.Select(profile => profile.Name).ToArray();

    public static IReadOnlyList<string> GetCompleteProfileHelpLines()
    {
        var lines = new List<string>
        {
            "Usage:",
            $"  /complete-profile <{FormatProfileUsageChoices()}>",
            string.Empty,
            "Available profiles:",
        };

        var nameWidth = Profiles.Max(profile => profile.Name.Length);
        foreach (var profile in Profiles)
        {
            lines.Add($"  {profile.Name.PadRight(nameWidth)}  {profile.Description}");
        }

        lines.Add(string.Empty);
        lines.Add("Examples:");
        lines.Add("  /complete-profile test");
        lines.Add("  /complete-profile balanced");
        return lines;
    }

    public static IReadOnlyList<string> GetUnknownProfileHelpLines(string profileName)
    {
        var lines = new List<string>
        {
            $"Unknown Complete profile: {profileName}",
            string.Empty,
            "Available profiles:",
        };

        foreach (var name in GetAvailableProfileNames())
        {
            lines.Add($"  {name}");
        }

        lines.Add(string.Empty);
        lines.Add("Examples:");
        lines.Add("  /complete-profile test");
        lines.Add("  /complete-profile balanced");
        return lines;
    }

    public static RfsCompleteModelProfile? FindProfile(string name)
        => Profiles.FirstOrDefault(profile => string.Equals(profile.Name, name, StringComparison.Ordinal));

    internal static JsonObject BuildLlmConfig(RfsCompleteModelProfile profile)
    {
        return new JsonObject
        {
            ["defaultModel"] = profile.DefaultModel,
            ["stages"] = new JsonObject
            {
                ["intent"] = new JsonObject { ["model"] = profile.IntentModel },
                ["traceSliceProposal"] = new JsonObject { ["model"] = profile.TraceSliceProposalModel },
                ["conversationalMemory"] = new JsonObject { ["model"] = profile.ConversationalMemoryModel },
                ["principalAnswer"] = new JsonObject { ["model"] = profile.PrincipalAnswerModel },
            },
        };
    }

    public static RfsCompleteModelProfileResult SetCompleteProfile(string profileName, string? startingDirectory = null)
    {
        var trimmedName = profileName.Trim();
        if (string.IsNullOrWhiteSpace(trimmedName))
        {
            return new RfsCompleteModelProfileResult(false, "rfs complete-profile: missing profile name.", null, null, null, null);
        }

        var profile = FindProfile(trimmedName);
        if (profile is null)
        {
            return new RfsCompleteModelProfileResult(false, $"Unknown Complete profile: {trimmedName}", null, null, trimmedName, null);
        }

        var repoRoot = FindRepoRoot(startingDirectory ?? Directory.GetCurrentDirectory());
        if (repoRoot is null)
        {
            return new RfsCompleteModelProfileResult(false, "rfs complete-profile: repository root not found.", null, null, trimmedName, null);
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

            // Preserve top-level keys that already exist
            if (!config.ContainsKey("schemaVersion"))
            {
                config["schemaVersion"] = 1;
            }

            config["llm"] = BuildLlmConfig(profile);

            File.WriteAllText(paths.ConfigPath, JsonSerializer.Serialize(config, IndentedJsonOptions) + Environment.NewLine, Utf8NoBom);

            return new RfsCompleteModelProfileResult(true, null, repoRoot, paths.ConfigPath, trimmedName, profile);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException)
        {
            return new RfsCompleteModelProfileResult(
                false,
                $"rfs complete-profile: failed to update {paths.ConfigPath}: {ex.Message}",
                repoRoot,
                paths.ConfigPath,
                trimmedName,
                null);
        }
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
