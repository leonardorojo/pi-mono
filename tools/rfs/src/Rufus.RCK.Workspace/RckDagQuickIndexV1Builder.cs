using System.Text.Json;

namespace Rufus.RCK.Workspace;

public static class RckDagQuickIndexV1Builder
{
    public static RckDagQuickIndexV1BuildResult Build(string? startingDirectory = null, int recentChainLimit = 5)
    {
        if (recentChainLimit < 1)
        {
            recentChainLimit = 1;
        }

        var contextPack = RckWorkspaceContextPackReader.Read(startingDirectory);
        if (!contextPack.Success)
        {
            return RckDagQuickIndexV1BuildResult.Failure(
                contextPack.ErrorMessage ?? "rfs dag-quick-index-v1: failed to read RCK workspace state.");
        }

        if (string.IsNullOrWhiteSpace(contextPack.HeadStateId))
        {
            return RckDagQuickIndexV1BuildResult.Failure("rfs dag-quick-index-v1: HEAD state not available.");
        }

        var warnings = new List<string>();
        var headStateId = contextPack.HeadStateId!;
        var recentEntries = contextPack.ActiveChain.Take(recentChainLimit).ToArray();
        var recentStateIds = recentEntries
            .Select(entry => entry.StateId)
            .Where(stateId => !string.IsNullOrWhiteSpace(stateId))
            .ToArray();
        var recentDeltaIds = recentEntries
            .Select(entry => entry.IncomingDeltaId)
            .Where(deltaId => !string.IsNullOrWhiteSpace(deltaId))
            .Select(deltaId => deltaId!)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        var statesById = contextPack.States.ToDictionary(state => state.Id, StringComparer.Ordinal);
        var deltasById = contextPack.Deltas.ToDictionary(delta => delta.Id, StringComparer.Ordinal);
        var anchorsByStateId = contextPack.AnchorsByStateId;
        var stateDistances = BuildStateDistances(headStateId, contextPack.DeltasByToStateId, deltasById);

        var anchorCandidates = BuildAnchorCandidates(
            contextPack.Anchors,
            statesById,
            contextPack.DeltasByToStateId,
            contextPack.DeltasByFromStateId,
            stateDistances,
            recentStateIds,
            warnings);

        var stateCandidates = BuildStateCandidates(
            contextPack.States,
            anchorsByStateId,
            contextPack.DeltasByToStateId,
            contextPack.DeltasByFromStateId,
            stateDistances,
            contextPack.ActiveChain,
            warnings);

        var deltaCandidates = BuildDeltaCandidates(
            contextPack.Deltas,
            deltasById,
            stateDistances,
            warnings);

        return RckDagQuickIndexV1BuildResult.SuccessResult(
            new RckDagQuickIndexV1(
                HeadStateId: headStateId,
                RecentStateIds: recentStateIds,
                RecentDeltaIds: recentDeltaIds,
                Anchors: anchorCandidates,
                States: stateCandidates,
                Deltas: deltaCandidates),
            warnings);
    }

    private static IReadOnlyList<RckDagAnchorCandidate> BuildAnchorCandidates(
        IReadOnlyList<RckWorkspaceContextPackAnchorObject> anchors,
        IReadOnlyDictionary<string, RckWorkspaceContextPackStateObject> statesById,
        IReadOnlyDictionary<string, IReadOnlyList<string>> deltasByToStateId,
        IReadOnlyDictionary<string, IReadOnlyList<string>> deltasByFromStateId,
        IReadOnlyDictionary<string, int> stateDistances,
        IReadOnlyCollection<string> recentStateIds,
        List<string> warnings)
    {
        return anchors
            .OrderBy(anchor => anchor.Id, StringComparer.Ordinal)
            .Select(anchor =>
            {
                if (!statesById.ContainsKey(anchor.StateId))
                {
                    warnings.Add($"rfs dag-quick-index-v1: anchor '{anchor.Id}' points to missing state '{anchor.StateId}'.");
                }

                deltasByToStateId.TryGetValue(anchor.StateId, out var incomingDeltaIds);
                deltasByFromStateId.TryGetValue(anchor.StateId, out var outgoingDeltaIds);

                return new RckDagAnchorCandidate(
                    Id: anchor.Id,
                    StateId: anchor.StateId,
                    Label: anchor.Meta.Label,
                    Reason: anchor.Meta.Reason,
                    CreatedAtUtc: anchor.Meta.CreatedAtUtc,
                    IsRecentChain: recentStateIds.Contains(anchor.StateId, StringComparer.Ordinal),
                    ParentAnchorIds: anchor.ParentAnchorIds.ToArray(),
                    DistanceToHead: TryGetDistance(stateDistances, anchor.StateId),
                    IncomingDeltaIds: incomingDeltaIds?.OrderBy(deltaId => deltaId, StringComparer.Ordinal).ToArray() ?? Array.Empty<string>(),
                    OutgoingDeltaIds: outgoingDeltaIds?.OrderBy(deltaId => deltaId, StringComparer.Ordinal).ToArray() ?? Array.Empty<string>());
            })
            .ToArray();
    }

    private static IReadOnlyList<RckDagStateCandidate> BuildStateCandidates(
        IReadOnlyList<RckWorkspaceContextPackStateObject> states,
        IReadOnlyDictionary<string, IReadOnlyList<RckWorkspaceContextPackAnchorSummary>> anchorsByStateId,
        IReadOnlyDictionary<string, IReadOnlyList<string>> deltasByToStateId,
        IReadOnlyDictionary<string, IReadOnlyList<string>> deltasByFromStateId,
        IReadOnlyDictionary<string, int> stateDistances,
        IReadOnlyList<RckWorkspaceContextPackActiveEntry> activeChain,
        List<string> warnings)
    {
        var activeEntriesByStateId = activeChain.ToDictionary(entry => entry.StateId, StringComparer.Ordinal);

        return states
            .OrderBy(state => state.Id, StringComparer.Ordinal)
            .Select(state =>
            {
                anchorsByStateId.TryGetValue(state.Id, out var anchors);
                deltasByToStateId.TryGetValue(state.Id, out var incomingDeltaIds);
                deltasByFromStateId.TryGetValue(state.Id, out var outgoingDeltaIds);

                if (!stateDistances.ContainsKey(state.Id) && activeEntriesByStateId.ContainsKey(state.Id))
                {
                    warnings.Add($"rfs dag-quick-index-v1: active state '{state.Id}' was not assigned a distance to HEAD.");
                }

                activeEntriesByStateId.TryGetValue(state.Id, out var activeEntry);

                return new RckDagStateCandidate(
                    Id: state.Id,
                    ShortId: GetShortId(state.Id),
                    CreatedAtUtc: state.Meta.CreatedAtUtc,
                    AttachedAnchorIds: anchors?.Select(anchor => anchor.Id).OrderBy(id => id, StringComparer.Ordinal).ToArray() ?? Array.Empty<string>(),
                    IncomingDeltaIds: incomingDeltaIds?.OrderBy(deltaId => deltaId, StringComparer.Ordinal).ToArray() ?? Array.Empty<string>(),
                    OutgoingDeltaIds: outgoingDeltaIds?.OrderBy(deltaId => deltaId, StringComparer.Ordinal).ToArray() ?? Array.Empty<string>(),
                    DistanceToHead: TryGetDistance(stateDistances, state.Id),
                    Mode: activeEntry?.Mode,
                    PromptSummary: activeEntry?.Prompt,
                    AnswerSummary: activeEntry?.AnswerSummary);
            })
            .ToArray();
    }

    private static IReadOnlyList<RckDagDeltaCandidate> BuildDeltaCandidates(
        IReadOnlyList<RckWorkspaceContextPackDeltaObject> deltas,
        IReadOnlyDictionary<string, RckWorkspaceContextPackDeltaObject> deltasById,
        IReadOnlyDictionary<string, int> stateDistances,
        List<string> warnings)
    {
        return deltas
            .OrderBy(delta => delta.Id, StringComparer.Ordinal)
            .Select(delta =>
            {
                if (!deltasById.ContainsKey(delta.Id))
                {
                    warnings.Add($"rfs dag-quick-index-v1: delta '{delta.Id}' is missing from the lookup table.");
                }

                var operationSummary = SummarizeDeltaOps(delta);
                var evidenceSummary = SummarizeEvidence(delta);

                return new RckDagDeltaCandidate(
                    Id: delta.Id,
                    FromStateId: delta.FromStateId,
                    ToStateId: delta.ToStateId,
                    CreatedAtUtc: delta.Meta.CreatedAtUtc,
                    OperationSummary: operationSummary,
                    EvidenceSummary: evidenceSummary);
            })
            .ToArray();
    }

    private static IReadOnlyDictionary<string, int> BuildStateDistances(
        string headStateId,
        IReadOnlyDictionary<string, IReadOnlyList<string>> deltasByToStateId,
        IReadOnlyDictionary<string, RckWorkspaceContextPackDeltaObject> deltasById)
    {
        var distances = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            [headStateId] = 0,
        };

        var queue = new Queue<string>();
        queue.Enqueue(headStateId);

        while (queue.Count > 0)
        {
            var currentStateId = queue.Dequeue();
            var currentDistance = distances[currentStateId];

            if (!deltasByToStateId.TryGetValue(currentStateId, out var incomingDeltaIds))
            {
                continue;
            }

            foreach (var deltaId in incomingDeltaIds.OrderBy(id => id, StringComparer.Ordinal))
            {
                if (!deltasById.TryGetValue(deltaId, out var delta))
                {
                    continue;
                }

                var predecessorStateId = delta.FromStateId;
                if (distances.ContainsKey(predecessorStateId))
                {
                    continue;
                }

                distances[predecessorStateId] = currentDistance + 1;
                queue.Enqueue(predecessorStateId);
            }
        }

        return distances;
    }

    private static int? TryGetDistance(IReadOnlyDictionary<string, int> distances, string stateId)
        => distances.TryGetValue(stateId, out var distance) ? distance : null;

    private static string GetShortId(string id)
        => id.Length <= 7 ? id : id[..7];

    private static string? SummarizeDeltaOps(RckWorkspaceContextPackDeltaObject delta)
    {
        if (delta.Ops.Count == 0)
        {
            return null;
        }

        var parts = delta.Ops
            .Take(3)
            .Select(op => string.IsNullOrWhiteSpace(op.Path) ? op.Kind : $"{op.Kind}:{op.Path}")
            .ToArray();

        var summary = string.Join(", ", parts);
        if (delta.Ops.Count > parts.Length)
        {
            summary += $" (+{delta.Ops.Count - parts.Length} more)";
        }

        return summary;
    }

    private static string? SummarizeEvidence(RckWorkspaceContextPackDeltaObject delta)
    {
        if (delta.EvidenceRefs.Count == 0)
        {
            return null;
        }

        var summaries = delta.EvidenceRefs
            .Select(evidenceRef => evidenceRef.Summary)
            .Where(summary => !string.IsNullOrWhiteSpace(summary))
            .Take(3)
            .ToArray();

        if (summaries.Length == 0)
        {
            return null;
        }

        return string.Join("; ", summaries!);
    }
}

public sealed record RckDagQuickIndexV1BuildResult(
    bool Success,
    string? ErrorMessage,
    RckDagQuickIndexV1? DagQuickIndex,
    IReadOnlyList<string> Warnings)
{
    public static RckDagQuickIndexV1BuildResult Failure(string errorMessage)
        => new(false, errorMessage, null, Array.Empty<string>());

    public static RckDagQuickIndexV1BuildResult SuccessResult(RckDagQuickIndexV1 dagQuickIndex, IReadOnlyList<string> warnings)
        => new(true, null, dagQuickIndex, warnings);
}

public sealed record RckDagQuickIndexV1(
    string HeadStateId,
    IReadOnlyList<string> RecentStateIds,
    IReadOnlyList<string> RecentDeltaIds,
    IReadOnlyList<RckDagAnchorCandidate> Anchors,
    IReadOnlyList<RckDagStateCandidate> States,
    IReadOnlyList<RckDagDeltaCandidate> Deltas);

public sealed record RckDagAnchorCandidate(
    string Id,
    string StateId,
    string? Label,
    string? Reason,
    DateTimeOffset CreatedAtUtc,
    bool IsRecentChain,
    IReadOnlyList<string> ParentAnchorIds,
    int? DistanceToHead,
    IReadOnlyList<string> IncomingDeltaIds,
    IReadOnlyList<string> OutgoingDeltaIds);

public sealed record RckDagStateCandidate(
    string Id,
    string ShortId,
    DateTimeOffset CreatedAtUtc,
    IReadOnlyList<string> AttachedAnchorIds,
    IReadOnlyList<string> IncomingDeltaIds,
    IReadOnlyList<string> OutgoingDeltaIds,
    int? DistanceToHead,
    string? Mode,
    string? PromptSummary,
    string? AnswerSummary);

public sealed record RckDagDeltaCandidate(
    string Id,
    string FromStateId,
    string ToStateId,
    DateTimeOffset CreatedAtUtc,
    string? OperationSummary,
    string? EvidenceSummary);
