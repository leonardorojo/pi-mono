namespace Rufus.RCK.Workspace;

public static class RckConversationalMemoryInputBuilder
{
    public static RckConversationalMemoryInputBuildResult Build(
        string? repoRoot,
        string currentPrompt,
        RckConversationalMemoryLimits limits)
    {
        ArgumentNullException.ThrowIfNull(currentPrompt);

        var resolvedRoot = ResolveRepoRoot(repoRoot);
        var logResult = RckWorkspaceLogReader.Read(resolvedRoot);
        if (!logResult.Success)
        {
            return RckConversationalMemoryInputBuildResult.Failure(
                logResult.ErrorMessage ?? "rfs conversational-memory: failed to read RCK workspace state.");
        }

        var warnings = new List<string>();
        var recentInteractions = BuildRecentInteractions(logResult, limits, warnings);

        if (recentInteractions.Count == 0)
        {
            warnings.Add("no-recent-interactions");
        }

        var input = new RckConversationalMemoryInput(
            CurrentPrompt: currentPrompt,
            RecentInteractions: recentInteractions,
            Limits: NormalizeLimits(limits));

        return RckConversationalMemoryInputBuildResult.SuccessResult(input, warnings);
    }

    private static string ResolveRepoRoot(string? repoRoot)
    {
        if (!string.IsNullOrWhiteSpace(repoRoot))
        {
            return Path.GetFullPath(repoRoot);
        }

        return Directory.GetCurrentDirectory();
    }

    private static RckConversationalMemoryLimits NormalizeLimits(RckConversationalMemoryLimits limits)
    {
        return new RckConversationalMemoryLimits(
            MaxInteractions: Math.Max(0, limits.MaxInteractions),
            MaxPromptChars: Math.Max(0, limits.MaxPromptChars),
            MaxTotalChars: Math.Max(0, limits.MaxTotalChars));
    }

    private static IReadOnlyList<RckConversationalMemoryInteraction> BuildRecentInteractions(
        RckWorkspaceLogResult logResult,
        RckConversationalMemoryLimits limits,
        List<string> warnings)
    {
        var normalizedLimits = NormalizeLimits(limits);
        if (logResult.Entries.Count == 0 || normalizedLimits.MaxInteractions == 0 || normalizedLimits.MaxTotalChars == 0)
        {
            if (logResult.Entries.Count > 0 && normalizedLimits.MaxInteractions == 0)
            {
                warnings.Add("recent-interactions-truncated");
            }

            if (logResult.Entries.Count > 0 && normalizedLimits.MaxTotalChars == 0)
            {
                warnings.Add("total-budget-truncated");
            }

            return Array.Empty<RckConversationalMemoryInteraction>();
        }

        var selected = new List<RckConversationalMemoryInteraction>();
        var totalChars = 0;
        var conversationEntries = logResult.Entries.Where(IsConversationEntry).ToArray();

        if (conversationEntries.Length == 0)
        {
            return Array.Empty<RckConversationalMemoryInteraction>();
        }

        foreach (var entry in conversationEntries)
        {
            if (selected.Count >= normalizedLimits.MaxInteractions)
            {
                warnings.Add("recent-interactions-truncated");
                break;
            }

            if (string.IsNullOrWhiteSpace(entry.StateId))
            {
                continue;
            }

            var prompt = entry.Prompt ?? string.Empty;
            if (prompt.Length > normalizedLimits.MaxPromptChars)
            {
                prompt = prompt[..normalizedLimits.MaxPromptChars];
                warnings.Add("prompt-truncated");
            }

            var answerSummary = entry.AnswerSummary ?? string.Empty;
            if (string.IsNullOrWhiteSpace(entry.AnswerSummary))
            {
                warnings.Add("missing-answer-summary");
            }

            var interactionChars = prompt.Length + answerSummary.Length;
            if (totalChars + interactionChars > normalizedLimits.MaxTotalChars)
            {
                warnings.Add("total-budget-truncated");
                break;
            }

            selected.Add(new RckConversationalMemoryInteraction(
                StateId: entry.StateId,
                DeltaId: string.IsNullOrWhiteSpace(entry.DeltaId) ? null : entry.DeltaId,
                Mode: string.IsNullOrWhiteSpace(entry.Mode) ? "unknown" : entry.Mode,
                Prompt: prompt,
                AnswerSummary: answerSummary,
                CreatedAtUtc: entry.CreatedAtUtc));
            totalChars += interactionChars;
        }

        return selected;
    }

    private static bool IsConversationEntry(RckWorkspaceLogEntry entry)
    {
        if (string.Equals(entry.Mode, "genesis", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return !string.IsNullOrWhiteSpace(entry.StateId)
            && (!string.IsNullOrWhiteSpace(entry.Prompt) || !string.IsNullOrWhiteSpace(entry.AnswerSummary) || !string.IsNullOrWhiteSpace(entry.DeltaId));
    }
}

public sealed record RckConversationalMemoryInput(
    string CurrentPrompt,
    IReadOnlyList<RckConversationalMemoryInteraction> RecentInteractions,
    RckConversationalMemoryLimits Limits);

public sealed record RckConversationalMemoryInteraction(
    string StateId,
    string? DeltaId,
    string Mode,
    string Prompt,
    string AnswerSummary,
    DateTimeOffset CreatedAtUtc);

public sealed record RckConversationalMemoryLimits(
    int MaxInteractions,
    int MaxPromptChars,
    int MaxTotalChars);

public sealed record RckConversationalMemoryInputBuildResult(
    bool Success,
    string? ErrorMessage,
    RckConversationalMemoryInput? Input,
    IReadOnlyList<string> Warnings)
{
    public static RckConversationalMemoryInputBuildResult Failure(string errorMessage)
        => new(false, errorMessage, null, Array.Empty<string>());

    public static RckConversationalMemoryInputBuildResult SuccessResult(RckConversationalMemoryInput input, IReadOnlyList<string> warnings)
        => new(true, null, input, warnings);
}
