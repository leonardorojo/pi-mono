using System.Text;
using Rufus.RCK.Workspace;

namespace Rufus.Cli.Tui;

internal static class RfsTuiOperationalHandoffPromptBuilder
{
    internal static string BuildContextSummary(RfsTuiLastInteractionContext lastInteraction)
    {
        if (lastInteraction.CompleteContext is null)
        {
            return string.Empty;
        }

        var complete = lastInteraction.CompleteContext;
        var sb = new StringBuilder();
        sb.AppendLine("ContextPack summary:");
        sb.AppendLine($"- validation: {complete.ValidationStatus ?? "(unknown)"}");
        sb.AppendLine($"- selection: {complete.SelectionStrategy ?? "(unknown)"}");
        sb.AppendLine($"- scope: {complete.ContextPackScope ?? "(unknown)"}");
        sb.AppendLine($"- intent source: {complete.IntentSource ?? "(unknown)"}");
        sb.AppendLine($"- selected states/deltas/anchors: {complete.SelectedStateCount} / {complete.SelectedDeltaCount} / {complete.SelectedAnchorCount}");
        sb.AppendLine($"- estimated tokens: {complete.EstimatedTokens:N0}");
        sb.AppendLine($"- transport risk: {complete.TransportRisk}");
        sb.AppendLine($"- truncated: {complete.Truncated.ToString().ToLowerInvariant()}");

        if (complete.Warnings.Count > 0)
        {
            sb.AppendLine($"- warnings: {string.Join("; ", complete.Warnings)}");
        }

        if (complete.Omissions.Count > 0)
        {
            sb.AppendLine($"- omissions: {string.Join("; ", complete.Omissions)}");
        }

        return sb.ToString().TrimEnd();
    }

    internal static string BuildExecutionDirective() => string.Join(Environment.NewLine, new[]
    {
        "Execution directive:",
        "The operational instruction below is the main task you must execute now.",
        "Do not rewrite it.",
        "Do not summarize it.",
        "Do not return the instruction as your answer.",
        "Execute it using the available tools and runtime.",
        "Use read-only filesystem/git inspection when the task requires repo evidence.",
        "Return factual results backed by evidence.",
        "If a requested action cannot be executed, explain why and report what evidence is missing.",
    });

    internal static string BuildObjective() =>
        "Execute the operational instruction above under the restrictions below and return an evidence-based result.";

    internal static string BuildOriginalUserRequestSection(string originalPrompt)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Original user request, for context only:");
        AppendIndentedBlock(sb, originalPrompt.Trim());
        return sb.ToString().TrimEnd();
    }

    internal static string BuildOperationalInstructionSection(string previousAnswer)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Operational instruction to execute:");
        AppendIndentedBlock(sb, previousAnswer.Trim());
        return sb.ToString().TrimEnd();
    }

    internal static IReadOnlyList<string> BuildRestrictions(string runnerName)
        => [
            "Do not modify files unless the operational instruction explicitly authorizes it.",
            "Do not commit.",
            "Do not push.",
            "Do not invent commands, runtime, or events.",
            "Keep stderr, errors, and final answer separate.",
            $"Report commands and tools used when applicable for {runnerName}.",
            "Report limitations precisely.",
        ];

    internal static IReadOnlyList<string> BuildEvidenceStandard()
        => [
            "1. Result",
            "2. Evidence verified",
            "   - files read",
            "   - commands/tools executed",
            "   - relevant paths",
            "3. Inferencias razonables",
            "4. Limitations / uncertainties",
            "5. Next suggested step",
        ];

    internal static string BuildPromptText(
        string title,
        string repoRoot,
        string branch,
        string dirtyState,
        string mode,
        string originalPrompt,
        string previousAnswer,
        string contextSummary,
        IReadOnlyList<string> runnerNotes)
    {
        var sb = new StringBuilder();
        sb.AppendLine(title);
        sb.AppendLine(new string('-', title.Length));
        sb.AppendLine($"Repo root: {repoRoot}");
        sb.AppendLine($"Branch: {branch}");
        sb.AppendLine($"Dirty state: {dirtyState}");
        sb.AppendLine($"Mode used: {mode}");
        sb.AppendLine();
        sb.AppendLine(BuildExecutionDirective());
        sb.AppendLine();
        sb.AppendLine("Objective:");
        sb.AppendLine(BuildObjective());
        sb.AppendLine();
        sb.AppendLine(BuildOperationalInstructionSection(previousAnswer));
        sb.AppendLine();
        sb.AppendLine(BuildOriginalUserRequestSection(originalPrompt));

        if (!string.IsNullOrWhiteSpace(contextSummary))
        {
            sb.AppendLine();
            sb.AppendLine(contextSummary);
        }

        if (runnerNotes.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("Runner-specific notes:");
            foreach (var note in runnerNotes)
            {
                sb.AppendLine($"- {note}");
            }
        }

        sb.AppendLine();
        sb.AppendLine("Restrictions:");
        foreach (var restriction in BuildRestrictions(title.Contains("Hermes", StringComparison.OrdinalIgnoreCase) ? "Hermes" : "Pi"))
        {
            sb.AppendLine($"- {restriction}");
        }

        sb.AppendLine();
        sb.AppendLine("Evidence standard:");
        foreach (var line in BuildEvidenceStandard())
        {
            sb.AppendLine($"- {line}");
        }

        return sb.ToString().TrimEnd();
    }

    private static void AppendIndentedBlock(StringBuilder sb, string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            sb.AppendLine("  (none)");
            return;
        }

        var normalized = text.Replace("\r\n", "\n").Replace('\r', '\n');
        foreach (var line in normalized.Split('\n'))
        {
            sb.AppendLine($"  {line}");
        }
    }
}

internal sealed record RfsTuiOperationalHandoffPromptDraft(
    string RepoRoot,
    string Branch,
    string DirtyState,
    string Mode,
    string OriginalPrompt,
    string PreviousAnswer,
    string ContextSummary,
    string ExecutionDirective,
    string Objective,
    string OperationalInstructionToExecute,
    string OriginalUserRequestForContext,
    IReadOnlyList<string> Restrictions,
    IReadOnlyList<string> EvidenceStandard,
    IReadOnlyList<string> RunnerNotes,
    string PromptText);