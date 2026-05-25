namespace Rufus.Cli.Tui;

internal sealed class RfsTuiSessionState
{
    internal const string DefaultSessionModel = "gpt-5.4-mini";

    public string CurrentSessionModel { get; private set; } = DefaultSessionModel;

    public string LastMode { get; private set; } = "none";

    public string LastContextKind { get; private set; } = "none";

    public RfsTuiLastInteractionContext? LastInteraction { get; private set; }

    public RfsTuiSimpleContextSummary? LastSimpleContext { get; private set; }

    public RfsTuiCompleteContextSummary? LastCompleteContext { get; private set; }

    public RfsTuiTraceSummary? LastTrace { get; private set; }

    public void ResetSessionModel()
    {
        CurrentSessionModel = DefaultSessionModel;
    }

    public void ResetInteraction()
    {
        LastMode = "none";
        LastContextKind = "none";
        LastInteraction = null;
        LastSimpleContext = null;
        LastCompleteContext = null;
        LastTrace = null;
    }

    public void SetSessionModel(string model)
    {
        var trimmedModel = string.IsNullOrWhiteSpace(model)
            ? throw new ArgumentException("model cannot be empty.", nameof(model))
            : model.Trim();

        CurrentSessionModel = trimmedModel;
    }

    public string ResolveMainModel()
        => CurrentSessionModel;

    public void RecordDirect(string prompt, string answer)
    {
        LastMode = "direct";
        LastContextKind = "none";
        LastInteraction = new RfsTuiLastInteractionContext("direct", prompt, answer, null, null);
        LastSimpleContext = null;
        LastCompleteContext = null;
        LastTrace = null;
    }

    public void RecordSimple(RfsTuiSimpleContextSummary summary, string mode, string prompt, string answer)
    {
        LastMode = mode;
        LastContextKind = "simple";
        LastInteraction = new RfsTuiLastInteractionContext(mode, prompt, answer, summary, null);
        LastSimpleContext = summary;
        LastCompleteContext = null;
        LastTrace = null;
    }

    public void RecordComplete(RfsTuiCompleteContextSummary summary, string prompt, string answer)
    {
        LastMode = "complete";
        LastContextKind = "complete";
        LastInteraction = new RfsTuiLastInteractionContext("complete", prompt, answer, null, summary);
        LastCompleteContext = summary;
        LastTrace = RfsTuiTraceSummary.Create(summary);
        LastSimpleContext = null;
    }

    public void RecordPlan(RfsTuiSimpleContextSummary summary, string prompt, string answer)
    {
        LastMode = "plan";
        LastContextKind = "simple";
        LastInteraction = new RfsTuiLastInteractionContext("plan", prompt, answer, summary, null);
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
    string? IntentSource,
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

internal sealed record RfsTuiLastInteractionContext(
    string Mode,
    string Prompt,
    string Answer,
    RfsTuiSimpleContextSummary? SimpleContext,
    RfsTuiCompleteContextSummary? CompleteContext)
{
    public bool HasCompleteContextPack => CompleteContext is not null;
}
