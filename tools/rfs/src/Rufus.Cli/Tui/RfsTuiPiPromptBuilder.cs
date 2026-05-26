using System.Text;
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
        var contextSummary = BuildContextSummary(lastInteraction);
        var promptText = BuildPromptText(repoRoot, branch, dirtyState, repoName, lastInteraction, contextSummary);

        return RfsTuiPiPromptBuildResult.Available(
            new RfsTuiPiPromptDraft(
                repoRoot,
                branch,
                dirtyState,
                lastInteraction.Mode,
                lastInteraction.Prompt.Trim(),
                lastInteraction.Answer.Trim(),
                contextSummary,
                "Re-ejecutar la última interacción útil de RFS con Pi, manteniendo guardrails estrictos y mostrando runtime real si el stream emite eventos.",
                [
                    "No modificar archivos.",
                    "No hacer commit.",
                    "No hacer push.",
                    "No tocar RCK Core.",
                    "No inventar eventos ni runtime.",
                    "Separar stderr, errores y respuesta final.",
                ],
                [
                    "Runtime visible real.",
                    "Respuesta final.",
                    "Errores y stderr.",
                    "Riesgos o límites.",
                ],
                promptText));
    }

    private static string BuildContextSummary(RfsTuiLastInteractionContext lastInteraction)
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

    private static string BuildPromptText(
        string repoRoot,
        string branch,
        string dirtyState,
        string repoName,
        RfsTuiLastInteractionContext lastInteraction,
        string contextSummary)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Pi run prompt");
        sb.AppendLine("-------------");
        sb.AppendLine($"Repo: {repoName}");
        sb.AppendLine($"Repo root: {repoRoot}");
        sb.AppendLine($"Branch: {branch}");
        sb.AppendLine($"Dirty state: {dirtyState}");
        sb.AppendLine($"Mode used: {lastInteraction.Mode}");
        sb.AppendLine();
        sb.AppendLine("Última interacción útil:");
        sb.AppendLine("Prompt original:");
        AppendIndentedBlock(sb, lastInteraction.Prompt.Trim());
        sb.AppendLine();
        sb.AppendLine("Respuesta previa del LLM principal:");
        AppendIndentedBlock(sb, lastInteraction.Answer.Trim());

        if (!string.IsNullOrWhiteSpace(contextSummary))
        {
            sb.AppendLine();
            sb.AppendLine(contextSummary);
        }

        sb.AppendLine();
        sb.AppendLine("Objetivo sugerido para Pi:");
        sb.AppendLine("Revisar la última interacción útil de RFS con Pi y responder de forma honesta. Mostrar runtime real solo si el JSON event stream lo emite, no inventar tool events, separar stderr y final answer, y conservar los guardrails.");
        sb.AppendLine();
        sb.AppendLine("Restricciones:");
        sb.AppendLine("- No modificar archivos.");
        sb.AppendLine("- No hacer commit.");
        sb.AppendLine("- No hacer push.");
        sb.AppendLine("- No tocar RCK Core.");
        sb.AppendLine("- No inventar eventos ni runtime.");
        sb.AppendLine("- Separar stderr, errores y respuesta final.");
        sb.AppendLine();
        sb.AppendLine("Entrega esperada:");
        sb.AppendLine("1. Runtime visible real.");
        sb.AppendLine("2. Respuesta final.");
        sb.AppendLine("3. Errores y stderr.");
        sb.AppendLine("4. Riesgos o límites.");
        return sb.ToString().TrimEnd();
    }

    private static void AppendIndentedBlock(StringBuilder sb, string text)
    {
        using var reader = new StringReader(text);
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            sb.AppendLine($"  {line}");
        }
    }
}

internal sealed record RfsTuiPiPromptDraft(
    string RepoRoot,
    string Branch,
    string DirtyState,
    string Mode,
    string OriginalPrompt,
    string PreviousAnswer,
    string ContextSummary,
    string SuggestedObjective,
    IReadOnlyList<string> Restrictions,
    IReadOnlyList<string> Deliverables,
    string PromptText);

internal sealed record RfsTuiPiPromptBuildResult(
    bool Success,
    RfsTuiPiPromptDraft? Draft,
    string? ErrorMessage)
{
    internal static RfsTuiPiPromptBuildResult Available(RfsTuiPiPromptDraft draft)
        => new(true, draft, null);

    internal static RfsTuiPiPromptBuildResult Unavailable(string errorMessage)
        => new(false, null, errorMessage);
}
