using System.Text.Json;

namespace Rufus.RCK.Workspace;

public static class RckTraceSliceProposalValidator
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = null,
    };

    public static RckTraceSliceProposalValidationResult Validate(
        string proposalJson,
        string? startingDirectory = null,
        int maxStates = 5,
        int maxDeltas = 5)
    {
        if (string.IsNullOrWhiteSpace(proposalJson))
        {
            return RckTraceSliceProposalValidationResult.Failure("rfs trace-slice-validate: proposal JSON is empty.");
        }

        if (maxStates < 1)
        {
            maxStates = 1;
        }

        if (maxDeltas < 1)
        {
            maxDeltas = 1;
        }

        var contextPack = RckWorkspaceContextPackReader.Read(startingDirectory);
        if (!contextPack.Success)
        {
            return RckTraceSliceProposalValidationResult.Failure(
                contextPack.ErrorMessage ?? "rfs trace-slice-validate: failed to read RCK workspace state.");
        }

        if (string.IsNullOrWhiteSpace(contextPack.HeadStateId))
        {
            return RckTraceSliceProposalValidationResult.Failure("rfs trace-slice-validate: HEAD state not available.");
        }

        try
        {
            using var document = JsonDocument.Parse(proposalJson);
            var root = document.RootElement;

            var proposalType = GetRequiredString(root, "type");
            if (!string.Equals(proposalType, "rufus.trace-slice-proposal", StringComparison.Ordinal))
            {
                throw new InvalidDataException($"Expected proposal type 'rufus.trace-slice-proposal', received '{proposalType}'.");
            }

            var schemaVersion = GetRequiredInt32(root, "schemaVersion");
            if (schemaVersion != 1)
            {
                throw new InvalidDataException($"Expected schemaVersion 1, received {schemaVersion}.");
            }

            var promptElement = GetRequiredObject(root, "prompt");
            var intentElement = GetRequiredObject(root, "intent");
            var selectionElement = GetRequiredObject(root, "requestedSelection");
            var policyElement = GetRequiredObject(root, "requestedMaterializationPolicy");

            var promptText = GetRequiredString(promptElement, "text");
            var promptIsExcerpt = GetOptionalBoolean(promptElement, "isExcerpt");
            var intentKind = GetRequiredString(intentElement, "kind");
            var intentSummary = GetRequiredString(intentElement, "summary");
            var intentSource = GetRequiredString(intentElement, "source");

            var requestedStateIds = ReadDistinctStringArray(selectionElement, "stateIds");
            var requestedDeltaIds = ReadDistinctStringArray(selectionElement, "deltaIds");
            var requestedAnchorIds = ReadDistinctStringArray(selectionElement, "anchorIds");
            var requestedArtifactRefs = ReadDistinctStringArray(selectionElement, "artifactRefs");
            var proposalWarnings = ReadDistinctStringArray(root, "warnings");

            var stateLookup = contextPack.States.ToDictionary(state => state.Id, StringComparer.Ordinal);
            var deltaLookup = contextPack.Deltas.ToDictionary(delta => delta.Id, StringComparer.Ordinal);
            var anchorLookup = contextPack.Anchors.ToDictionary(anchor => anchor.Id, StringComparer.Ordinal);
            var knownArtifacts = BuildKnownArtifactLookup(contextPack);

            var accepted = new List<RckTraceSliceValidationItem>();
            var rejected = new List<RckTraceSliceValidationItem>();
            var downgraded = new List<RckTraceSliceValidationItem>();
            var reasons = new List<string>();

            var acceptedStateIds = ValidateIds(
                requestedStateIds,
                maxStates,
                targetPrefix: "state:",
                exceedsReason: $"Rejected because the proposal exceeded maxStates={maxStates}.",
                missingReasonFactory: id => $"Rejected because state '{id}' does not exist in the DAG.",
                acceptedReasonFactory: id => $"Accepted because state '{id}' exists in the DAG and stays within maxStates={maxStates}.",
                exists: id => stateLookup.ContainsKey(id),
                accepted,
                rejected,
                reasons);

            var acceptedDeltaIds = ValidateIds(
                requestedDeltaIds,
                maxDeltas,
                targetPrefix: "delta:",
                exceedsReason: $"Rejected because the proposal exceeded maxDeltas={maxDeltas}.",
                missingReasonFactory: id => $"Rejected because delta '{id}' does not exist in the DAG.",
                acceptedReasonFactory: id => $"Accepted because delta '{id}' exists in the DAG and stays within maxDeltas={maxDeltas}.",
                exists: id => deltaLookup.ContainsKey(id),
                accepted,
                rejected,
                reasons);

            var acceptedAnchorIds = new List<string>();
            foreach (var anchorId in requestedAnchorIds)
            {
                if (!anchorLookup.ContainsKey(anchorId))
                {
                    var reason = $"Rejected because anchor '{anchorId}' does not exist in the DAG.";
                    rejected.Add(new RckTraceSliceValidationItem($"anchor:{anchorId}", reason));
                    reasons.Add(reason);
                    continue;
                }

                acceptedAnchorIds.Add(anchorId);
                accepted.Add(new RckTraceSliceValidationItem(
                    $"anchor:{anchorId}",
                    $"Accepted as metadata because anchor '{anchorId}' exists in the DAG."));
            }

            var acceptedRequestedArtifactPaths = new List<string>();
            foreach (var artifactRef in requestedArtifactRefs)
            {
                var normalizedPath = NormalizePath(artifactRef);
                if (string.IsNullOrWhiteSpace(normalizedPath))
                {
                    continue;
                }

                if (ShouldExcludePath(normalizedPath))
                {
                    var reason = $"Rejected artifact '{normalizedPath}' because .rfs, bin, and obj paths are excluded from TraceSlice materialization.";
                    rejected.Add(new RckTraceSliceValidationItem($"artifact:{normalizedPath}", reason));
                    reasons.Add(reason);
                    continue;
                }

                if (!knownArtifacts.ContainsKey(normalizedPath))
                {
                    var reason = $"Rejected artifact '{normalizedPath}' because it is not available in the known metadata-only artifact set.";
                    rejected.Add(new RckTraceSliceValidationItem($"artifact:{normalizedPath}", reason));
                    reasons.Add(reason);
                    continue;
                }

                acceptedRequestedArtifactPaths.Add(normalizedPath);
                accepted.Add(new RckTraceSliceValidationItem(
                    $"artifact:{normalizedPath}",
                    $"Accepted as metadata-only artifact '{normalizedPath}'."));
            }

            var includeStatePayloads = GetOptionalBoolean(policyElement, "includeStatePayloads");
            var includeDeltaDecodedOps = GetOptionalBoolean(policyElement, "includeDeltaDecodedOps");
            var includeArtifactContents = GetOptionalBoolean(policyElement, "includeArtifactContents");
            var includeGitDiffs = GetOptionalBoolean(policyElement, "includeGitDiffs");
            var includeStdoutStderr = GetOptionalBoolean(policyElement, "includeStdoutStderr");
            var includeJsonl = GetOptionalBoolean(policyElement, "includeJsonl");

            var validatedMaterializationPolicy = new
            {
                includeStatePayloads,
                includeDeltaDecodedOps,
                includeArtifactContents = false,
                includeGitDiffs = false,
                includeStdoutStderr = false,
                includeJsonl = false,
            };

            RegisterMaterializationDecision(
                requestedValue: includeStatePayloads,
                validatedValue: includeStatePayloads,
                propertyName: "includeStatePayloads",
                allowedReason: "Accepted because TraceSlice v0 allows includeStatePayloads.",
                downgradeReason: null,
                accepted,
                downgraded,
                reasons);

            RegisterMaterializationDecision(
                requestedValue: includeDeltaDecodedOps,
                validatedValue: includeDeltaDecodedOps,
                propertyName: "includeDeltaDecodedOps",
                allowedReason: "Accepted because TraceSlice v0 allows includeDeltaDecodedOps.",
                downgradeReason: null,
                accepted,
                downgraded,
                reasons);

            RegisterMaterializationDecision(
                requestedValue: includeArtifactContents,
                validatedValue: false,
                propertyName: "includeArtifactContents",
                allowedReason: "TraceSlice v0 keeps includeArtifactContents disabled.",
                downgradeReason: "Downgraded includeArtifactContents to false because validated TraceSlice output is metadata-only.",
                accepted,
                downgraded,
                reasons);

            RegisterMaterializationDecision(
                requestedValue: includeGitDiffs,
                validatedValue: false,
                propertyName: "includeGitDiffs",
                allowedReason: "TraceSlice v0 keeps includeGitDiffs disabled.",
                downgradeReason: "Downgraded includeGitDiffs to false because validated TraceSlice output excludes diffs.",
                accepted,
                downgraded,
                reasons);

            RegisterMaterializationDecision(
                requestedValue: includeStdoutStderr,
                validatedValue: false,
                propertyName: "includeStdoutStderr",
                allowedReason: "TraceSlice v0 keeps includeStdoutStderr disabled.",
                downgradeReason: "Downgraded includeStdoutStderr to false because validated TraceSlice output excludes raw stdout/stderr.",
                accepted,
                downgraded,
                reasons);

            RegisterMaterializationDecision(
                requestedValue: includeJsonl,
                validatedValue: false,
                propertyName: "includeJsonl",
                allowedReason: "TraceSlice v0 keeps includeJsonl disabled.",
                downgradeReason: "Downgraded includeJsonl to false because validated TraceSlice output excludes raw JSONL.",
                accepted,
                downgraded,
                reasons);

            var states = acceptedStateIds
                .Select(stateId => BuildStateSlice(stateLookup, stateId, includeStatePayloads))
                .Where(state => state is not null)
                .Select(state => state!)
                .ToArray();

            var deltas = acceptedDeltaIds
                .Select(deltaId => BuildDeltaSlice(deltaLookup, deltaId, includeDeltaDecodedOps))
                .Where(delta => delta is not null)
                .Select(delta => delta!)
                .ToArray();

            var anchors = acceptedAnchorIds
                .Select(anchorId => BuildAnchorSlice(anchorLookup, anchorId))
                .Where(anchor => anchor is not null)
                .Select(anchor => anchor!)
                .ToArray();

            var artifacts = BuildArtifacts(contextPack, stateLookup, acceptedStateIds, acceptedRequestedArtifactPaths, knownArtifacts)
                .ToArray();

            var usableSelection = acceptedStateIds.Count > 0 || acceptedDeltaIds.Count > 0 || acceptedAnchorIds.Count > 0;
            if (!usableSelection)
            {
                reasons.Add("Proposal did not yield a usable validated selection.");
            }

            var validationStatus = !usableSelection
                ? "rejected"
                : rejected.Count == 0 && downgraded.Count == 0
                    ? "accepted"
                    : "partial";

            var notes = new List<string>
            {
                "Validated from deterministic TraceSliceProposal output.",
                "TraceSliceProposal validation is read-only and does not write .rfs/rck.",
                "Artifacts remain metadata-only; file contents, diffs, stdout/stderr, and JSONL are excluded.",
            };
            notes.AddRange(proposalWarnings.Select(warning => $"Planner warning: {warning}"));

            var documentObject = new
            {
                type = "rufus.trace-slice",
                schemaVersion = 1,
                prompt = new
                {
                    text = promptText,
                    isExcerpt = promptIsExcerpt,
                },
                intent = new
                {
                    kind = intentKind,
                    summary = intentSummary,
                    source = intentSource,
                },
                selection = new
                {
                    strategy = "proposal-validated",
                    maxStates,
                    headStateId = contextPack.HeadStateId,
                    stateIds = acceptedStateIds.ToArray(),
                    deltaIds = acceptedDeltaIds.ToArray(),
                    anchorIds = acceptedAnchorIds.ToArray(),
                },
                artifacts,
                materializationPolicy = validatedMaterializationPolicy,
                states,
                deltas,
                anchors,
                validation = new
                {
                    status = validationStatus,
                    accepted = accepted.Select(item => new { target = item.Target, reason = item.Reason }).ToArray(),
                    rejected = rejected.Select(item => new { target = item.Target, reason = item.Reason }).ToArray(),
                    downgraded = downgraded.Select(item => new { target = item.Target, reason = item.Reason }).ToArray(),
                    reasons = reasons.Distinct(StringComparer.Ordinal).ToArray(),
                },
                notes = notes.ToArray(),
                exclusions = new[]
                {
                    "file contents",
                    "git diffs",
                    "stdout/stderr",
                    "jsonl",
                    "RCK writes",
                    "HEAD mutation",
                    "TraceSliceProposal authority escalation",
                    "LLM calls",
                },
            };

            return RckTraceSliceProposalValidationResult.SuccessResult(JsonSerializer.Serialize(documentObject, JsonOptions));
        }
        catch (Exception ex)
        {
            return RckTraceSliceProposalValidationResult.Failure($"rfs trace-slice-validate: failed to validate proposal: {ex.Message}");
        }
    }

    private static List<string> ValidateIds(
        IReadOnlyList<string> requestedIds,
        int maxCount,
        string targetPrefix,
        string exceedsReason,
        Func<string, string> missingReasonFactory,
        Func<string, string> acceptedReasonFactory,
        Func<string, bool> exists,
        ICollection<RckTraceSliceValidationItem> accepted,
        ICollection<RckTraceSliceValidationItem> rejected,
        ICollection<string> reasons)
    {
        var acceptedIds = new List<string>();
        foreach (var id in requestedIds)
        {
            if (!exists(id))
            {
                var reason = missingReasonFactory(id);
                rejected.Add(new RckTraceSliceValidationItem($"{targetPrefix}{id}", reason));
                reasons.Add(reason);
                continue;
            }

            if (acceptedIds.Count >= maxCount)
            {
                rejected.Add(new RckTraceSliceValidationItem($"{targetPrefix}{id}", exceedsReason));
                reasons.Add(exceedsReason);
                continue;
            }

            acceptedIds.Add(id);
            accepted.Add(new RckTraceSliceValidationItem($"{targetPrefix}{id}", acceptedReasonFactory(id)));
        }

        return acceptedIds;
    }

    private static void RegisterMaterializationDecision(
        bool requestedValue,
        bool validatedValue,
        string propertyName,
        string allowedReason,
        string? downgradeReason,
        ICollection<RckTraceSliceValidationItem> accepted,
        ICollection<RckTraceSliceValidationItem> downgraded,
        ICollection<string> reasons)
    {
        var target = $"materializationPolicy.{propertyName}";
        if (requestedValue == validatedValue)
        {
            accepted.Add(new RckTraceSliceValidationItem(target, allowedReason));
            return;
        }

        if (!string.IsNullOrWhiteSpace(downgradeReason))
        {
            downgraded.Add(new RckTraceSliceValidationItem(target, downgradeReason));
            reasons.Add(downgradeReason);
        }
    }

    private static object? BuildStateSlice(
        IReadOnlyDictionary<string, RckWorkspaceContextPackStateObject> stateLookup,
        string stateId,
        bool includeStatePayloads)
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
            payload = includeStatePayloads ? state.PayloadDecoded : null,
            refs = state.Refs.Select(SerializeRef).ToArray(),
            meta = SerializeMeta(state.Meta),
        };
    }

    private static object? BuildDeltaSlice(
        IReadOnlyDictionary<string, RckWorkspaceContextPackDeltaObject> deltaLookup,
        string deltaId,
        bool includeDeltaDecodedOps)
    {
        if (!deltaLookup.TryGetValue(deltaId, out var delta))
        {
            return null;
        }

        var decodedPayload = includeDeltaDecodedOps && delta.Ops.Count > 0 ? delta.Ops[0].DecodedValueJson : default;
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
                decodedValue = includeDeltaDecodedOps ? op.DecodedValueJson : null,
            }).ToArray(),
            refs = delta.Refs.Select(SerializeRef).ToArray(),
            evidenceRefs = delta.EvidenceRefs.Select(SerializeEvidenceRef).ToArray(),
            meta = SerializeMeta(delta.Meta),
        };
    }

    private static object? BuildAnchorSlice(
        IReadOnlyDictionary<string, RckWorkspaceContextPackAnchorObject> anchorLookup,
        string anchorId)
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

    private static IReadOnlyDictionary<string, (string? ChangeType, string? Source)> BuildKnownArtifactLookup(RckWorkspaceContextPackResult contextPack)
    {
        var knownArtifacts = new Dictionary<string, (string? ChangeType, string? Source)>(StringComparer.Ordinal);

        foreach (var changedArtifact in contextPack.ChangedArtifacts)
        {
            if (string.IsNullOrWhiteSpace(changedArtifact.Path) || ShouldExcludePath(changedArtifact.Path))
            {
                continue;
            }

            knownArtifacts[NormalizePath(changedArtifact.Path)] = (changedArtifact.ChangeType, changedArtifact.Source);
        }

        foreach (var state in contextPack.States)
        {
            foreach (var artifact in ReadArtifactsFromPayload(state.PayloadDecoded))
            {
                if (string.IsNullOrWhiteSpace(artifact.Path) || ShouldExcludePath(artifact.Path))
                {
                    continue;
                }

                knownArtifacts[NormalizePath(artifact.Path)] = (artifact.ChangeType, artifact.Source);
            }
        }

        return knownArtifacts;
    }

    private static IReadOnlyList<object> BuildArtifacts(
        RckWorkspaceContextPackResult contextPack,
        IReadOnlyDictionary<string, RckWorkspaceContextPackStateObject> stateLookup,
        IReadOnlyList<string> selectedStateIds,
        IReadOnlyList<string> requestedArtifactPaths,
        IReadOnlyDictionary<string, (string? ChangeType, string? Source)> knownArtifacts)
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

        foreach (var stateId in selectedStateIds)
        {
            if (!stateLookup.TryGetValue(stateId, out var state))
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

        foreach (var requestedArtifactPath in requestedArtifactPaths)
        {
            if (!knownArtifacts.TryGetValue(requestedArtifactPath, out var metadata))
            {
                continue;
            }

            AddArtifact(
                artifacts,
                seen,
                requestedArtifactPath,
                metadata.ChangeType,
                metadata.Source ?? "proposal");
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

    private static JsonElement GetRequiredObject(JsonElement element, string propertyName)
    {
        if (element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.Object)
        {
            return property;
        }

        throw new InvalidDataException($"Missing object property '{propertyName}'.");
    }

    private static string GetRequiredString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.String)
        {
            throw new InvalidDataException($"Missing string property '{propertyName}'.");
        }

        var value = property.GetString();
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidDataException($"String property '{propertyName}' cannot be empty.");
        }

        return value;
    }

    private static int GetRequiredInt32(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.Number || !property.TryGetInt32(out var value))
        {
            throw new InvalidDataException($"Missing int property '{propertyName}'.");
        }

        return value;
    }

    private static IReadOnlyList<string> ReadDistinctStringArray(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<string>();
        }

        var values = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in property.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String)
            {
                continue;
            }

            var value = item.GetString();
            if (string.IsNullOrWhiteSpace(value) || !seen.Add(value))
            {
                continue;
            }

            values.Add(value);
        }

        return values;
    }

    private static bool GetOptionalBoolean(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property))
        {
            return false;
        }

        return property.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => false,
        };
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

public sealed record RckTraceSliceProposalValidationResult(bool Success, string? ErrorMessage, string? Json)
{
    public static RckTraceSliceProposalValidationResult Failure(string errorMessage) => new(false, errorMessage, null);

    public static RckTraceSliceProposalValidationResult SuccessResult(string json) => new(true, null, json);
}

public sealed record RckTraceSliceValidationItem(string Target, string Reason);
