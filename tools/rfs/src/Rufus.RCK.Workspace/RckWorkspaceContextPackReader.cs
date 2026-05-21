using System.Globalization;
using System.Text.Json;

namespace Rufus.RCK.Workspace;

public static class RckWorkspaceContextPackReader
{
    public static RckWorkspaceContextPackResult Read(string? startingDirectory = null)
    {
        var repoRoot = FindRepoRoot(startingDirectory ?? Directory.GetCurrentDirectory());
        if (repoRoot is null)
        {
            return RckWorkspaceContextPackResult.Failure("rfs context-pack: repository root not found. run rfs init");
        }

        var paths = new RckWorkspacePaths(repoRoot);
        if (!Directory.Exists(paths.WorkspaceDirectory) || !Directory.Exists(paths.RckDirectory) || !File.Exists(paths.HeadPath))
        {
            return RckWorkspaceContextPackResult.Failure("rfs context-pack: .rfs is not initialized. run rfs init");
        }

        var headStateId = ReadHeadStateId(paths.HeadPath);
        if (headStateId is null)
        {
            return RckWorkspaceContextPackResult.Failure("rfs context-pack: invalid HEAD file. run rfs init");
        }

        var stateSnapshots = LoadStateSnapshots(paths.StatesDirectory);
        if (stateSnapshots.Count == 0)
        {
            return RckWorkspaceContextPackResult.Failure("rfs context-pack: no State objects found. run rfs init");
        }

        var statesById = stateSnapshots.ToDictionary(snapshot => snapshot.Object.Id, snapshot => snapshot, StringComparer.Ordinal);
        if (!statesById.ContainsKey(headStateId))
        {
            return RckWorkspaceContextPackResult.Failure("rfs context-pack: HEAD points to a missing State. run rfs init");
        }

        var deltaSnapshots = LoadDeltaSnapshots(paths.DeltasDirectory);
        var anchorSnapshots = LoadAnchorSnapshots(paths.AnchorsDirectory);
        var gitContext = GitWorkspaceContext.Capture(repoRoot);
        var generatedAtUtc = DateTimeOffset.UtcNow;

        var states = stateSnapshots.Select(snapshot => snapshot.Object).ToArray();
        var deltas = deltaSnapshots.Select(snapshot => snapshot.Object).ToArray();
        var anchors = anchorSnapshots.Select(snapshot => snapshot.Object).ToArray();

        var deltasByToStateSnapshots = BuildDeltaSnapshotIndex(deltaSnapshots, snapshot => snapshot.Object.ToStateId);
        var deltasByFromStateSnapshots = BuildDeltaSnapshotIndex(deltaSnapshots, snapshot => snapshot.Object.FromStateId);
        var anchorsByStateId = BuildAnchorIndex(anchorSnapshots);
        var activeChain = BuildActiveChain(headStateId, statesById, deltasByToStateSnapshots, anchorsByStateId);

        var activeStateIds = activeChain.Select(entry => entry.StateId).ToArray();
        var activeDeltaIds = activeChain
            .Select(entry => entry.IncomingDeltaId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id!)
            .ToArray();

        var orphanStateIds = states
            .Select(state => state.Id)
            .Where(stateId => !activeStateIds.Contains(stateId, StringComparer.Ordinal))
            .OrderBy(stateId => stateId, StringComparer.Ordinal)
            .ToArray();

        var orphanDeltaIds = deltas
            .Select(delta => delta.Id)
            .Where(deltaId => !activeDeltaIds.Contains(deltaId, StringComparer.Ordinal))
            .OrderBy(deltaId => deltaId, StringComparer.Ordinal)
            .ToArray();

        var workspace = new RckWorkspaceContextPackWorkspace(
            Root: repoRoot,
            GitBranch: gitContext.Branch,
            GitCommit: gitContext.Commit,
            GitDirty: gitContext.Dirty);

        return RckWorkspaceContextPackResult.Create(
            repoRoot: repoRoot,
            workspace: workspace,
            headStateId: headStateId,
            generatedAtUtc: generatedAtUtc,
            states: states,
            deltas: deltas,
            anchors: anchors,
            activeChain: activeChain,
            activeStateIds: activeStateIds,
            activeDeltaIds: activeDeltaIds,
            orphanStateIds: orphanStateIds,
            orphanDeltaIds: orphanDeltaIds,
            anchorsByStateId: anchorsByStateId,
            deltasByToStateId: BuildDeltaIdIndex(deltasByToStateSnapshots),
            deltasByFromStateId: BuildDeltaIdIndex(deltasByFromStateSnapshots));
    }

    private static IReadOnlyList<RckWorkspaceContextPackActiveEntry> BuildActiveChain(
        string headStateId,
        IReadOnlyDictionary<string, StateSnapshot> statesById,
        IReadOnlyDictionary<string, IReadOnlyList<DeltaSnapshot>> deltasByToStateId,
        IReadOnlyDictionary<string, IReadOnlyList<RckWorkspaceContextPackAnchorSummary>> anchorsByStateId)
    {
        var chain = new List<RckWorkspaceContextPackActiveEntry>();
        var visitedStateIds = new HashSet<string>(StringComparer.Ordinal);
        var currentStateId = headStateId;

        while (visitedStateIds.Add(currentStateId))
        {
            if (!statesById.TryGetValue(currentStateId, out var stateSnapshot))
            {
                break;
            }

            deltasByToStateId.TryGetValue(currentStateId, out var incomingDeltas);
            var incomingDelta = incomingDeltas is { Count: > 0 } ? incomingDeltas[0] : null;
            anchorsByStateId.TryGetValue(currentStateId, out var anchors);

            var promptPreview = BuildPromptPreview(stateSnapshot.InteractionPrompt, out var promptIsExcerpt);
            var mode = stateSnapshot.InteractionMode ?? (incomingDelta is null ? "genesis" : "unknown");

            chain.Add(new RckWorkspaceContextPackActiveEntry(
                StateId: currentStateId,
                IncomingDeltaId: incomingDelta?.Object.Id,
                Anchors: anchors ?? Array.Empty<RckWorkspaceContextPackAnchorSummary>(),
                Mode: mode,
                Prompt: promptPreview,
                PromptIsExcerpt: promptIsExcerpt,
                AnswerSummary: stateSnapshot.InteractionAnswerSummary,
                GitBranch: stateSnapshot.GitBranch,
                GitCommit: stateSnapshot.GitCommit,
                GitDirty: stateSnapshot.GitDirty,
                Artifacts: stateSnapshot.Artifacts));

            if (incomingDelta is null)
            {
                break;
            }

            currentStateId = incomingDelta.Object.FromStateId;
        }

        return chain;
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<RckWorkspaceContextPackAnchorSummary>> BuildAnchorIndex(IReadOnlyList<AnchorSnapshot> anchors)
    {
        var grouped = new Dictionary<string, List<RckWorkspaceContextPackAnchorSummary>>(StringComparer.Ordinal);

        foreach (var anchor in anchors)
        {
            if (!grouped.TryGetValue(anchor.Object.StateId, out var list))
            {
                list = new List<RckWorkspaceContextPackAnchorSummary>();
                grouped[anchor.Object.StateId] = list;
            }

            list.Add(new RckWorkspaceContextPackAnchorSummary(anchor.Object.Id, anchor.Object.Meta.Label));
        }

        var result = new SortedDictionary<string, IReadOnlyList<RckWorkspaceContextPackAnchorSummary>>(StringComparer.Ordinal);
        foreach (var pair in grouped)
        {
            result[pair.Key] = pair.Value.OrderBy(anchor => anchor.Id, StringComparer.Ordinal).ToArray();
        }

        return result;
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<DeltaSnapshot>> BuildDeltaSnapshotIndex(
        IReadOnlyList<DeltaSnapshot> snapshots,
        Func<DeltaSnapshot, string> selector)
    {
        var grouped = new Dictionary<string, List<DeltaSnapshot>>(StringComparer.Ordinal);

        foreach (var snapshot in snapshots)
        {
            var key = selector(snapshot);
            if (!grouped.TryGetValue(key, out var list))
            {
                list = new List<DeltaSnapshot>();
                grouped[key] = list;
            }

            list.Add(snapshot);
        }

        var result = new SortedDictionary<string, IReadOnlyList<DeltaSnapshot>>(StringComparer.Ordinal);
        foreach (var pair in grouped)
        {
            result[pair.Key] = pair.Value.OrderBy(snapshot => snapshot.Object.Id, StringComparer.Ordinal).ToArray();
        }

        return result;
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<string>> BuildDeltaIdIndex(IReadOnlyDictionary<string, IReadOnlyList<DeltaSnapshot>> snapshotsByKey)
    {
        var result = new SortedDictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
        foreach (var pair in snapshotsByKey)
        {
            result[pair.Key] = pair.Value.Select(snapshot => snapshot.Object.Id).ToArray();
        }

        return result;
    }

    private static List<StateSnapshot> LoadStateSnapshots(string directory)
    {
        var snapshots = new List<StateSnapshot>();
        if (!Directory.Exists(directory))
        {
            return snapshots;
        }

        foreach (var path in Directory.EnumerateFiles(directory, "*.json", SearchOption.TopDirectoryOnly)
                     .OrderBy(path => Path.GetFileName(path), StringComparer.Ordinal))
        {
            var snapshot = TryReadStateSnapshot(path);
            if (snapshot is not null)
            {
                snapshots.Add(snapshot);
            }
        }

        return snapshots;
    }

    private static List<DeltaSnapshot> LoadDeltaSnapshots(string directory)
    {
        var snapshots = new List<DeltaSnapshot>();
        if (!Directory.Exists(directory))
        {
            return snapshots;
        }

        foreach (var path in Directory.EnumerateFiles(directory, "*.json", SearchOption.TopDirectoryOnly)
                     .OrderBy(path => Path.GetFileName(path), StringComparer.Ordinal))
        {
            var snapshot = TryReadDeltaSnapshot(path);
            if (snapshot is not null)
            {
                snapshots.Add(snapshot);
            }
        }

        return snapshots;
    }

    private static List<AnchorSnapshot> LoadAnchorSnapshots(string directory)
    {
        var snapshots = new List<AnchorSnapshot>();
        if (!Directory.Exists(directory))
        {
            return snapshots;
        }

        foreach (var path in Directory.EnumerateFiles(directory, "*.json", SearchOption.TopDirectoryOnly)
                     .OrderBy(path => Path.GetFileName(path), StringComparer.Ordinal))
        {
            var snapshot = TryReadAnchorSnapshot(path);
            if (snapshot is not null)
            {
                snapshots.Add(snapshot);
            }
        }

        return snapshots;
    }

    private static StateSnapshot? TryReadStateSnapshot(string path)
    {
        try
        {
            var root = ParseRoot(path);
            var id = GetRequiredString(root, "id", path);
            var payloadCanonicalJson = GetRequiredString(root, "payloadCanonicalJson", path);
            var payloadDecoded = TryParseJsonElement(payloadCanonicalJson);
            var refs = ReadRefs(root, path);
            var meta = ReadMeta(GetRequiredObject(root, "meta", path), path);

            string? interactionMode = null;
            string? interactionPrompt = null;
            string? interactionAnswerSummary = null;
            string? gitBranch = null;
            string? gitCommit = null;
            bool gitDirty = false;
            IReadOnlyList<GitWorkspaceArtifactChange> artifacts = Array.Empty<GitWorkspaceArtifactChange>();

            if (payloadDecoded is JsonElement decodedElement && decodedElement.ValueKind == JsonValueKind.Object)
            {
                var payloadType = GetOptionalString(decodedElement, "type");
                if (string.Equals(payloadType, "rufus.initial-state", StringComparison.Ordinal))
                {
                    interactionMode = "genesis";
                }
                else if (TryGetObjectProperty(decodedElement, "interaction", out var interactionElement))
                {
                    interactionMode = GetOptionalString(interactionElement, "mode");
                    interactionPrompt = GetOptionalString(interactionElement, "prompt");
                    interactionAnswerSummary = GetOptionalString(interactionElement, "answerSummary");
                }

                if (TryGetObjectProperty(decodedElement, "git", out var gitElement))
                {
                    gitBranch = GetOptionalString(gitElement, "branch");
                    gitCommit = GetOptionalString(gitElement, "commit");
                    gitDirty = GetOptionalBoolean(gitElement, "dirty");
                }

                artifacts = ReadArtifactArray(decodedElement);
            }

            return new StateSnapshot(
                new RckWorkspaceContextPackStateObject(id, payloadCanonicalJson, payloadDecoded, refs, meta),
                interactionMode,
                interactionPrompt,
                interactionAnswerSummary,
                gitBranch,
                gitCommit,
                gitDirty,
                artifacts);
        }
        catch
        {
            return null;
        }
    }

    private static DeltaSnapshot? TryReadDeltaSnapshot(string path)
    {
        try
        {
            var root = ParseRoot(path);
            var id = GetRequiredString(root, "id", path);
            var fromStateId = GetRequiredString(root, "fromStateId", path);
            var toStateId = GetRequiredString(root, "toStateId", path);
            var ops = ReadDeltaOps(root, path);
            var refs = ReadRefs(root, path);
            var evidenceRefs = ReadEvidenceRefs(root, path);
            var meta = ReadMeta(GetRequiredObject(root, "meta", path), path);

            return new DeltaSnapshot(
                new RckWorkspaceContextPackDeltaObject(id, fromStateId, toStateId, ops, refs, evidenceRefs, meta));
        }
        catch
        {
            return null;
        }
    }

    private static AnchorSnapshot? TryReadAnchorSnapshot(string path)
    {
        try
        {
            var root = ParseRoot(path);
            var id = GetRequiredString(root, "id", path);
            var stateId = GetRequiredString(root, "stateId", path);
            var parentAnchorIds = ReadStringArray(root, "parentAnchorIds");
            var meta = ReadMeta(GetRequiredObject(root, "meta", path), path);

            return new AnchorSnapshot(new RckWorkspaceContextPackAnchorObject(id, stateId, parentAnchorIds, meta));
        }
        catch
        {
            return null;
        }
    }

    private static IReadOnlyList<RckWorkspaceContextPackDeltaOpObject> ReadDeltaOps(JsonElement root, string path)
    {
        if (!root.TryGetProperty("ops", out var opsElement) || opsElement.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<RckWorkspaceContextPackDeltaOpObject>();
        }

        var ops = new List<RckWorkspaceContextPackDeltaOpObject>();
        foreach (var opElement in opsElement.EnumerateArray())
        {
            if (opElement.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var kind = GetOptionalString(opElement, "kind");
            var opPath = GetOptionalString(opElement, "path");
            if (kind is null || opPath is null)
            {
                continue;
            }

            var valueJson = GetOptionalString(opElement, "valueJson");
            var decodedValueJson = TryParseJsonElement(valueJson);
            ops.Add(new RckWorkspaceContextPackDeltaOpObject(kind, opPath, valueJson, decodedValueJson));
        }

        return ops;
    }

    private static IReadOnlyList<RckWorkspaceContextPackRefObject> ReadRefs(JsonElement root, string path)
    {
        if (!root.TryGetProperty("refs", out var refsElement) || refsElement.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<RckWorkspaceContextPackRefObject>();
        }

        var refs = new List<RckWorkspaceContextPackRefObject>();
        foreach (var refElement in refsElement.EnumerateArray())
        {
            if (refElement.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var id = GetOptionalString(refElement, "id");
            var kind = GetOptionalString(refElement, "kind");
            var uri = GetOptionalString(refElement, "uri");
            if (id is null || kind is null || uri is null)
            {
                continue;
            }

            var meta = refElement.TryGetProperty("meta", out var metaElement) && metaElement.ValueKind == JsonValueKind.Object
                ? ReadMeta(metaElement, path)
                : null;

            refs.Add(new RckWorkspaceContextPackRefObject(
                id,
                kind,
                uri,
                GetOptionalString(refElement, "hash"),
                GetOptionalString(refElement, "mediaType"),
                meta));
        }

        return refs;
    }

    private static IReadOnlyList<RckWorkspaceContextPackEvidenceRefObject> ReadEvidenceRefs(JsonElement root, string path)
    {
        if (!root.TryGetProperty("evidenceRefs", out var evidenceRefsElement) || evidenceRefsElement.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<RckWorkspaceContextPackEvidenceRefObject>();
        }

        var evidenceRefs = new List<RckWorkspaceContextPackEvidenceRefObject>();
        foreach (var evidenceRefElement in evidenceRefsElement.EnumerateArray())
        {
            if (evidenceRefElement.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var id = GetOptionalString(evidenceRefElement, "id");
            var kind = GetOptionalString(evidenceRefElement, "kind");
            if (id is null || kind is null)
            {
                continue;
            }

            if (!TryGetObjectProperty(evidenceRefElement, "ref", out var refElement) || refElement.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var nestedRefId = GetOptionalString(refElement, "id");
            var nestedRefKind = GetOptionalString(refElement, "kind");
            var nestedRefUri = GetOptionalString(refElement, "uri");
            if (nestedRefId is null || nestedRefKind is null || nestedRefUri is null)
            {
                continue;
            }

            var nestedMeta = refElement.TryGetProperty("meta", out var metaElement) && metaElement.ValueKind == JsonValueKind.Object
                ? ReadMeta(metaElement, path)
                : null;

            var nestedRef = new RckWorkspaceContextPackRefObject(
                nestedRefId,
                nestedRefKind,
                nestedRefUri,
                GetOptionalString(refElement, "hash"),
                GetOptionalString(refElement, "mediaType"),
                nestedMeta);

            evidenceRefs.Add(new RckWorkspaceContextPackEvidenceRefObject(
                id,
                kind,
                nestedRef,
                GetOptionalString(evidenceRefElement, "summary"),
                GetOptionalString(evidenceRefElement, "hash")));
        }

        return evidenceRefs;
    }

    private static IReadOnlyList<GitWorkspaceArtifactChange> ReadArtifactArray(JsonElement root)
    {
        if (!root.TryGetProperty("artifacts", out var artifactsElement) || artifactsElement.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<GitWorkspaceArtifactChange>();
        }

        var artifacts = new List<GitWorkspaceArtifactChange>();
        foreach (var artifactElement in artifactsElement.EnumerateArray())
        {
            if (artifactElement.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var kind = GetOptionalString(artifactElement, "kind");
            var path = GetOptionalString(artifactElement, "path");
            var changeType = GetOptionalString(artifactElement, "changeType");
            var gitStatus = GetOptionalString(artifactElement, "gitStatus");
            var source = GetOptionalString(artifactElement, "source");
            if (kind is null || path is null || changeType is null || gitStatus is null || source is null)
            {
                continue;
            }

            artifacts.Add(new GitWorkspaceArtifactChange(kind, path, changeType, gitStatus, source));
        }

        return artifacts;
    }

    private static IReadOnlyList<string> ReadStringArray(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var arrayElement) || arrayElement.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<string>();
        }

        var values = new List<string>();
        foreach (var item in arrayElement.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String)
            {
                continue;
            }

            var value = item.GetString();
            if (!string.IsNullOrWhiteSpace(value))
            {
                values.Add(value);
            }
        }

        return values;
    }

    private static RckWorkspaceContextPackMeta ReadMeta(JsonElement metaElement, string path)
    {
        var createdAtUtcText = GetRequiredString(metaElement, "createdAtUtc", path);
        var createdAtUtc = DateTimeOffset.Parse(createdAtUtcText, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
        return new RckWorkspaceContextPackMeta(
            createdAtUtc,
            GetOptionalString(metaElement, "CreatedBy"),
            GetOptionalString(metaElement, "Label"),
            GetOptionalString(metaElement, "Reason"));
    }

    private static JsonElement? TryParseJsonElement(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            return document.RootElement.Clone();
        }
        catch
        {
            return null;
        }
    }

    private static JsonElement ParseRoot(string path)
    {
        var json = File.ReadAllText(path);
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

    private static JsonElement GetRequiredObject(JsonElement root, string propertyName, string path)
    {
        if (!root.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException($"Invalid RCK JSON at {path}: missing object property '{propertyName}'.");
        }

        return property;
    }

    private static string GetRequiredString(JsonElement root, string propertyName, string path)
    {
        if (!root.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.String)
        {
            throw new InvalidDataException($"Invalid RCK JSON at {path}: missing string property '{propertyName}'.");
        }

        return property.GetString() ?? string.Empty;
    }

    private static string? GetOptionalString(JsonElement root, string propertyName)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        if (!root.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        var value = property.GetString();
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static bool GetOptionalBoolean(JsonElement root, string propertyName)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        if (!root.TryGetProperty(propertyName, out var property))
        {
            return false;
        }

        return property.ValueKind == JsonValueKind.True;
    }

    private static bool TryGetObjectProperty(JsonElement root, string propertyName, out JsonElement property)
    {
        property = default;
        if (root.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        if (!root.TryGetProperty(propertyName, out property))
        {
            return false;
        }

        return true;
    }

    private static string? ReadHeadStateId(string headPath)
    {
        try
        {
            var headContent = File.ReadAllText(headPath).Trim();
            return string.IsNullOrWhiteSpace(headContent) ? null : headContent;
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static string BuildPromptPreview(string? prompt, out bool isExcerpt)
    {
        if (string.IsNullOrWhiteSpace(prompt))
        {
            isExcerpt = false;
            return string.Empty;
        }

        var normalized = string.Join(" ", prompt.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        const int maxLength = 360;
        if (normalized.Length <= maxLength)
        {
            isExcerpt = false;
            return normalized;
        }

        isExcerpt = true;
        return normalized[..(maxLength - 1)] + "…";
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

    private sealed record StateSnapshot(
        RckWorkspaceContextPackStateObject Object,
        string? InteractionMode,
        string? InteractionPrompt,
        string? InteractionAnswerSummary,
        string? GitBranch,
        string? GitCommit,
        bool GitDirty,
        IReadOnlyList<GitWorkspaceArtifactChange> Artifacts);

    private sealed record DeltaSnapshot(RckWorkspaceContextPackDeltaObject Object);

    private sealed record AnchorSnapshot(RckWorkspaceContextPackAnchorObject Object);
}
