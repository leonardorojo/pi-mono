using System.Text.Json;

namespace Rufus.RCK.Workspace;

public static class RckTraceSliceBuilder
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = null,
    };

    public static RckTraceSliceBuildResult Build(string prompt, string? startingDirectory = null, int maxStates = 5)
    {
        if (maxStates < 1)
        {
            maxStates = 1;
        }

        var contextPack = RckWorkspaceContextPackReader.Read(startingDirectory);
        if (!contextPack.Success)
        {
            return RckTraceSliceBuildResult.Failure(contextPack.ErrorMessage ?? "rfs trace-slice: failed to read RCK workspace state.");
        }

        if (string.IsNullOrWhiteSpace(contextPack.HeadStateId))
        {
            return RckTraceSliceBuildResult.Failure("rfs trace-slice: HEAD state not available.");
        }

        var activeEntries = contextPack.ActiveChain.Take(maxStates).ToArray();
        var stateLookup = contextPack.States.ToDictionary(state => state.Id, StringComparer.Ordinal);
        var deltaLookup = contextPack.Deltas.ToDictionary(delta => delta.Id, StringComparer.Ordinal);
        var anchorLookup = contextPack.Anchors.ToDictionary(anchor => anchor.Id, StringComparer.Ordinal);

        var stateIds = activeEntries.Select(entry => entry.StateId).ToArray();
        var deltaIds = activeEntries
            .Select(entry => entry.IncomingDeltaId)
            .Where(deltaId => !string.IsNullOrWhiteSpace(deltaId))
            .Select(deltaId => deltaId!)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var anchorIds = activeEntries
            .SelectMany(entry => entry.Anchors.Select(anchor => anchor.Id))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        var states = activeEntries
            .Select(entry => BuildStateSlice(stateLookup, entry.StateId))
            .Where(state => state is not null)
            .Select(state => state!)
            .ToArray();

        var deltas = deltaIds
            .Select(deltaId => BuildDeltaSlice(deltaLookup, deltaId))
            .Where(delta => delta is not null)
            .Select(delta => delta!)
            .ToArray();

        var anchors = anchorIds
            .Select(anchorId => BuildAnchorSlice(anchorLookup, anchorId))
            .Where(anchor => anchor is not null)
            .Select(anchor => anchor!)
            .ToArray();

        var artifacts = BuildArtifacts(contextPack, activeEntries, stateLookup);
        var document = new
        {
            type = "rufus.trace-slice",
            schemaVersion = 1,
            prompt = new
            {
                text = prompt,
                isExcerpt = false,
            },
            intent = new
            {
                kind = "trace-slice-request",
                summary = BuildIntentSummary(prompt),
                source = "deterministic",
            },
            selection = new
            {
                strategy = "active-chain-recent",
                maxStates = maxStates,
                headStateId = contextPack.HeadStateId,
                stateIds = stateIds,
                deltaIds = deltaIds,
                anchorIds = anchorIds,
            },
            artifacts = artifacts,
            materializationPolicy = new
            {
                includeStatePayloads = true,
                includeDeltaDecodedOps = true,
                includeArtifactContents = false,
                includeGitDiffs = false,
                includeStdoutStderr = false,
                includeJsonl = false,
            },
            states = states,
            deltas = deltas,
            anchors = anchors,
            notes = new[]
            {
                "Derived from the active chain starting at HEAD.",
                "TraceSlice v0 is deterministic and read-only.",
                "Semantic payloads are included; file contents and diffs are excluded.",
            },
            exclusions = new[]
            {
                "file contents",
                "git diffs",
                "stdout/stderr",
                "jsonl",
                "RCK writes",
                "TraceSliceAgent",
                "LLM calls",
            },
        };

        return RckTraceSliceBuildResult.SuccessResult(JsonSerializer.Serialize(document, JsonOptions));
    }

    private static object? BuildStateSlice(IReadOnlyDictionary<string, RckWorkspaceContextPackStateObject> stateLookup, string stateId)
    {
        if (!stateLookup.TryGetValue(stateId, out var state))
        {
            return null;
        }

        return new
        {
            type = "rufus.rck.state",
            id = state.Id,
            payloadType = GetPayloadType(state.PayloadDecoded),
            payload = state.PayloadDecoded,
            refs = state.Refs.Select(SerializeRef).ToArray(),
            meta = SerializeMeta(state.Meta),
        };
    }

    private static object? BuildDeltaSlice(IReadOnlyDictionary<string, RckWorkspaceContextPackDeltaObject> deltaLookup, string deltaId)
    {
        if (!deltaLookup.TryGetValue(deltaId, out var delta))
        {
            return null;
        }

        var decodedPayload = delta.Ops.Count > 0 ? delta.Ops[0].DecodedValueJson : default;
        return new
        {
            type = "rufus.rck.delta",
            id = delta.Id,
            fromStateId = delta.FromStateId,
            toStateId = delta.ToStateId,
            payloadType = GetPayloadType(decodedPayload),
            payload = decodedPayload,
            ops = delta.Ops.Select(op => new
            {
                kind = op.Kind,
                path = op.Path,
                decodedValue = op.DecodedValueJson,
            }).ToArray(),
            refs = delta.Refs.Select(SerializeRef).ToArray(),
            evidenceRefs = delta.EvidenceRefs.Select(SerializeEvidenceRef).ToArray(),
            meta = SerializeMeta(delta.Meta),
        };
    }

    private static object? BuildAnchorSlice(IReadOnlyDictionary<string, RckWorkspaceContextPackAnchorObject> anchorLookup, string anchorId)
    {
        if (!anchorLookup.TryGetValue(anchorId, out var anchor))
        {
            return null;
        }

        return new
        {
            id = anchor.Id,
            stateId = anchor.StateId,
            parentAnchorIds = anchor.ParentAnchorIds,
            createdAtUtc = anchor.Meta.CreatedAtUtc,
            label = anchor.Meta.Label,
            reason = anchor.Meta.Reason,
        };
    }

    private static IReadOnlyList<object> BuildArtifacts(
        RckWorkspaceContextPackResult contextPack,
        IReadOnlyList<RckWorkspaceContextPackActiveEntry> activeEntries,
        IReadOnlyDictionary<string, RckWorkspaceContextPackStateObject> stateLookup)
    {
        var artifacts = new List<object>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var changedArtifact in contextPack.ChangedArtifacts)
        {
            AddArtifact(
                artifacts,
                seen,
                changedArtifact.Path,
                changedArtifact.ChangeType,
                changedArtifact.Source);
        }

        foreach (var activeEntry in activeEntries)
        {
            if (!stateLookup.TryGetValue(activeEntry.StateId, out var state))
            {
                continue;
            }

            foreach (var artifact in ReadArtifactsFromPayload(state.PayloadDecoded))
            {
                AddArtifact(
                    artifacts,
                    seen,
                    artifact.Path,
                    artifact.ChangeType,
                    artifact.Source ?? "payload");
            }
        }

        return artifacts;
    }

    private static void AddArtifact(
        ICollection<object> artifacts,
        ISet<string> seen,
        string? path,
        string? changeType,
        string? source)
    {
        if (string.IsNullOrWhiteSpace(path) || ShouldExcludePath(path))
        {
            return;
        }

        var normalizedPath = NormalizePath(path);
        var key = $"{normalizedPath}\u001F{changeType ?? string.Empty}\u001F{source ?? string.Empty}";
        if (!seen.Add(key))
        {
            return;
        }

        artifacts.Add(new
        {
            path = normalizedPath,
            changeType = changeType ?? "unknown",
            source = source ?? "payload",
            includeMode = "metadata-only",
        });
    }

    private static IReadOnlyList<(string Path, string? ChangeType, string? Source)> ReadArtifactsFromPayload(JsonElement? payload)
    {
        if (payload is not { ValueKind: JsonValueKind.Object } root)
        {
            return Array.Empty<(string, string?, string?)>();
        }

        if (!root.TryGetProperty("artifacts", out var artifactsElement) || artifactsElement.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<(string, string?, string?)>();
        }

        var artifacts = new List<(string Path, string? ChangeType, string? Source)>();
        foreach (var item in artifactsElement.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var path = GetOptionalString(item, "path");
            if (string.IsNullOrWhiteSpace(path) || ShouldExcludePath(path))
            {
                continue;
            }

            artifacts.Add((
                Path: path,
                ChangeType: GetOptionalString(item, "changeType"),
                Source: GetOptionalString(item, "source")));
        }

        return artifacts;
    }

    private static string BuildIntentSummary(string prompt)
    {
        var normalized = NormalizeExcerpt(prompt, 120);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return "TraceSlice request.";
        }

        return normalized;
    }

    private static string NormalizeExcerpt(string value, int maxLength)
    {
        var normalized = string.Join(" ", value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).Trim();
        if (normalized.Length <= maxLength)
        {
            return normalized;
        }

        return maxLength <= 1 ? normalized[..1] : normalized[..(maxLength - 1)] + "…";
    }

    private static string? GetPayloadType(JsonElement? payload)
    {
        if (payload is not { ValueKind: JsonValueKind.Object } root)
        {
            return null;
        }

        return GetOptionalString(root, "type") ?? "unknown";
    }

    private static string? GetOptionalString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        return property.GetString();
    }

    private static object SerializeRef(RckWorkspaceContextPackRefObject reference)
    {
        return new
        {
            id = reference.Id,
            kind = reference.Kind,
            uri = reference.Uri,
            hash = reference.Hash,
            mediaType = reference.MediaType,
        };
    }

    private static object SerializeEvidenceRef(RckWorkspaceContextPackEvidenceRefObject evidenceRef)
    {
        return new
        {
            id = evidenceRef.Id,
            kind = evidenceRef.Kind,
            @ref = SerializeRef(evidenceRef.Ref),
            summary = evidenceRef.Summary,
            hash = evidenceRef.Hash,
        };
    }

    private static object SerializeMeta(RckWorkspaceContextPackMeta meta)
    {
        return new
        {
            createdAtUtc = meta.CreatedAtUtc,
            createdBy = meta.CreatedBy,
            label = meta.Label,
            reason = meta.Reason,
        };
    }

    private static bool ShouldExcludePath(string path)
    {
        var normalizedPath = NormalizePath(path);
        var segments = normalizedPath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        foreach (var segment in segments)
        {
            if (string.Equals(segment, ".rfs", StringComparison.Ordinal)
                || string.Equals(segment, "bin", StringComparison.Ordinal)
                || string.Equals(segment, "obj", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static string NormalizePath(string path)
    {
        return path.Replace('\\', '/').Trim();
    }
}

public sealed record RckTraceSliceBuildResult
{
    public RckTraceSliceBuildResult(bool success, string? errorMessage, string? json)
    {
        Success = success;
        ErrorMessage = errorMessage;
        Json = json;
    }

    public bool Success { get; }

    public string? ErrorMessage { get; }

    public string? Json { get; }

    public static RckTraceSliceBuildResult SuccessResult(string json) => new(true, null, json);

    public static RckTraceSliceBuildResult Failure(string errorMessage) => new(false, errorMessage, null);
}
