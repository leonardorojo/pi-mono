namespace Rufus.RCK.Core.Tracing;

public static class RckTraceValidationIssueCodes
{
    public const string MissingDeltaFromState = "missing_delta_from_state";
    public const string MissingDeltaToState = "missing_delta_to_state";
    public const string MissingAnchorState = "missing_anchor_state";
    public const string MissingAnchorParent = "missing_anchor_parent";
    public const string DeltaCycleDetected = "delta_cycle_detected";
    public const string AnchorParentCycleDetected = "anchor_parent_cycle_detected";
}
