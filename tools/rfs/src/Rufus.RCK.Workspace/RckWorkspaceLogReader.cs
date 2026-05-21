using System.Globalization;
using System.Text.Json;
using Rufus.RCK.Core.Model;

namespace Rufus.RCK.Workspace;

public static class RckWorkspaceLogReader
{
    public static RckWorkspaceLogResult Read(string? startingDirectory = null)
    {
        var repoRoot = FindRepoRoot(startingDirectory ?? Directory.GetCurrentDirectory());
        if (repoRoot is null)
        {
            return RckWorkspaceLogResult.Failure("rfs log: repository root not found.");
        }

        var paths = new RckWorkspacePaths(repoRoot);
        var workspaceInitialized = Directory.Exists(paths.WorkspaceDirectory);
        if (!workspaceInitialized)
        {
            return RckWorkspaceLogResult.Create(
                repoRoot,
                workspaceInitialized: false,
                rckDirectoryExists: false,
                rckInitialized: false,
                headExists: false,
                headResolved: false,
                headStateId: null,
                entries: Array.Empty<RckWorkspaceLogEntry>());
        }

        var rckInitialized = Directory.Exists(paths.RckDirectory);
        var headExists = File.Exists(paths.HeadPath);
        var headStateId = headExists ? ReadHeadStateId(paths.HeadPath) : null;
        if (!rckInitialized || !headExists)
        {
            return RckWorkspaceLogResult.Create(
                repoRoot,
                workspaceInitialized: true,
                rckDirectoryExists: rckInitialized,
                rckInitialized: false,
                headExists: headExists,
                headResolved: false,
                headStateId: headStateId,
                entries: Array.Empty<RckWorkspaceLogEntry>());
        }

        var states = LoadStates(paths.StatesDirectory);
        var deltas = LoadDeltas(paths.DeltasDirectory);
        var anchors = LoadAnchors(paths.AnchorsDirectory);

        var headResolved = !string.IsNullOrWhiteSpace(headStateId) && states.ContainsKey(headStateId);
        if (!headResolved)
        {
            return RckWorkspaceLogResult.Create(
                repoRoot,
                workspaceInitialized: true,
                rckDirectoryExists: true,
                rckInitialized: false,
                headExists: true,
                headResolved: false,
                headStateId: headStateId,
                entries: Array.Empty<RckWorkspaceLogEntry>());
        }

        var entries = BuildActiveChain(headStateId!, states, deltas, anchors);
        return RckWorkspaceLogResult.Create(
            repoRoot,
            workspaceInitialized: true,
            rckDirectoryExists: true,
            rckInitialized: true,
            headExists: true,
            headResolved: true,
            headStateId: headStateId,
            entries: entries);
    }

    private static IReadOnlyList<RckWorkspaceLogEntry> BuildActiveChain(
        string headStateId,
        IReadOnlyDictionary<string, StateSnapshot> states,
        IReadOnlyDictionary<string, DeltaSnapshot> deltas,
        IReadOnlyDictionary<string, IReadOnlyList<AnchorSnapshot>> anchors)
    {
        var orderedEntries = new List<RckWorkspaceLogEntry>();
        var currentStateId = headStateId;
        var visitedStateIds = new HashSet<string>(StringComparer.Ordinal);

        while (visitedStateIds.Add(currentStateId))
        {
            if (!states.TryGetValue(currentStateId, out var state))
            {
                break;
            }

            deltas.TryGetValue(currentStateId, out var delta);
            anchors.TryGetValue(currentStateId, out var anchorList);

            orderedEntries.Add(CreateEntry(state, delta, anchorList ?? Array.Empty<AnchorSnapshot>()));

            if (delta is null)
            {
                break;
            }

            currentStateId = delta.FromStateId;
        }

        return orderedEntries;
    }

    private static RckWorkspaceLogEntry CreateEntry(
        StateSnapshot state,
        DeltaSnapshot? delta,
        IReadOnlyList<AnchorSnapshot> anchors)
    {
        var mode = state.Mode ?? delta?.CauseMode ?? "unknown";
        return RckWorkspaceLogEntry.Create(
            state.StateId,
            delta?.DeltaId,
            mode,
            state.Prompt,
            state.AnswerSummary,
            state.GitCommit,
            state.GitDirty,
            state.Artifacts,
            state.CreatedAtUtc,
            state.CreatedBy,
            state.Label,
            state.Reason,
            anchors.Select(anchor => RckWorkspaceLogAnchor.Create(anchor.AnchorId, anchor.Label, anchor.CreatedAtUtc)).ToArray(),
            state.PayloadType);
    }

    private static IReadOnlyDictionary<string, StateSnapshot> LoadStates(string directory)
    {
        if (!Directory.Exists(directory))
        {
            return new Dictionary<string, StateSnapshot>(StringComparer.Ordinal);
        }

        var snapshots = new Dictionary<string, StateSnapshot>(StringComparer.Ordinal);
        foreach (var path in Directory.EnumerateFiles(directory, "*.json", SearchOption.TopDirectoryOnly)
                     .OrderBy(path => Path.GetFileName(path), StringComparer.Ordinal))
        {
            var snapshot = TryReadStateSnapshot(path);
            if (snapshot is null)
            {
                continue;
            }

            snapshots[snapshot.StateId] = snapshot;
        }

        return snapshots;
    }

    private static IReadOnlyDictionary<string, DeltaSnapshot> LoadDeltas(string directory)
    {
        if (!Directory.Exists(directory))
        {
            return new Dictionary<string, DeltaSnapshot>(StringComparer.Ordinal);
        }

        var snapshots = new Dictionary<string, DeltaSnapshot>(StringComparer.Ordinal);
        foreach (var path in Directory.EnumerateFiles(directory, "*.json", SearchOption.TopDirectoryOnly)
                     .OrderBy(path => Path.GetFileName(path), StringComparer.Ordinal))
        {
            var snapshot = TryReadDeltaSnapshot(path);
            if (snapshot is null)
            {
                continue;
            }

            snapshots[snapshot.ToStateId] = snapshot;
        }

        return snapshots;
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<AnchorSnapshot>> LoadAnchors(string directory)
    {
        if (!Directory.Exists(directory))
        {
            return new Dictionary<string, IReadOnlyList<AnchorSnapshot>>(StringComparer.Ordinal);
        }

        var anchors = new Dictionary<string, List<AnchorSnapshot>>(StringComparer.Ordinal);
        foreach (var path in Directory.EnumerateFiles(directory, "*.json", SearchOption.TopDirectoryOnly)
                     .OrderBy(path => Path.GetFileName(path), StringComparer.Ordinal))
        {
            var anchor = TryReadAnchorSnapshot(path);
            if (anchor is null)
            {
                continue;
            }

            if (!anchors.TryGetValue(anchor.StateId, out var list))
            {
                list = new List<AnchorSnapshot>();
                anchors[anchor.StateId] = list;
            }

            list.Add(anchor);
        }

        return anchors.ToDictionary(pair => pair.Key, pair => (IReadOnlyList<AnchorSnapshot>)pair.Value, StringComparer.Ordinal);
    }

    private static StateSnapshot? TryReadStateSnapshot(string path)
    {
        try
        {
            var root = ParseRoot(path);
            var id = GetRequiredString(root, "id", path);
            var meta = root.TryGetProperty("meta", out var metaElement) ? metaElement : default;
            var payloadJson = GetRequiredString(root, "payloadCanonicalJson", path);
            using var payloadDocument = JsonDocument.Parse(payloadJson);
            var payloadRoot = payloadDocument.RootElement;

            var payloadType = GetOptionalString(payloadRoot, "type") ?? "unknown";
            var createdAtUtc = ParseCreatedAt(meta, path);
            var createdBy = GetOptionalString(meta, "CreatedBy");
            var label = GetOptionalString(meta, "Label");
            var reason = GetOptionalString(meta, "Reason");

            var gitCommit = GetOptionalGitCommit(payloadRoot);
            var gitDirty = GetOptionalGitDirty(payloadRoot);
            var artifacts = GetArtifacts(payloadRoot, path);
            var interaction = payloadRoot.TryGetProperty("interaction", out var interactionElement) ? interactionElement : default;
            var mode = payloadType == "rufus.initial-state"
                ? "genesis"
                : GetOptionalString(interaction, "mode") ?? "unknown";
            var prompt = GetOptionalString(interaction, "prompt");
            var answerSummary = GetOptionalString(interaction, "answerSummary");

            return new StateSnapshot(
                id,
                payloadType,
                createdAtUtc,
                createdBy,
                label,
                reason,
                mode,
                prompt,
                answerSummary,
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
            var meta = root.TryGetProperty("meta", out var metaElement) ? metaElement : default;
            var createdAtUtc = ParseCreatedAt(meta, path);
            var valueJson = TryGetFirstOpValueJson(root, path);
            var decoded = DecodeDeltaValueJson(valueJson);

            return new DeltaSnapshot(
                id,
                fromStateId,
                toStateId,
                createdAtUtc,
                GetOptionalString(meta, "CreatedBy"),
                GetOptionalString(meta, "Label"),
                GetOptionalString(meta, "Reason"),
                decoded?.Mode,
                decoded?.Prompt,
                decoded?.Answer,
                decoded?.EvidenceToolCount ?? 0,
                decoded?.EvidenceArtifactCount ?? 0);
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
            var meta = root.TryGetProperty("meta", out var metaElement) ? metaElement : default;
            var createdAtUtc = ParseCreatedAt(meta, path);

            return new AnchorSnapshot(
                id,
                stateId,
                GetOptionalString(meta, "Label"),
                createdAtUtc);
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

    private static string GetRequiredString(JsonElement element, string propertyName, string path)
    {
        if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.String)
        {
            throw new InvalidDataException($"Invalid RCK JSON at {path}: missing string property '{propertyName}'.");
        }

        return property.GetString() ?? string.Empty;
    }

    private static string? GetOptionalString(JsonElement element, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        var value = property.GetString();
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static bool GetOptionalGitDirty(JsonElement payloadRoot)
    {
        if (!payloadRoot.TryGetProperty("git", out var gitElement))
        {
            return false;
        }

        if (!gitElement.TryGetProperty("dirty", out var dirtyElement))
        {
            return false;
        }

        return dirtyElement.ValueKind == JsonValueKind.True;
    }

    private static string? GetOptionalGitCommit(JsonElement payloadRoot)
    {
        if (!payloadRoot.TryGetProperty("git", out var gitElement))
        {
            return null;
        }

        return GetOptionalString(gitElement, "commit");
    }

    private static DateTimeOffset ParseCreatedAt(JsonElement meta, string path)
    {
        var createdAtText = GetRequiredString(meta, "createdAtUtc", path);
        return DateTimeOffset.Parse(createdAtText, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
    }

    private static IReadOnlyList<GitWorkspaceArtifactChange> GetArtifacts(JsonElement payloadRoot, string path)
    {
        if (!payloadRoot.TryGetProperty("artifacts", out var artifactsElement) || artifactsElement.ValueKind != JsonValueKind.Array)
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
            var artifactPath = GetOptionalString(artifactElement, "path");
            var changeType = GetOptionalString(artifactElement, "changeType");
            var gitStatus = GetOptionalString(artifactElement, "gitStatus");
            var source = GetOptionalString(artifactElement, "source");

            if (kind is null || artifactPath is null || changeType is null || gitStatus is null || source is null)
            {
                continue;
            }

            artifacts.Add(new GitWorkspaceArtifactChange(kind, artifactPath, changeType, gitStatus, source));
        }

        return artifacts;
    }

    private static string? TryGetFirstOpValueJson(JsonElement root, string path)
    {
        if (!root.TryGetProperty("ops", out var opsElement) || opsElement.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (var opElement in opsElement.EnumerateArray())
        {
            if (opElement.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            if (!opElement.TryGetProperty("valueJson", out var valueJsonElement) || valueJsonElement.ValueKind != JsonValueKind.String)
            {
                continue;
            }

            var valueJson = valueJsonElement.GetString();
            if (!string.IsNullOrWhiteSpace(valueJson))
            {
                return valueJson;
            }
        }

        return null;
    }

    private static DeltaDecoded? DecodeDeltaValueJson(string? valueJson)
    {
        if (string.IsNullOrWhiteSpace(valueJson))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(valueJson);
            var root = document.RootElement;
            var cause = root.TryGetProperty("cause", out var causeElement) && causeElement.ValueKind == JsonValueKind.Object
                ? causeElement
                : default;
            var evidence = root.TryGetProperty("evidence", out var evidenceElement) && evidenceElement.ValueKind == JsonValueKind.Object
                ? evidenceElement
                : default;

            return new DeltaDecoded(
                GetOptionalString(cause, "mode"),
                GetOptionalString(cause, "prompt"),
                GetOptionalString(cause, "answer"),
                CountArrayElements(evidence, "tools"),
                CountArrayElements(evidence, "artifacts"));
        }
        catch
        {
            return null;
        }
    }

    private static int CountArrayElements(JsonElement element, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return 0;
        }

        if (!element.TryGetProperty(propertyName, out var arrayElement) || arrayElement.ValueKind != JsonValueKind.Array)
        {
            return 0;
        }

        return arrayElement.GetArrayLength();
    }

    private static string? ReadHeadStateId(string headPath)
    {
        try
        {
            var headContent = File.ReadAllText(headPath).Trim();
            return string.IsNullOrWhiteSpace(headContent) ? null : headContent;
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

    private sealed record StateSnapshot(
        string StateId,
        string PayloadType,
        DateTimeOffset CreatedAtUtc,
        string? CreatedBy,
        string? Label,
        string? Reason,
        string? Mode,
        string? Prompt,
        string? AnswerSummary,
        string? GitCommit,
        bool GitDirty,
        IReadOnlyList<GitWorkspaceArtifactChange> Artifacts);

    private sealed record DeltaSnapshot(
        string DeltaId,
        string FromStateId,
        string ToStateId,
        DateTimeOffset CreatedAtUtc,
        string? CreatedBy,
        string? Label,
        string? Reason,
        string? CauseMode,
        string? CausePrompt,
        string? CauseAnswer,
        int EvidenceToolCount,
        int EvidenceArtifactCount);

    private sealed record AnchorSnapshot(
        string AnchorId,
        string StateId,
        string? Label,
        DateTimeOffset CreatedAtUtc);

    private sealed record DeltaDecoded(
        string? Mode,
        string? Prompt,
        string? Answer,
        int EvidenceToolCount,
        int EvidenceArtifactCount);
}
