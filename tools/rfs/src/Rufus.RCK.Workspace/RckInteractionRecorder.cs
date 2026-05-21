using System.Text;
using System.Text.Json;
using Rufus.RCK.Core.Hashing;
using Rufus.RCK.Core.Model;

namespace Rufus.RCK.Workspace;

public static class RckInteractionRecorder
{
    private static readonly JsonSerializerOptions IndentedJsonOptions = new() { WriteIndented = true };
    private static readonly UTF8Encoding Utf8NoBom = new(false);

    public static RckInteractionRecordResult RecordAsk(string prompt, string answer, string? startingDirectory = null)
        => Record(RckInteractionRecord.CreateAsk(prompt, answer), startingDirectory);

    public static RckInteractionRecordResult RecordAgent(
        string prompt,
        string answer,
        IEnumerable<RckInteractionTool>? tools = null,
        string? startingDirectory = null)
        => Record(RckInteractionRecord.CreateAgent(prompt, answer, tools), startingDirectory);

    public static RckInteractionRecordResult Record(RckInteractionRecord record, string? startingDirectory = null)
    {
        var repoRoot = FindRepoRoot(startingDirectory ?? Directory.GetCurrentDirectory());
        if (repoRoot is null)
        {
            return RckInteractionRecordResult.Failure("[rck] record: repository root not found.");
        }

        var createdBy = GetCreatedBy(record.Mode);
        var paths = new RckWorkspacePaths(repoRoot);
        if (!File.Exists(paths.HeadPath))
        {
            return RckInteractionRecordResult.Failure($"[rck] record: HEAD not found at {paths.HeadPath}; run rfs init first.");
        }

        var headStateId = ReadHeadStateId(paths.HeadPath);
        if (headStateId is null)
        {
            return RckInteractionRecordResult.Failure($"[rck] record: invalid HEAD file at {paths.HeadPath}.");
        }

        var previousStatePath = Path.Combine(paths.StatesDirectory, $"{headStateId}.json");
        if (!File.Exists(previousStatePath))
        {
            return RckInteractionRecordResult.Failure($"[rck] record: previous state not found: {previousStatePath}");
        }

        var previousState = LoadStoredState(previousStatePath);
        var currentGit = GitWorkspaceContext.Capture(repoRoot);
        var changedArtifacts = currentGit.ChangedArtifacts;
        var nextStatePayloadJson = BuildInteractionStatePayload(record, currentGit, changedArtifacts);
        var nextState = RckState.Create(
            nextStatePayloadJson,
            meta: new RckStateMeta(DateTimeOffset.UtcNow, createdBy, record.Mode, "recorded LLM interaction"));

        var interactionDeltaPayload = BuildInteractionDeltaPayload(previousState.Id, nextState.Id, record, changedArtifacts);

        var interactionOp = new PatchOp(
            PatchOpKind.Replace,
            "/interaction",
            JsonSerializer.Serialize(interactionDeltaPayload));

        var delta = RckDelta.Create(
            previousState.Id,
            nextState.Id,
            ops: new[] { interactionOp },
            meta: new RckDeltaMeta(DateTimeOffset.UtcNow, createdBy, record.Mode, "recorded LLM interaction delta"));

        var stateCreated = EnsureState(paths, nextState);
        var deltaCreated = EnsureDelta(paths, delta);

        File.WriteAllText(paths.HeadPath, nextState.Id + Environment.NewLine, Utf8NoBom);
        var headUpdated = true;

        var currentCommit = currentGit.Commit;
        var previousCommit = previousState.GitCommit;
        var anchorCreated = false;
        string? anchorLabel = null;
        RckAnchorId? anchorId = null;

        if (!string.IsNullOrWhiteSpace(currentCommit) && !string.Equals(previousCommit, currentCommit, StringComparison.Ordinal))
        {
            anchorLabel = $"git-commit:{GetShortCommit(currentCommit)}";
            var anchor = RckAnchor.Create(
                nextState.Id,
                meta: new RckAnchorMeta(DateTimeOffset.UtcNow, createdBy, anchorLabel, "detected new git commit during recorded interaction"));

            anchorCreated = EnsureAnchor(paths, anchor);
            anchorId = anchor.Id;
        }

        return RckInteractionRecordResult.SuccessResult(
            repoRoot,
            paths,
            stateCreated,
            deltaCreated,
            headUpdated,
            anchorCreated,
            anchorLabel,
            nextState.Id,
            delta.Id,
            anchorId);
    }

    private static bool EnsureState(RckWorkspacePaths paths, RckState state)
    {
        Directory.CreateDirectory(paths.StatesDirectory);
        var statePath = Path.Combine(paths.StatesDirectory, $"{state.Id}.json");
        if (File.Exists(statePath))
        {
            return false;
        }

        File.WriteAllText(statePath, SerializeStateEnvelope(state), Utf8NoBom);
        return true;
    }

    private static bool EnsureDelta(RckWorkspacePaths paths, RckDelta delta)
    {
        Directory.CreateDirectory(paths.DeltasDirectory);
        var deltaPath = Path.Combine(paths.DeltasDirectory, $"{delta.Id}.json");
        if (File.Exists(deltaPath))
        {
            return false;
        }

        File.WriteAllText(deltaPath, SerializeDeltaEnvelope(delta), Utf8NoBom);
        return true;
    }

    private static bool EnsureAnchor(RckWorkspacePaths paths, RckAnchor anchor)
    {
        Directory.CreateDirectory(paths.AnchorsDirectory);
        var anchorPath = Path.Combine(paths.AnchorsDirectory, $"{anchor.Id}.json");
        if (File.Exists(anchorPath))
        {
            return false;
        }

        File.WriteAllText(anchorPath, SerializeAnchorEnvelope(anchor), Utf8NoBom);
        return true;
    }

    private static string BuildInteractionStatePayload(
        RckInteractionRecord record,
        GitWorkspaceContext gitContext,
        IReadOnlyList<GitWorkspaceArtifactChange> artifacts)
    {
        var payload = new
        {
            type = "rufus.interaction-state",
            schemaVersion = 1,
            interaction = new
            {
                mode = record.Mode,
                prompt = record.Prompt,
                answerSummary = record.AnswerSummary,
            },
            git = new
            {
                branch = gitContext.Branch,
                commit = gitContext.Commit,
                dirty = gitContext.Dirty,
            },
            artifacts = artifacts.Select(SerializeArtifactChange).ToArray(),
        };

        return JsonSerializer.Serialize(payload);
    }

    private static object BuildInteractionDeltaPayload(
        RckStateId previousStateId,
        RckStateId nextStateId,
        RckInteractionRecord record,
        IReadOnlyList<GitWorkspaceArtifactChange> artifacts)
    {
        var changes = new List<object>
        {
            new
            {
                path = "/interaction",
                kind = "updated",
                summary = "Recorded a new LLM interaction.",
            },
            new
            {
                path = "/git",
                kind = "refreshed",
                summary = "Captured current Git context.",
            },
        };

        if (artifacts.Count > 0)
        {
            changes.Add(new
            {
                path = "/artifacts",
                kind = "updated",
                summary = "Detected changed workspace artifacts.",
            });
        }

        var tools = record.Mode == "agent"
            ? record.Tools.Select(tool => new
            {
                name = tool.Name,
                status = tool.Status,
            }).Cast<object>().ToArray()
            : Array.Empty<object>();

        if (record.Mode == "agent" && record.Tools.Count > 0)
        {
            changes.Add(new
            {
                path = "/tools",
                kind = "added",
                summary = "Captured tool calls used by the agent.",
            });
        }

        return new
        {
            type = "rufus.interaction-delta",
            schemaVersion = 1,
            change = new
            {
                summary = "Recorded a new LLM interaction.",
                fromStateId = previousStateId.ToString(),
                toStateId = nextStateId.ToString(),
                changes = changes.ToArray(),
            },
            cause = new
            {
                type = "llm-interaction",
                mode = record.Mode,
                prompt = record.Prompt,
                answer = record.Answer,
            },
            evidence = new
            {
                tools = tools,
                artifacts = artifacts.Select(SerializeArtifactChange).ToArray(),
            },
        };
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

    private static string SerializeDeltaEnvelope(RckDelta delta)
    {
        var envelope = new
        {
            schemaVersion = 1,
            type = "rufus.rck.delta",
            id = delta.Id.ToString(),
            fromStateId = delta.FromStateId.ToString(),
            toStateId = delta.ToStateId.ToString(),
            ops = delta.Ops.Select(op => new
            {
                kind = op.Kind.ToString(),
                path = op.Path,
                valueJson = op.ValueJson,
            }).ToArray(),
            refs = delta.Refs.Select(SerializeRckRef).ToArray(),
            evidenceRefs = delta.EvidenceRefs.Select(SerializeEvidenceRef).ToArray(),
            meta = new
            {
                createdAtUtc = delta.Meta.CreatedAtUtc,
                delta.Meta.CreatedBy,
                delta.Meta.Label,
                delta.Meta.Reason,
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

    private static object SerializeEvidenceRef(EvidenceRef evidenceRef)
    {
        return new
        {
            evidenceRef.Id,
            evidenceRef.Kind,
            @ref = SerializeRckRef(evidenceRef.Ref),
            evidenceRef.Summary,
            hash = evidenceRef.Hash?.Value,
        };
    }

    private static object SerializeArtifactChange(GitWorkspaceArtifactChange artifactChange)
    {
        return new
        {
            kind = artifactChange.Kind,
            path = artifactChange.Path,
            changeType = artifactChange.ChangeType,
            gitStatus = artifactChange.GitStatus,
            source = artifactChange.Source,
        };
    }

    private static StoredState LoadStoredState(string path)
    {
        var json = File.ReadAllText(path, Utf8NoBom);
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        var id = new RckStateId(new RckHash(GetRequiredString(root, "id", path)));
        var payloadCanonicalJson = GetRequiredString(root, "payloadCanonicalJson", path);
        var gitCommit = ExtractGitCommit(payloadCanonicalJson);

        return new StoredState(id, payloadCanonicalJson, gitCommit);
    }

    private static RckStateId? ReadHeadStateId(string headPath)
    {
        var headText = File.ReadAllText(headPath, Utf8NoBom).Trim();
        if (string.IsNullOrWhiteSpace(headText))
        {
            return null;
        }

        try
        {
            return new RckStateId(new RckHash(headText));
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    private static string? ExtractGitCommit(string payloadCanonicalJson)
    {
        using var payloadDocument = JsonDocument.Parse(payloadCanonicalJson);
        var root = payloadDocument.RootElement;

        if (!root.TryGetProperty("git", out var gitElement))
        {
            return null;
        }

        if (!gitElement.TryGetProperty("commit", out var commitElement))
        {
            return null;
        }

        return commitElement.ValueKind == JsonValueKind.String ? commitElement.GetString() : null;
    }

    private static string GetRequiredString(JsonElement element, string propertyName, string path)
    {
        if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.String)
        {
            throw new InvalidDataException($"Invalid RCK JSON at {path}: missing string property '{propertyName}'.");
        }

        return property.GetString() ?? string.Empty;
    }

    private static string GetShortCommit(string commit)
        => commit.Length <= 7 ? commit : commit[..7];

    private static string GetCreatedBy(string mode)
        => mode switch
        {
            "agent" => "rfs agent --record",
            _ => "rfs ask --record",
        };

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

    private sealed record StoredState(RckStateId Id, string PayloadCanonicalJson, string? GitCommit);
}
