using System.Text;
using System.Text.Json;
using Rufus.RCK.Core.Model;

namespace Rufus.RCK.Workspace;

public static class RckWorkspaceInitializer
{
    private static readonly JsonSerializerOptions IndentedJsonOptions = new() { WriteIndented = true };
    private static readonly UTF8Encoding Utf8NoBom = new(false);

    public static RckWorkspaceInitResult Initialize(string? startingDirectory = null)
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

        var anchor = BuildGenesisAnchor(state);
        var anchorCreated = EnsureGenesisAnchor(paths, anchor);

        return RckWorkspaceInitResult.SuccessResult(
            repoRoot,
            paths,
            configCreated,
            rckDirectoriesCreated,
            stateCreated,
            anchorCreated,
            state.Id,
            anchor.Id);
    }

    private static bool EnsureConfig(RckWorkspacePaths paths)
    {
        if (File.Exists(paths.ConfigPath))
        {
            return false;
        }

        Directory.CreateDirectory(paths.WorkspaceDirectory);
        var configContent = "{\n  \"schemaVersion\": 1,\n  \"type\": \"rufus.workspace\",\n  \"createdBy\": \"rfs init\"\n}\n";
        File.WriteAllText(paths.ConfigPath, configContent, Utf8NoBom);
        return true;
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

    private static bool EnsureGenesisAnchor(RckWorkspacePaths paths, RckAnchor anchor)
    {
        var anchorPath = Path.Combine(paths.AnchorsDirectory, $"{anchor.Id}.json");
        if (File.Exists(anchorPath))
        {
            return false;
        }

        File.WriteAllText(anchorPath, SerializeAnchorEnvelope(anchor), Utf8NoBom);
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

    private static string SerializeAnchorEnvelope(RckAnchor anchor)
    {
        var envelope = new
        {
            schemaVersion = 1,
            type = "rufus.rck.anchor",
            id = anchor.Id.ToString(),
            stateId = anchor.StateId.ToString(),
            parentAnchorIds = anchor.ParentAnchorIds.Select(parent => parent.ToString()).ToArray(),
            meta = new
            {
                createdAtUtc = anchor.Meta.CreatedAtUtc,
                anchor.Meta.CreatedBy,
                anchor.Meta.Label,
                anchor.Meta.Reason,
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
