namespace Rufus.Cli.Tui;

internal sealed class RfsTuiSessionState
{
    internal const string DefaultSessionModel = "gpt-5.4-mini";

    public string CurrentSessionModel { get; private set; } = DefaultSessionModel;

    public string? CurrentSessionModelProvider { get; private set; }

    /// <summary>
    /// The workspace-persisted default model (read from .rfs/config.json llm.defaultModel).
    /// Null when no workspace default is configured — the hardcoded <see cref="DefaultSessionModel"/> applies.
    /// </summary>
    public string? WorkspaceDefaultModel { get; internal set; }

    public string LastMode { get; private set; } = "none";

    public string LastContextKind { get; private set; } = "none";

    public RfsTuiLastInteractionContext? LastInteraction { get; private set; }

    public RfsTuiSimpleContextSummary? LastSimpleContext { get; private set; }

    public RfsTuiCompleteContextSummary? LastCompleteContext { get; private set; }

    public RfsTuiTraceSummary? LastTrace { get; private set; }

    /// <summary>
    /// Returns the baseline model for "is this a session override?" checks.
    /// When a workspace default is set, that is the baseline; otherwise the hardcoded constant.
    /// </summary>
    public string ResolveModelBaseline()
        => WorkspaceDefaultModel ?? DefaultSessionModel;

    public void ResetSessionModel()
    {
        CurrentSessionModel = WorkspaceDefaultModel ?? DefaultSessionModel;
        CurrentSessionModelProvider = null;
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

    public void SetSessionModel(string model, string? provider = null)
    {
        var trimmedModel = string.IsNullOrWhiteSpace(model)
            ? throw new ArgumentException("model cannot be empty.", nameof(model))
            : model.Trim();

        if (string.IsNullOrWhiteSpace(provider) && trimmedModel.Contains('/'))
        {
            var slashIndex = trimmedModel.IndexOf('/');
            provider = slashIndex > 0 && slashIndex < trimmedModel.Length - 1
                ? trimmedModel[..slashIndex].Trim()
                : null;
            trimmedModel = slashIndex > 0 && slashIndex < trimmedModel.Length - 1
                ? trimmedModel[(slashIndex + 1)..].Trim()
                : trimmedModel;
        }

        CurrentSessionModel = trimmedModel;
        CurrentSessionModelProvider = string.IsNullOrWhiteSpace(provider) ? null : provider.Trim();
    }

    public string ResolveMainModel()
        => string.IsNullOrWhiteSpace(CurrentSessionModelProvider)
            ? CurrentSessionModel
            : $"{CurrentSessionModelProvider}/{CurrentSessionModel}";

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
    IReadOnlyList<string> Omissions,
    string? ConversationalMemoryStatus = null,
    int ConversationalMemoryInteractionCount = 0,
    string? ConversationalMemoryModel = null,
    IReadOnlyList<string>? ConversationalMemoryWarnings = null);


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
