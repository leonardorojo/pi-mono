using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Rufus.RCK.Core.Model;

namespace Rufus.RCK.Workspace;

public static class RckWorkspaceInitializer
{
    private static readonly JsonSerializerOptions IndentedJsonOptions = new() { WriteIndented = true };
    private static readonly UTF8Encoding Utf8NoBom = new(false);

    public static RckWorkspaceInitResult Initialize(string? startingDirectory = null)
    {
        try
        {
            var repoRoot = FindRepoRoot(startingDirectory ?? Directory.GetCurrentDirectory());
            if (repoRoot is null)
            {
                return RckWorkspaceInitResult.Failure("rfs init: repository root not found.");
            }

            var paths = new RckWorkspacePaths(repoRoot);

            var configCreated = EnsureConfig(paths);
            var rckDirectoriesCreated = EnsureRckDirectories(paths);

            var gitContext = GitWorkspaceContext.Capture(repoRoot);
            var workspaceName = Path.GetFileName(Path.TrimEndingDirectorySeparator(repoRoot));
            var state = BuildGenesisState(repoRoot, workspaceName, gitContext);
            var stateCreated = EnsureGenesisState(paths, state);
            var headCreated = EnsureHead(paths, state.Id);

            var anchor = BuildGenesisAnchor(state);
            var anchorCreated = RckWorkspaceAnchorWriter.EnsureAnchor(paths, anchor);

            return RckWorkspaceInitResult.SuccessResult(
                repoRoot,
                paths,
                configCreated,
                rckDirectoriesCreated,
                headCreated,
                stateCreated,
                anchorCreated,
                state.Id,
                anchor.Id);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException)
        {
            return RckWorkspaceInitResult.Failure($"rfs init: failed to initialize workspace: {ex.Message}");
        }
    }

    private static bool EnsureConfig(RckWorkspacePaths paths)
    {
        Directory.CreateDirectory(paths.WorkspaceDirectory);

        var balancedProfile = RfsCompleteModelProfileStore.FindProfile("balanced")
            ?? throw new InvalidOperationException("Unknown default Complete profile: balanced.");

        if (!File.Exists(paths.ConfigPath))
        {
            WriteCompleteProfileConfig(paths.ConfigPath, BuildWorkspaceConfig(balancedProfile));
            return true;
        }

        var config = ReadConfig(paths.ConfigPath);
        if (!TryUpgradeLegacyInitConfig(config, balancedProfile))
        {
            return false;
        }

        WriteCompleteProfileConfig(paths.ConfigPath, config);
        return true;
    }

    private static JsonObject BuildWorkspaceConfig(RfsCompleteModelProfile profile)
    {
        return new JsonObject
        {
            ["schemaVersion"] = 1,
            ["type"] = "rufus.workspace",
            ["createdBy"] = "rfs init",
            ["llm"] = RfsCompleteModelProfileStore.BuildLlmConfig(profile),
        };
    }

    private static bool TryUpgradeLegacyInitConfig(JsonObject config, RfsCompleteModelProfile balancedProfile)
    {
        if (!IsLegacyInitGeneratedConfig(config))
        {
            return false;
        }

        var existingLlm = config["llm"] as JsonObject;
        var balancedLlm = RfsCompleteModelProfileStore.BuildLlmConfig(balancedProfile);

        if (existingLlm is null)
        {
            config["llm"] = balancedLlm;
            return true;
        }

        if (!existingLlm.ContainsKey("defaultModel") && balancedLlm["defaultModel"] is not null)
        {
            existingLlm["defaultModel"] = balancedLlm["defaultModel"]!.DeepClone();
        }

        if (!existingLlm.ContainsKey("stages") && balancedLlm["stages"] is not null)
        {
            existingLlm["stages"] = balancedLlm["stages"]!.DeepClone();
        }

        return true;
    }

    private static bool IsLegacyInitGeneratedConfig(JsonObject config)
    {
        return string.Equals(ReadStringProperty(config, "type"), "rufus.workspace", StringComparison.Ordinal)
            && string.Equals(ReadStringProperty(config, "createdBy"), "rfs init", StringComparison.Ordinal)
            && (!config.TryGetPropertyValue("llm", out var llmNode) || llmNode is not JsonObject llm || !llm.ContainsKey("stages"));
    }

    private static string? ReadStringProperty(JsonObject config, string propertyName)
    {
        if (!config.TryGetPropertyValue(propertyName, out var propertyNode) || propertyNode is not JsonValue propertyValue)
        {
            return null;
        }

        return propertyValue.TryGetValue<string>(out var value) ? value?.Trim() : null;
    }

    private static void WriteCompleteProfileConfig(string configPath, JsonObject config)
    {
        File.WriteAllText(configPath, JsonSerializer.Serialize(config, IndentedJsonOptions) + Environment.NewLine, Utf8NoBom);
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

    private static bool EnsureRckDirectories(RckWorkspacePaths paths)
    {
        var created = false;

        if (!Directory.Exists(paths.WorkspaceDirectory))
        {
            Directory.CreateDirectory(paths.WorkspaceDirectory);
            created = true;
        }

        if (!Directory.Exists(paths.RckDirectory))
        {
            Directory.CreateDirectory(paths.RckDirectory);
            created = true;
        }

        if (!Directory.Exists(paths.StatesDirectory))
        {
            Directory.CreateDirectory(paths.StatesDirectory);
            created = true;
        }

        if (!Directory.Exists(paths.DeltasDirectory))
        {
            Directory.CreateDirectory(paths.DeltasDirectory);
            created = true;
        }

        if (!Directory.Exists(paths.AnchorsDirectory))
        {
            Directory.CreateDirectory(paths.AnchorsDirectory);
            created = true;
        }

        return created;
    }

    private static bool EnsureHead(RckWorkspacePaths paths, RckStateId genesisStateId)
    {
        if (File.Exists(paths.HeadPath))
        {
            return false;
        }

        File.WriteAllText(paths.HeadPath, genesisStateId + Environment.NewLine, Utf8NoBom);
        return true;
    }

    private static bool EnsureGenesisState(RckWorkspacePaths paths, RckState state)
    {
        var statePath = Path.Combine(paths.StatesDirectory, $"{state.Id}.json");
        if (File.Exists(statePath))
        {
            return false;
        }

        File.WriteAllText(statePath, SerializeStateEnvelope(state), Utf8NoBom);
        return true;
    }


    private static RckState BuildGenesisState(string repoRoot, string workspaceName, GitWorkspaceContext gitContext)
    {
        var statePayload = BuildInitialStatePayload(repoRoot, workspaceName, gitContext);
        return RckState.Create(
            statePayload,
            meta: new RckStateMeta(DateTimeOffset.UtcNow, "rfs init", "genesis", "initial rfs workspace state"));
    }

    private static RckAnchor BuildGenesisAnchor(RckState state)
    {
        return RckAnchor.Create(
            state.Id,
            meta: new RckAnchorMeta(DateTimeOffset.UtcNow, "rfs init", "genesis", "initial rfs workspace anchor"));
    }

    private static string BuildInitialStatePayload(string repoRoot, string workspaceName, GitWorkspaceContext gitContext)
    {
        var payload = new
        {
            type = "rufus.initial-state",
            schemaVersion = 1,
            workspace = new
            {
                type = "rufus.workspace",
                root = repoRoot,
                name = workspaceName,
            },
            git = new
            {
                branch = gitContext.Branch,
                commit = gitContext.Commit,
                dirty = gitContext.Dirty,
            },
            rfs = new
            {
                initializedBy = "rfs init",
            },
        };

        return JsonSerializer.Serialize(payload);
    }

    private static string SerializeStateEnvelope(RckState state)
    {
        var envelope = new
        {
            schemaVersion = 1,
            type = "rufus.rck.state",
            id = state.Id.ToString(),
            payloadCanonicalJson = state.PayloadCanonicalJson,
            refs = state.Refs.Select(SerializeRckRef).ToArray(),
            meta = new
            {
                createdAtUtc = state.Meta.CreatedAtUtc,
                state.Meta.CreatedBy,
                state.Meta.Label,
                state.Meta.Reason,
            },
        };

        return JsonSerializer.Serialize(envelope, IndentedJsonOptions);
    }


    private static object SerializeRckRef(RckRef rckRef)
    {
        return new
        {
            rckRef.Id,
            rckRef.Kind,
            uri = rckRef.Uri.ToString(),
            hash = rckRef.Hash?.Value,
            rckRef.MediaType,
            meta = rckRef.Meta is null
                ? null
                : new
                {
                    createdAtUtc = rckRef.Meta.CreatedAtUtc,
                    rckRef.Meta.CreatedBy,
                    rckRef.Meta.Label,
                    rckRef.Meta.Reason,
                },
        };
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
