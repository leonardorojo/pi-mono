namespace Rufus.RCK.Workspace;

public static class RckAnchorExpansionService
{
    public static RckAnchorExpansionResult Expand(RckAnchorExpansionRequest request)
    {
        if (request.QuickIndex is null)
        {
            return RckAnchorExpansionResult.Failure("rfs anchor-expansion: quick index is required.");
        }

        if (request.MaxStates < 1)
        {
            return RckAnchorExpansionResult.Failure("rfs anchor-expansion: maxStates must be greater than zero.");
        }

        if (request.MaxDeltas < 1)
        {
            return RckAnchorExpansionResult.Failure("rfs anchor-expansion: maxDeltas must be greater than zero.");
        }

        var quickIndex = request.QuickIndex;
        var policy = request.Policy;
        var warnings = new List<string>();
        var evidence = new List<RckAnchorExpansionEvidence>();
        var anchorIds = new List<string>();
        var stateIds = new List<string>();
        var deltaIds = new List<string>();

        var anchorsById = quickIndex.Anchors
            .OrderBy(anchor => anchor.Id, StringComparer.Ordinal)
            .ToDictionary(anchor => anchor.Id, StringComparer.Ordinal);
        var statesById = quickIndex.States
            .OrderBy(state => state.Id, StringComparer.Ordinal)
            .ToDictionary(state => state.Id, StringComparer.Ordinal);
        var deltasById = quickIndex.Deltas
            .OrderBy(delta => delta.Id, StringComparer.Ordinal)
            .ToDictionary(delta => delta.Id, StringComparer.Ordinal);
        var stateOrder = BuildStateOrder(quickIndex.States);

        var selectedAnchorIds = DedupeInOrder(request.SelectedAnchorIds);
        var validSelectedAnchorCount = 0;
        var validSelectedAnchorStateCount = 0;

        if (selectedAnchorIds.Count == 0)
        {
            return ExpandRecentChainFallback(
                quickIndex,
                request.MaxStates,
                request.MaxDeltas,
                warnings,
                evidence,
                anchorIds,
                stateIds,
                deltaIds,
                reason: "no anchors selected and fallback disabled",
                fallbackEnabled: policy.FallbackToRecentChain);
        }

        var selectedAnchorHadMissingShape = false;
        foreach (var selectedAnchorId in selectedAnchorIds)
        {
            if (!anchorsById.TryGetValue(selectedAnchorId, out var anchor))
            {
                warnings.Add($"rfs anchor-expansion: missing anchor '{selectedAnchorId}'.");
                evidence.Add(new RckAnchorExpansionEvidence(
                    Kind: "missing-anchor",
                    SourceId: selectedAnchorId,
                    TargetId: null,
                    Reason: "selected anchor was not found in quick index"));
                selectedAnchorHadMissingShape = true;
                continue;
            }

            validSelectedAnchorCount++;
            AddAnchor(anchorIds, evidence, anchor.Id, anchor.Id, "selected-anchor", "selected anchor", null);

            if (policy.IncludeParentAnchorLineage)
            {
                AddParentLineage(anchor, anchorsById, anchorIds, evidence, warnings);
            }

            if (!string.IsNullOrWhiteSpace(anchor.StateId) && statesById.ContainsKey(anchor.StateId))
            {
                validSelectedAnchorStateCount++;
                if (!stateIds.Contains(anchor.StateId, StringComparer.Ordinal))
                {
                    if (stateIds.Count >= request.MaxStates)
                    {
                        warnings.Add($"rfs anchor-expansion: truncated at maxStates={request.MaxStates}.");
                        evidence.Add(new RckAnchorExpansionEvidence(
                            Kind: "truncated-max-states",
                            SourceId: anchor.Id,
                            TargetId: anchor.StateId,
                            Reason: $"maxStates={request.MaxStates}"));
                    }
                    else
                    {
                        stateIds.Add(anchor.StateId);
                        evidence.Add(new RckAnchorExpansionEvidence(
                            Kind: "anchor-state",
                            SourceId: anchor.Id,
                            TargetId: anchor.StateId,
                            Reason: "anchor state"));
                    }
                }

                if (policy.IncludeIncomingDeltas)
                {
                    AddStateDeltas(
                        anchor.StateId,
                        quickIndex,
                        deltasById,
                        request.MaxDeltas,
                        deltaIds,
                        evidence,
                        warnings,
                        sourceId: anchor.Id,
                        sourceKind: "incoming-delta",
                        stateIds,
                        statesById,
                        stateOrder,
                        incoming: true,
                        includeNeighborStates: policy.IncludeNeighborStates,
                        maxStates: request.MaxStates);
                }

                if (policy.IncludeOutgoingDeltas)
                {
                    AddStateDeltas(
                        anchor.StateId,
                        quickIndex,
                        deltasById,
                        request.MaxDeltas,
                        deltaIds,
                        evidence,
                        warnings,
                        sourceId: anchor.Id,
                        sourceKind: "outgoing-delta",
                        stateIds,
                        statesById,
                        stateOrder,
                        incoming: false,
                        includeNeighborStates: policy.IncludeNeighborStates,
                        maxStates: request.MaxStates);
                }
            }
            else
            {
                warnings.Add($"rfs anchor-expansion: anchor '{anchor.Id}' points to missing state '{anchor.StateId}'.");
                evidence.Add(new RckAnchorExpansionEvidence(
                    Kind: "missing-anchor-state",
                    SourceId: anchor.Id,
                    TargetId: anchor.StateId,
                    Reason: "anchor state was not found in quick index"));
            }
        }

        if (validSelectedAnchorStateCount == 0)
        {
            if (policy.FallbackToRecentChain)
            {
                return ExpandRecentChainFallback(
                    quickIndex,
                    request.MaxStates,
                    request.MaxDeltas,
                    warnings,
                    evidence,
                    anchorIds,
                    stateIds,
                    deltaIds,
                    reason: selectedAnchorHadMissingShape ? "selected anchors were missing or invalid" : "no selected anchors produced a valid state",
                    fallbackEnabled: true);
            }

            return RckAnchorExpansionResult.SuccessResult(
                Strategy: validSelectedAnchorCount > 0 ? "partial-anchor-guided" : "empty",
                AnchorIds: anchorIds.Distinct(StringComparer.Ordinal).ToArray(),
                StateIds: stateIds.Distinct(StringComparer.Ordinal).ToArray(),
                DeltaIds: deltaIds.Distinct(StringComparer.Ordinal).ToArray(),
                Warnings: warnings,
                ExpansionEvidence: evidence);
        }

        if (selectedAnchorHadMissingShape)
        {
            warnings.Add("rfs anchor-expansion: partially expanded anchors; missing anchors were skipped.");
        }

        return RckAnchorExpansionResult.SuccessResult(
            Strategy: selectedAnchorHadMissingShape ? "partial-anchor-guided" : "anchor-guided",
            AnchorIds: anchorIds.Distinct(StringComparer.Ordinal).ToArray(),
            StateIds: stateIds.Distinct(StringComparer.Ordinal).ToArray(),
            DeltaIds: deltaIds.Distinct(StringComparer.Ordinal).ToArray(),
            Warnings: warnings,
            ExpansionEvidence: evidence);
    }

    private static RckAnchorExpansionResult ExpandRecentChainFallback(
        RckDagQuickIndexV1 quickIndex,
        int maxStates,
        int maxDeltas,
        List<string> warnings,
        List<RckAnchorExpansionEvidence> evidence,
        List<string> anchorIds,
        List<string> stateIds,
        List<string> deltaIds,
        string reason,
        bool fallbackEnabled)
    {
        if (!fallbackEnabled)
        {
            warnings.Add("rfs anchor-expansion: no anchors selected and fallback disabled.");
            evidence.Add(new RckAnchorExpansionEvidence(
                Kind: "empty",
                SourceId: "selection",
                TargetId: null,
                Reason: reason));
            return RckAnchorExpansionResult.SuccessResult(
                Strategy: "empty",
                AnchorIds: Array.Empty<string>(),
                StateIds: Array.Empty<string>(),
                DeltaIds: Array.Empty<string>(),
                Warnings: warnings,
                ExpansionEvidence: evidence);
        }

        warnings.Add($"rfs anchor-expansion: {reason}; using recent chain fallback.");
        evidence.Add(new RckAnchorExpansionEvidence(
            Kind: "recent-chain-fallback",
            SourceId: "selection",
            TargetId: null,
            Reason: reason));

        foreach (var stateId in quickIndex.RecentStateIds)
        {
            if (stateIds.Count >= maxStates)
            {
                warnings.Add($"rfs anchor-expansion: truncated recent chain at maxStates={maxStates}.");
                evidence.Add(new RckAnchorExpansionEvidence(
                    Kind: "truncated-max-states",
                    SourceId: "recent-chain-fallback",
                    TargetId: stateId,
                    Reason: $"maxStates={maxStates}"));
                break;
            }

            if (stateIds.Contains(stateId, StringComparer.Ordinal))
            {
                continue;
            }

            stateIds.Add(stateId);
            evidence.Add(new RckAnchorExpansionEvidence(
                Kind: "recent-chain-state",
                SourceId: "recent-chain-fallback",
                TargetId: stateId,
                Reason: "recent chain state"));
        }

        foreach (var deltaId in quickIndex.RecentDeltaIds)
        {
            if (deltaIds.Count >= maxDeltas)
            {
                warnings.Add($"rfs anchor-expansion: truncated recent chain at maxDeltas={maxDeltas}.");
                evidence.Add(new RckAnchorExpansionEvidence(
                    Kind: "truncated-max-deltas",
                    SourceId: "recent-chain-fallback",
                    TargetId: deltaId,
                    Reason: $"maxDeltas={maxDeltas}"));
                break;
            }

            if (deltaIds.Contains(deltaId, StringComparer.Ordinal))
            {
                continue;
            }

            deltaIds.Add(deltaId);
            evidence.Add(new RckAnchorExpansionEvidence(
                Kind: "recent-chain-delta",
                SourceId: "recent-chain-fallback",
                TargetId: deltaId,
                Reason: "recent chain delta"));
        }

        return RckAnchorExpansionResult.SuccessResult(
            Strategy: "recent-chain-fallback",
            AnchorIds: Array.Empty<string>(),
            StateIds: stateIds.ToArray(),
            DeltaIds: deltaIds.ToArray(),
            Warnings: warnings,
            ExpansionEvidence: evidence);
    }

    private static void AddParentLineage(
        RckDagAnchorCandidate anchor,
        IReadOnlyDictionary<string, RckDagAnchorCandidate> anchorsById,
        List<string> anchorIds,
        List<RckAnchorExpansionEvidence> evidence,
        List<string> warnings)
    {
        foreach (var parentId in anchor.ParentAnchorIds.OrderBy(id => id, StringComparer.Ordinal))
        {
            if (!anchorsById.TryGetValue(parentId, out var parentAnchor))
            {
                warnings.Add($"rfs anchor-expansion: missing parent anchor '{parentId}' referenced by '{anchor.Id}'.");
                continue;
            }

            if (anchorIds.Contains(parentAnchor.Id, StringComparer.Ordinal))
            {
                continue;
            }

            anchorIds.Add(parentAnchor.Id);
            evidence.Add(new RckAnchorExpansionEvidence(
                Kind: "parent-anchor-lineage",
                SourceId: anchor.Id,
                TargetId: parentAnchor.Id,
                Reason: "included direct parent anchor lineage"));
        }
    }

    private static void AddStateDeltas(
        string stateId,
        RckDagQuickIndexV1 quickIndex,
        IReadOnlyDictionary<string, RckDagDeltaCandidate> deltasById,
        int maxDeltas,
        List<string> deltaIds,
        List<RckAnchorExpansionEvidence> evidence,
        List<string> warnings,
        string sourceId,
        string sourceKind,
        List<string> stateIds,
        IReadOnlyDictionary<string, RckDagStateCandidate> statesById,
        IReadOnlyDictionary<string, int> stateOrder,
        bool incoming,
        bool includeNeighborStates,
        int maxStates)
    {
        var stateCandidate = statesById[stateId];
        var deltaSourceIds = incoming ? stateCandidate.IncomingDeltaIds : stateCandidate.OutgoingDeltaIds;

        foreach (var deltaId in deltaSourceIds)
        {
            if (!deltasById.TryGetValue(deltaId, out var delta))
            {
                warnings.Add($"rfs anchor-expansion: delta '{deltaId}' referenced by state '{stateId}' was missing from quick index.");
                evidence.Add(new RckAnchorExpansionEvidence(
                    Kind: incoming ? "missing-incoming-delta" : "missing-outgoing-delta",
                    SourceId: stateId,
                    TargetId: deltaId,
                    Reason: "delta missing from quick index"));
                continue;
            }

            if (deltaIds.Contains(delta.Id, StringComparer.Ordinal))
            {
                continue;
            }

            if (deltaIds.Count >= maxDeltas)
            {
                warnings.Add($"rfs anchor-expansion: truncated at maxDeltas={maxDeltas}.");
                evidence.Add(new RckAnchorExpansionEvidence(
                    Kind: "truncated-max-deltas",
                    SourceId: sourceId,
                    TargetId: delta.Id,
                    Reason: $"maxDeltas={maxDeltas}"));
                return;
            }

            deltaIds.Add(delta.Id);
            evidence.Add(new RckAnchorExpansionEvidence(
                Kind: sourceKind,
                SourceId: sourceId,
                TargetId: delta.Id,
                Reason: incoming ? "incoming delta" : "outgoing delta"));

            if (!includeNeighborStates)
            {
                continue;
            }

            AddNeighborState(delta.FromStateId, stateIds, statesById, evidence, warnings, maxStates, delta.Id, "neighbor-state-from");
            AddNeighborState(delta.ToStateId, stateIds, statesById, evidence, warnings, maxStates, delta.Id, "neighbor-state-to");
        }
    }

    private static void AddNeighborState(
        string stateId,
        List<string> stateIds,
        IReadOnlyDictionary<string, RckDagStateCandidate> statesById,
        List<RckAnchorExpansionEvidence> evidence,
        List<string> warnings,
        int maxStates,
        string sourceDeltaId,
        string kind)
    {
        if (!statesById.ContainsKey(stateId))
        {
            warnings.Add($"rfs anchor-expansion: neighbor state '{stateId}' referenced by delta '{sourceDeltaId}' is missing from quick index.");
            evidence.Add(new RckAnchorExpansionEvidence(
                Kind: "missing-neighbor-state",
                SourceId: sourceDeltaId,
                TargetId: stateId,
                Reason: "neighbor state missing from quick index"));
            return;
        }

        if (stateIds.Contains(stateId, StringComparer.Ordinal))
        {
            return;
        }

        if (stateIds.Count >= maxStates)
        {
            warnings.Add($"rfs anchor-expansion: truncated at maxStates={maxStates}.");
            evidence.Add(new RckAnchorExpansionEvidence(
                Kind: "truncated-max-states",
                SourceId: sourceDeltaId,
                TargetId: stateId,
                Reason: $"maxStates={maxStates}"));
            return;
        }

        stateIds.Add(stateId);
        evidence.Add(new RckAnchorExpansionEvidence(
            Kind: kind,
            SourceId: sourceDeltaId,
            TargetId: stateId,
            Reason: "neighbor state"));
    }

    private static void AddAnchor(
        List<string> anchorIds,
        List<RckAnchorExpansionEvidence> evidence,
        string sourceId,
        string targetId,
        string kind,
        string reason,
        string? extraReason)
    {
        if (anchorIds.Contains(targetId, StringComparer.Ordinal))
        {
            return;
        }

        anchorIds.Add(targetId);
        evidence.Add(new RckAnchorExpansionEvidence(
            Kind: kind,
            SourceId: sourceId,
            TargetId: targetId,
            Reason: extraReason is null ? reason : $"{reason}: {extraReason}"));
    }

    private static List<string> DedupeInOrder(IReadOnlyList<string>? values)
    {
        var result = new List<string>();
        if (values is null)
        {
            return result;
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var value in values)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            if (seen.Add(value))
            {
                result.Add(value);
            }
        }

        return result;
    }

    private static IReadOnlyDictionary<string, int> BuildStateOrder(IReadOnlyList<RckDagStateCandidate> states)
    {
        var result = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var index = 0; index < states.Count; index++)
        {
            result[states[index].Id] = index;
        }

        return result;
    }
}

public sealed record RckAnchorExpansionRequest(
    IReadOnlyList<string> SelectedAnchorIds,
    RckDagQuickIndexV1 QuickIndex,
    int MaxStates,
    int MaxDeltas,
    RckAnchorExpansionPolicy Policy);

public sealed record RckAnchorExpansionPolicy(
    bool IncludeIncomingDeltas = true,
    bool IncludeOutgoingDeltas = true,
    bool IncludeNeighborStates = true,
    bool FallbackToRecentChain = true,
    bool IncludeParentAnchorLineage = false,
    bool DeterministicOrdering = true);

public sealed record RckAnchorExpansionResult(
    bool Success,
    string? ErrorMessage,
    string Strategy,
    IReadOnlyList<string> AnchorIds,
    IReadOnlyList<string> StateIds,
    IReadOnlyList<string> DeltaIds,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<RckAnchorExpansionEvidence> ExpansionEvidence)
{
    public static RckAnchorExpansionResult Failure(string errorMessage)
        => new(false, errorMessage, "empty", Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>(), Array.Empty<RckAnchorExpansionEvidence>());

    public static RckAnchorExpansionResult SuccessResult(
        string Strategy,
        IReadOnlyList<string> AnchorIds,
        IReadOnlyList<string> StateIds,
        IReadOnlyList<string> DeltaIds,
        IReadOnlyList<string> Warnings,
        IReadOnlyList<RckAnchorExpansionEvidence> ExpansionEvidence)
        => new(true, null, Strategy, AnchorIds, StateIds, DeltaIds, Warnings, ExpansionEvidence);
}

public sealed record RckAnchorExpansionEvidence(
    string Kind,
    string SourceId,
    string? TargetId,
    string Reason);
