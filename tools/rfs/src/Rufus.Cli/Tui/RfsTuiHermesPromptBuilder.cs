using Rufus.RCK.Workspace;

namespace Rufus.Cli.Tui;

internal static class RfsTuiHermesPromptBuilder
{
    internal static RfsTuiHermesPromptBuildResult TryBuild(RckWorkspaceStatus status, RfsTuiSessionState sessionState)
    {
        var lastInteraction = sessionState.LastInteraction;
        if (lastInteraction is null || string.IsNullOrWhiteSpace(lastInteraction.Prompt) || string.IsNullOrWhiteSpace(lastInteraction.Answer))
        {
            return RfsTuiHermesPromptBuildResult.Unavailable("No hay una respuesta previa para generar handoff a Hermes.");
        }

        var branch = string.IsNullOrWhiteSpace(status.GitContext.Branch)
            ? "(detached)"
            : status.GitContext.Branch.Trim();
        var dirtyState = status.GitContext.Dirty ? "dirty" : "clean";
        var repoRoot = Path.GetFullPath(status.RepoRoot);
        var repoName = Path.GetFileName(Path.TrimEndingDirectorySeparator(repoRoot));
        var contextSummary = RfsTuiOperationalHandoffPromptBuilder.BuildContextSummary(lastInteraction);
        var executionDirective = RfsTuiOperationalHandoffPromptBuilder.BuildExecutionDirective();
        var objective = RfsTuiOperationalHandoffPromptBuilder.BuildObjective();
        var originalPrompt = lastInteraction.Prompt.Trim();
        var previousAnswer = lastInteraction.Answer.Trim();
        var originalUserRequestForContext = RfsTuiOperationalHandoffPromptBuilder.BuildOriginalUserRequestSection(originalPrompt);
        var operationalInstructionToExecute = RfsTuiOperationalHandoffPromptBuilder.BuildOperationalInstructionSection(previousAnswer);
        var restrictions = RfsTuiOperationalHandoffPromptBuilder.BuildRestrictions("Hermes");
        var evidenceStandard = RfsTuiOperationalHandoffPromptBuilder.BuildEvidenceStandard();
        var runnerNotes = new[]
        {
            "Use Hermes CLI one-shot transport for execution.",
            "Capture git status before and after the run when available.",
            "Respect argv prompt-size guardrails and report if the prompt is too large.",
        };
        var promptText = RfsTuiOperationalHandoffPromptBuilder.BuildPromptText(
            title: "Hermes operational handoff prompt",
            repoRoot: repoRoot,
            branch: branch,
            dirtyState: dirtyState,
            mode: lastInteraction.Mode,
            originalPrompt: originalPrompt,
            previousAnswer: previousAnswer,
            contextSummary: contextSummary,
            runnerNotes: runnerNotes);

        return RfsTuiHermesPromptBuildResult.Available(
            new RfsTuiOperationalHandoffPromptDraft(
                repoRoot,
                branch,
                dirtyState,
                lastInteraction.Mode,
                originalPrompt,
                previousAnswer,
                contextSummary,
                executionDirective,
                objective,
                operationalInstructionToExecute,
                originalUserRequestForContext,
                restrictions,
                evidenceStandard,
                runnerNotes,
                promptText));
    }
}

internal sealed record RfsTuiHermesPromptBuildResult(
    bool Success,
    RfsTuiOperationalHandoffPromptDraft? Draft,
    string? ErrorMessage)
{
    internal static RfsTuiHermesPromptBuildResult Available(RfsTuiOperationalHandoffPromptDraft draft)
        => new(true, draft, null);

    internal static RfsTuiHermesPromptBuildResult Unavailable(string errorMessage)
        => new(false, null, errorMessage);
}