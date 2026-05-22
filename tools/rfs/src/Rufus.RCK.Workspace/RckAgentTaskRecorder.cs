using System.Text;
using System.Text.Json;
using Rufus.RCK.Core.Hashing;
using Rufus.RCK.Core.Model;

namespace Rufus.RCK.Workspace;

public static class RckAgentTaskRecorder
{
    private static readonly JsonSerializerOptions IndentedJsonOptions = new() { WriteIndented = true };
    private static readonly UTF8Encoding Utf8NoBom = new(false);

    public static RckAgentTaskRecordResult RecordIntent(RckAgentTaskRecordInput record, string? startingDirectory = null)
    {
        ArgumentNullException.ThrowIfNull(record);

        var repoRoot = FindRepoRoot(startingDirectory ?? Directory.GetCurrentDirectory());
        if (repoRoot is null)
        {
            return RckAgentTaskRecordResult.Failure("[rck] record: repository root not found.");
        }

        var paths = new RckWorkspacePaths(repoRoot);
        if (!File.Exists(paths.HeadPath))
        {
            return RckAgentTaskRecordResult.Failure($"[rck] record: HEAD not found at {paths.HeadPath}; run rfs init first.");
        }

        var headStateId = ReadHeadStateId(paths.HeadPath);
        if (headStateId is null)
        {
            return RckAgentTaskRecordResult.Failure($"[rck] record: invalid HEAD file at {paths.HeadPath}.");
        }

        var currentGit = GitWorkspaceContext.Capture(repoRoot);
        var changedArtifacts = currentGit.ChangedArtifacts;
        var artifactReferences = BuildArtifactReferences(repoRoot, changedArtifacts);

        var statePayloadJson = BuildIntentStatePayload(record, currentGit, changedArtifacts);
        var state = RckState.Create(
            statePayloadJson,
            refs: artifactReferences.Select(reference => reference.Ref),
            meta: new RckStateMeta(
                DateTimeOffset.UtcNow,
                createdBy: "rfs intent --record",
                label: "intent-task",
                reason: "recorded deterministic intent inference projection"));

        var deltaPayloadJson = BuildIntentDeltaPayload(headStateId, state.Id, record, currentGit, changedArtifacts);
        var delta = RckDelta.Create(
            headStateId,
            state.Id,
            ops: Array.Empty<PatchOp>(),
            refs: artifactReferences.Select(reference => reference.Ref),
            evidenceRefs: artifactReferences.Select(reference => reference.EvidenceRef),
            meta: new RckDeltaMeta(
                DateTimeOffset.UtcNow,
                createdBy: "rfs intent --record",
                label: "intent-task",
                reason: "recorded deterministic intent inference delta"));

        var stateCreated = EnsureState(paths, state);
        var deltaCreated = EnsureDelta(paths, delta, deltaPayloadJson);

        File.WriteAllText(paths.HeadPath, state.Id + Environment.NewLine, Utf8NoBom);

        return RckAgentTaskRecordResult.SuccessResult(
            repoRoot,
            paths,
            headStateId.ToString(),
            state.Id.ToString(),
            delta.Id.ToString(),
            stateCreated,
            deltaCreated,
            headUpdated: true);
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

    private static bool EnsureDelta(RckWorkspacePaths paths, RckDelta delta, string deltaPayloadJson)
    {
        Directory.CreateDirectory(paths.DeltasDirectory);
        var deltaPath = Path.Combine(paths.DeltasDirectory, $"{delta.Id}.json");
        if (File.Exists(deltaPath))
        {
            return false;
        }

        File.WriteAllText(deltaPath, SerializeDeltaEnvelope(delta, deltaPayloadJson), Utf8NoBom);
        return true;
    }

    private static string BuildIntentStatePayload(
        RckAgentTaskRecordInput record,
        GitWorkspaceContext gitContext,
        IReadOnlyList<GitWorkspaceArtifactChange> artifacts)
    {
        var payload = new
        {
            type = "rufus.agent-task-state",
            schemaVersion = 1,
            agentTask = new
            {
                taskId = record.TaskId,
                taskKind = record.TaskKind,
                agentId = record.AgentId,
                status = record.Status,
                summary = NormalizeSummary(record.TaskSummary, 240),
            },
            execution = new
            {
                provider = record.ExecutionProvider,
                model = record.ExecutionModel,
            },
            output = new
            {
                kind = record.OutputKind,
                summary = NormalizeSummary(record.OutputSummary, 240),
                data = new
                {
                    intent = record.OutputData.Intent,
                    entities = record.OutputData.Entities,
                    constraints = record.OutputData.Constraints,
                },
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

    private static string BuildIntentDeltaPayload(
        RckStateId fromStateId,
        RckStateId toStateId,
        RckAgentTaskRecordInput record,
        GitWorkspaceContext gitContext,
        IReadOnlyList<GitWorkspaceArtifactChange> artifacts)
    {
        var changeItems = new List<object>();
        if (artifacts.Count > 0)
        {
            changeItems.Add(new
            {
                path = "/artifacts",
                kind = "updated",
                summary = $"Detected {artifacts.Count} changed artifact{(artifacts.Count == 1 ? string.Empty : "s") }.",
            });
        }

        return JsonSerializer.Serialize(new
        {
            type = "rufus.agent-task-delta",
            schemaVersion = 1,
            change = new
            {
                summary = "Recorded a deterministic intent inference projection.",
                fromStateId = fromStateId.ToString(),
                toStateId = toStateId.ToString(),
                changes = changeItems.ToArray(),
            },
            cause = new
            {
                type = "agent-task",
                taskId = record.TaskId,
                taskKind = record.TaskKind,
                goal = NormalizeSummary(record.GoalSummary, 160),
                inputSummary = NormalizeSummary(record.InputSummary, 240),
                agentId = record.AgentId,
                executionModel = new
                {
                    provider = record.ExecutionProvider,
                    model = record.ExecutionModel,
                },
            },
            evidence = new
            {
                agent = new
                {
                    status = record.Status,
                    summary = NormalizeSummary(record.TaskSummary, 240),
                    warnings = NormalizeMessages(record.Warnings),
                    errors = NormalizeMessages(record.Errors),
                },
                items = Array.Empty<object>(),
            },
            git = new
            {
                branch = gitContext.Branch,
                commit = gitContext.Commit,
                dirty = gitContext.Dirty,
            },
            artifacts = artifacts.Select(SerializeArtifactChange).ToArray(),
        });
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

    private static string SerializeDeltaEnvelope(RckDelta delta, string deltaPayloadJson)
    {
        var envelope = new
        {
            schemaVersion = 1,
            type = "rufus.rck.delta",
            id = delta.Id.ToString(),
            fromStateId = delta.FromStateId.ToString(),
            toStateId = delta.ToStateId.ToString(),
            ops = new[]
            {
                new
                {
                    kind = "replace",
                    path = "/agent-task",
                    valueJson = deltaPayloadJson,
                },
            },
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

    private static IReadOnlyList<ArtifactReferencePair> BuildArtifactReferences(
        string repoRoot,
        IReadOnlyList<GitWorkspaceArtifactChange> artifacts)
    {
        if (artifacts.Count == 0)
        {
            return Array.Empty<ArtifactReferencePair>();
        }

        var references = new List<ArtifactReferencePair>(artifacts.Count);
        foreach (var artifact in artifacts)
        {
            var normalizedPath = artifact.Path.Replace('\\', '/');
            var absolutePath = Path.GetFullPath(Path.Combine(repoRoot, artifact.Path));
            var referenceId = $"file:{normalizedPath}";
            var reference = new RckRef(referenceId, artifact.Kind, new Uri(absolutePath, UriKind.Absolute));
            var evidence = new EvidenceRef(
                id: $"changed-artifact:{artifact.ChangeType}:{normalizedPath}",
                kind: "changed-artifact",
                @ref: reference,
                summary: $"{artifact.ChangeType} file detected by git-status ({artifact.GitStatus})");

            references.Add(new ArtifactReferencePair(reference, evidence));
        }

        return references;
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

    private static IReadOnlyList<string> NormalizeMessages(IEnumerable<string>? values)
    {
        if (values is null)
        {
            return Array.Empty<string>();
        }

        var results = new List<string>();
        foreach (var value in values)
        {
            var normalized = NormalizeSummary(value, 160);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                continue;
            }

            results.Add(normalized);
            if (results.Count == 5)
            {
                break;
            }
        }

        return results;
    }

    private static string NormalizeSummary(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized = string.Join(" ", value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        if (normalized.Length <= maxLength)
        {
            return normalized;
        }

        return maxLength <= 1 ? normalized[..1] : normalized[..(maxLength - 1)] + "…";
    }

    private static RckStateId? ReadHeadStateId(string headPath)
    {
        try
        {
            var headText = File.ReadAllText(headPath, Utf8NoBom).Trim();
            if (string.IsNullOrWhiteSpace(headText))
            {
                return null;
            }

            return new RckStateId(new RckHash(headText));
        }
        catch
        {
            return null;
        }
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

    private sealed record ArtifactReferencePair(RckRef Ref, EvidenceRef EvidenceRef);
}
