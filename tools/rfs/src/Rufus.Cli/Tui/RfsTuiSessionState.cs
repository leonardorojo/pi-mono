namespace Rufus.Cli.Tui;

internal sealed class RfsTuiSessionState
{
    public string LastMode { get; private set; } = "none";

    public string LastContextKind { get; private set; } = "none";

    public RfsTuiSimpleContextSummary? LastSimpleContext { get; private set; }

    public RfsTuiCompleteContextSummary? LastCompleteContext { get; private set; }

    public RfsTuiTraceSummary? LastTrace { get; private set; }

    public void RecordDirect()
    {
        LastMode = "direct";
        LastContextKind = "none";
        LastSimpleContext = null;
        LastCompleteContext = null;
        LastTrace = null;
    }

    public void RecordSimple(RfsTuiSimpleContextSummary summary, string mode)
    {
        LastMode = mode;
        LastContextKind = "simple";
        LastSimpleContext = summary;
        LastCompleteContext = null;
        LastTrace = null;
    }

    public void RecordComplete(RfsTuiCompleteContextSummary summary)
    {
        LastMode = "complete";
        LastContextKind = "complete";
        LastCompleteContext = summary;
        LastTrace = RfsTuiTraceSummary.Create(summary);
        LastSimpleContext = null;
    }

    public void RecordPlan(RfsTuiSimpleContextSummary summary)
    {
        LastMode = "plan";
        LastContextKind = "simple";
        LastSimpleContext = summary;
        LastCompleteContext = null;
        LastTrace = null;
    }
}

internal sealed record RfsTuiSimpleContextSummary(
    int RecentInteractions,
    int Anchors,
    int Artifacts,
    int EstimatedChars,
    int EstimatedTokens,
    string ModelBudget,
    string ContextUsage,
    int TransportSizeChars,
    string TransportRisk,
    bool Truncated,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<string> Omissions);

internal sealed record RfsTuiCompleteContextSummary(
    string? SelectionStrategy,
    string? ValidationStatus,
    string? ContextPackScope,
    int SelectedStateCount,
    int SelectedDeltaCount,
    int SelectedAnchorCount,
    int EstimatedChars,
    int EstimatedTokens,
    string TransportRisk,
    bool Truncated,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<string> Omissions);

internal sealed record RfsTuiTraceSummary(
    string? SelectionStrategy,
    string? ValidationStatus,
    int SelectedStateCount,
    int SelectedDeltaCount,
    int SelectedAnchorCount,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<string> Omissions)
{
    public static RfsTuiTraceSummary Create(RfsTuiCompleteContextSummary summary)
        => new(
            summary.SelectionStrategy,
            summary.ValidationStatus,
            summary.SelectedStateCount,
            summary.SelectedDeltaCount,
            summary.SelectedAnchorCount,
            summary.Warnings,
            summary.Omissions);
}
