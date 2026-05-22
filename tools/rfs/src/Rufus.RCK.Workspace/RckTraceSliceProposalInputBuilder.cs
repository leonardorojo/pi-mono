namespace Rufus.RCK.Workspace;

public static class RckTraceSliceProposalInputBuilder
{
    public static RckTraceSliceProposalInputBuildResult Build(string? startingDirectory = null, int maxStates = 5)
    {
        if (maxStates < 1)
        {
            maxStates = 1;
        }

        var contextPack = RckWorkspaceContextPackReader.Read(startingDirectory);
        if (!contextPack.Success)
        {
            return RckTraceSliceProposalInputBuildResult.Failure(
                contextPack.ErrorMessage ?? "rfs trace-slice-proposal: failed to read RCK workspace state.");
        }

        if (string.IsNullOrWhiteSpace(contextPack.HeadStateId))
        {
            return RckTraceSliceProposalInputBuildResult.Failure("rfs trace-slice-proposal: HEAD state not available.");
        }

        var activeEntries = contextPack.ActiveChain.Take(maxStates).ToArray();
        var recentStateIds = activeEntries
            .Select(entry => entry.StateId)
            .Where(stateId => !string.IsNullOrWhiteSpace(stateId))
            .ToArray();
        var recentDeltaIds = activeEntries
            .Select(entry => entry.IncomingDeltaId)
            .Where(deltaId => !string.IsNullOrWhiteSpace(deltaId))
            .Select(deltaId => deltaId!)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        var anchorLookup = contextPack.Anchors.ToDictionary(anchor => anchor.Id, StringComparer.Ordinal);
        var anchors = new List<RckTraceSliceProposalAnchorMetadata>();
        var seenAnchorIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var activeEntry in activeEntries)
        {
            foreach (var anchorSummary in activeEntry.Anchors)
            {
                if (!seenAnchorIds.Add(anchorSummary.Id))
                {
                    continue;
                }

                if (anchorLookup.TryGetValue(anchorSummary.Id, out var anchor))
                {
                    anchors.Add(new RckTraceSliceProposalAnchorMetadata(
                        Id: anchor.Id,
                        StateId: anchor.StateId,
                        Label: anchor.Meta.Label,
                        Reason: anchor.Meta.Reason,
                        CreatedAtUtc: anchor.Meta.CreatedAtUtc.ToString("O"),
                        IsRecentChain: true));
                    continue;
                }

                anchors.Add(new RckTraceSliceProposalAnchorMetadata(
                    Id: anchorSummary.Id,
                    StateId: activeEntry.StateId,
                    Label: anchorSummary.Label,
                    Reason: null,
                    CreatedAtUtc: null,
                    IsRecentChain: true));
            }
        }

        return RckTraceSliceProposalInputBuildResult.SuccessResult(new RckTraceSliceProposalDagQuickIndex(
            HeadStateId: contextPack.HeadStateId,
            RecentStateIds: recentStateIds,
            RecentDeltaIds: recentDeltaIds,
            Anchors: anchors));
    }
}

public sealed record RckTraceSliceProposalInputBuildResult(
    bool Success,
    string? ErrorMessage,
    RckTraceSliceProposalDagQuickIndex? DagQuickIndex)
{
    public static RckTraceSliceProposalInputBuildResult Failure(string errorMessage)
        => new(false, errorMessage, null);

    public static RckTraceSliceProposalInputBuildResult SuccessResult(RckTraceSliceProposalDagQuickIndex dagQuickIndex)
        => new(true, null, dagQuickIndex);
}

public sealed record RckTraceSliceProposalDagQuickIndex(
    string HeadStateId,
    IReadOnlyList<string> RecentStateIds,
    IReadOnlyList<string> RecentDeltaIds,
    IReadOnlyList<RckTraceSliceProposalAnchorMetadata> Anchors);

public sealed record RckTraceSliceProposalAnchorMetadata(
    string Id,
    string StateId,
    string? Label,
    string? Reason,
    string? CreatedAtUtc,
    bool IsRecentChain);
