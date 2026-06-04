using Rufus.RCK.Workspace;

namespace Rufus.Cli.Tui;

internal static class RfsTuiPiPromptBuilder
{
    internal static RfsTuiPiPromptBuildResult TryBuild(RckWorkspaceStatus status, RfsTuiSessionState sessionState)
    {
        var lastInteraction = sessionState.LastInteraction;
        if (lastInteraction is null || string.IsNullOrWhiteSpace(lastInteraction.Prompt) || string.IsNullOrWhiteSpace(lastInteraction.Answer))
        {
            return RfsTuiPiPromptBuildResult.Unavailable("No hay una interacción previa para construir el prompt de Pi.");
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
        var restrictions = RfsTuiOperationalHandoffPromptBuilder.BuildRestrictions("Pi");
        var evidenceStandard = RfsTuiOperationalHandoffPromptBuilder.BuildEvidenceStandard();
        var runnerNotes = new[]
        {
            "Use the Pi JSON event stream and preserve runtime evidence if the stream emits it.",
            "Keep tool/runtime events separate from the final answer.",
        };
        var promptText = RfsTuiOperationalHandoffPromptBuilder.BuildPromptText(
            title: "Pi operational handoff prompt",
            repoRoot: repoRoot,
            branch: branch,
            dirtyState: dirtyState,
            mode: lastInteraction.Mode,
            originalPrompt: originalPrompt,
            previousAnswer: previousAnswer,
            contextSummary: contextSummary,
            runnerNotes: runnerNotes);

        return RfsTuiPiPromptBuildResult.Available(
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

internal sealed record RfsTuiPiPromptBuildResult(
    bool Success,
    RfsTuiOperationalHandoffPromptDraft? Draft,
    string? ErrorMessage)
{
    internal static RfsTuiPiPromptBuildResult Available(RfsTuiOperationalHandoffPromptDraft draft)
        => new(true, draft, null);

    internal static RfsTuiPiPromptBuildResult Unavailable(string errorMessage)
        => new(false, null, errorMessage);
}