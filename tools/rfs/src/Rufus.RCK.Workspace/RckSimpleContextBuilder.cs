using System.Globalization;
using System.Text;

namespace Rufus.RCK.Workspace;

public static class RckSimpleContextBuilder
{
    public const int DefaultRecentInteractions = 5;
    public const int MinRecentInteractions = 1;
    public const int MaxRecentInteractions = 8;
    public const int MaxArtifactRefs = 20;
    public const int MaxAnchors = 3;
    public const int TargetChars = 16_000;
    public const int MaxChars = 24_000;
    public const int HardMaxChars = 32_000;
    public const int MaxRecentPromptExcerptChars = 220;
    public const int MaxRecentAnswerSummaryChars = 240;
    public const int MaxInteractionArtifactRefs = 5;

    public static RckSimpleContextBuildResult Build(string? repoRoot, string prompt)
    {
        var resolvedRoot = ResolveRepoRoot(repoRoot);
        var omissions = new List<string>();
        var warnings = new List<string>();

        var preparedPrompt = PreparePrompt(prompt, omissions);
        var logResult = RckWorkspaceLogReader.Read(resolvedRoot);
        if (!logResult.Success)
        {
            omissions.Add($"recent interactions unavailable: {logResult.ErrorMessage ?? "unknown log reader error"}");
        }

        var gitContext = GitWorkspaceContext.Capture(resolvedRoot);
        var recentInteractions = BuildRecentInteractions(resolvedRoot, logResult, omissions);
        var anchors = BuildAnchors(logResult, omissions);
        var artifacts = BuildArtifactRefs(resolvedRoot, gitContext.ChangedArtifacts, omissions);

        var context = new RckSimpleContext(
            RckSimpleContext.DefaultType,
            RckSimpleContext.DefaultSchemaVersion,
            preparedPrompt,
            new RckSimpleContextBudget(TargetChars, MaxChars, HardMaxChars, 0, 0, false),
            new RckSimpleContextGit(
                gitContext.Branch,
                gitContext.Commit,
                gitContext.Dirty,
                gitContext.ChangedArtifacts.Count,
                artifacts),
            recentInteractions,
            anchors,
            artifacts,
            omissions,
            new RckSimpleContextGuardrails(false, false, false, false, false, false, false, false));

        var promptToSend = BuildPromptToSend(context);
        var estimatedChars = promptToSend.Length;
        var estimatedTokens = EstimateTokens(estimatedChars);

        var truncated = estimatedChars > MaxChars || omissions.Count > 0;
        var budget = context.Budget with
        {
            EstimatedChars = estimatedChars,
            EstimatedTokens = estimatedTokens,
            Truncated = truncated,
        };

        context = context with { Budget = budget };
        promptToSend = BuildPromptToSend(context);

        estimatedChars = promptToSend.Length;
        estimatedTokens = EstimateTokens(estimatedChars);
        truncated = estimatedChars > MaxChars || omissions.Count > 0;
        budget = context.Budget with
        {
            EstimatedChars = estimatedChars,
            EstimatedTokens = estimatedTokens,
            Truncated = truncated,
        };

        context = context with { Budget = budget };
        promptToSend = BuildPromptToSend(context);

        estimatedChars = promptToSend.Length;
        estimatedTokens = EstimateTokens(estimatedChars);
        truncated = estimatedChars > MaxChars || omissions.Count > 0;
        budget = context.Budget with
        {
            EstimatedChars = estimatedChars,
            EstimatedTokens = estimatedTokens,
            Truncated = truncated,
        };

        context = context with { Budget = budget };
        promptToSend = BuildPromptToSend(context);

        return new RckSimpleContextBuildResult(context, promptToSend, omissions, warnings);
    }

    private static string ResolveRepoRoot(string? repoRoot)
    {
        if (!string.IsNullOrWhiteSpace(repoRoot))
        {
            return Path.GetFullPath(repoRoot);
        }

        return Directory.GetCurrentDirectory();
    }

    private static RckSimpleContextPrompt PreparePrompt(string prompt, List<string> omissions)
    {
        var trimmed = prompt.TrimEnd();
        if (trimmed.Length <= HardMaxChars)
        {
            return new RckSimpleContextPrompt(trimmed, false);
        }

        omissions.Add($"current prompt excerpted from {trimmed.Length} chars to {HardMaxChars} chars");
        return new RckSimpleContextPrompt(trimmed[..HardMaxChars], true);
    }

    private static IReadOnlyList<RckSimpleContextRecentInteraction> BuildRecentInteractions(
        string repoRoot,
        RckWorkspaceLogResult logResult,
        List<string> omissions)
    {
        if (!logResult.Success || logResult.Entries.Count == 0)
        {
            return Array.Empty<RckSimpleContextRecentInteraction>();
        }

        var maxCount = Math.Clamp(DefaultRecentInteractions, MinRecentInteractions, MaxRecentInteractions);
        var selected = logResult.Entries.Take(maxCount).ToArray();
        if (logResult.Entries.Count > selected.Length)
        {
            omissions.Add($"recent interactions truncated from {logResult.Entries.Count} to {selected.Length}");
        }

        var recentInteractions = new List<RckSimpleContextRecentInteraction>(selected.Length);
        foreach (var entry in selected)
        {
            var artifactRefs = BuildArtifactRefs(repoRoot, entry.Artifacts, omissions, MaxInteractionArtifactRefs, "interaction artifacts");
            recentInteractions.Add(new RckSimpleContextRecentInteraction(
                entry.StateId,
                entry.StateShortId,
                entry.DeltaId,
                entry.DeltaShortId,
                entry.Mode,
                new RckSimpleContextPrompt(Excerpt(entry.Prompt, MaxRecentPromptExcerptChars), true),
                Excerpt(entry.AnswerSummary, MaxRecentAnswerSummaryChars),
                entry.CreatedAtUtc,
                entry.GitCommit,
                artifactRefs));
        }

        return recentInteractions;
    }

    private static IReadOnlyList<RckSimpleContextAnchorRef> BuildAnchors(
        RckWorkspaceLogResult logResult,
        List<string> omissions)
    {
        if (!logResult.Success || logResult.Entries.Count == 0)
        {
            return Array.Empty<RckSimpleContextAnchorRef>();
        }

        var anchors = new List<RckSimpleContextAnchorRef>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var entry in logResult.Entries)
        {
            foreach (var anchor in entry.Anchors)
            {
                if (!seen.Add(anchor.Id))
                {
                    continue;
                }

                anchors.Add(new RckSimpleContextAnchorRef(
                    anchor.Id,
                    anchor.ShortId,
                    anchor.Label,
                    anchor.CreatedAtUtc,
                    entry.StateId,
                    entry.StateShortId));

                if (anchors.Count >= MaxAnchors)
                {
                    break;
                }
            }

            if (anchors.Count >= MaxAnchors)
            {
                break;
            }
        }

        if (anchors.Count == 0)
        {
            omissions.Add("no anchor refs available in recent log entries");
        }
        else if (logResult.Entries.SelectMany(entry => entry.Anchors).Count() > anchors.Count)
        {
            omissions.Add($"anchors truncated to {MaxAnchors}");
        }

        return anchors;
    }

    private static IReadOnlyList<RckSimpleContextArtifactRef> BuildArtifactRefs(
        string repoRoot,
        IReadOnlyList<GitWorkspaceArtifactChange> artifacts,
        List<string> omissions,
        int maxCount = MaxArtifactRefs,
        string? omissionPrefix = null)
    {
        if (artifacts.Count == 0)
        {
            return Array.Empty<RckSimpleContextArtifactRef>();
        }

        var selected = artifacts.Take(maxCount).ToArray();
        if (artifacts.Count > selected.Length)
        {
            omissions.Add($"{omissionPrefix ?? "artifacts"} truncated from {artifacts.Count} to {selected.Length}");
        }

        var refs = new List<RckSimpleContextArtifactRef>(selected.Length);
        foreach (var artifact in selected)
        {
            refs.Add(new RckSimpleContextArtifactRef(
                artifact.Path,
                artifact.GitStatus,
                artifact.Kind,
                TryGetArtifactSizeBytes(repoRoot, artifact.Path)));
        }

        return refs;
    }

    private static long? TryGetArtifactSizeBytes(string repoRoot, string artifactPath)
    {
        try
        {
            var fullPath = Path.GetFullPath(Path.Combine(repoRoot, artifactPath));
            if (!File.Exists(fullPath))
            {
                return null;
            }

            return new FileInfo(fullPath).Length;
        }
        catch
        {
            return null;
        }
    }

    private static string BuildPromptToSend(RckSimpleContext context)
    {
        var sb = new StringBuilder();
        sb.AppendLine("You are assisting inside an RFS repository session.");
        sb.AppendLine("Use the following Simple Context. It is metadata-only and may be incomplete.");
        sb.AppendLine("Do not assume file contents unless provided.");
        sb.AppendLine();
        sb.AppendLine("[SIMPLE CONTEXT]");
        sb.Append(context.Render());
        sb.AppendLine();
        sb.AppendLine("[USER PROMPT]");
        sb.AppendLine(context.Prompt.Text);
        sb.AppendLine();
        sb.AppendLine("Respond to the user with the next useful step or answer.");
        return sb.ToString();
    }

    private static int EstimateTokens(int chars)
        => (int)Math.Ceiling(chars / 4.0);

    private static string Excerpt(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized = value.ReplaceLineEndings(" ").Trim();
        if (normalized.Length <= maxLength)
        {
            return normalized;
        }

        return normalized[..Math.Max(0, maxLength - 1)] + "…";
    }
}
