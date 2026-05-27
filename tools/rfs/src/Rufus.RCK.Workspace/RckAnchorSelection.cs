namespace Rufus.RCK.Workspace;

public sealed record RckAnchorSelection(
    IReadOnlyList<string> SelectedAnchorIds,
    string FallbackStrategy,
    IReadOnlyList<RckAnchorSelectionRationale> Rationale,
    IReadOnlyList<string> Warnings,
    double Confidence,
    bool RequestedRecentChainFallback = false);

public sealed record RckAnchorSelectionRationale(
    string Target,
    string Reason);

