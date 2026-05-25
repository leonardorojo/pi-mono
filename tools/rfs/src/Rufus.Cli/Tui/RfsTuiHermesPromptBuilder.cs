using System.Text;
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
        var contextSummary = BuildContextSummary(lastInteraction);
        var promptText = BuildPromptText(repoRoot, branch, dirtyState, repoName, lastInteraction, contextSummary);

        return RfsTuiHermesPromptBuildResult.Available(
            new RfsTuiHermesHandoffDraft(
                repoRoot,
                branch,
                dirtyState,
                lastInteraction.Mode,
                lastInteraction.Prompt.Trim(),
                lastInteraction.Answer.Trim(),
                contextSummary,
                "Revisar la última respuesta de RFS y preparar un handoff operativo para Hermes, con hechos, evidencias y riesgos separados de inferencias.",
                [
                    "No modificar archivos.",
                    "No hacer commit.",
                    "No hacer push.",
                    "Separar hechos de inferencias.",
                    "Entregar evidencia concreta.",
                ],
                [
                    "Archivos inspeccionados.",
                    "Hallazgos.",
                    "Evidencia.",
                    "Riesgos.",
                    "Próximos pasos.",
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
        sb.AppendLine("Hermes handoff draft");
        sb.AppendLine("────────────────────");
        sb.AppendLine($"Repo: {repoName}");
        sb.AppendLine($"Repo root: {repoRoot}");
        sb.AppendLine($"Branch: {branch}");
        sb.AppendLine($"Dirty state: {dirtyState}");
        sb.AppendLine($"Mode used: {lastInteraction.Mode}");
        sb.AppendLine();
        sb.AppendLine("Context:");
        sb.AppendLine("Prompt original:");
        AppendIndentedBlock(sb, lastInteraction.Prompt.Trim());
        sb.AppendLine();
        sb.AppendLine("Respuesta previa del LLM principal:");
        AppendIndentedBlock(sb, lastInteraction.Answer.Trim());
        sb.AppendLine();
        sb.AppendLine(contextSummary);
        sb.AppendLine();
        sb.AppendLine("Objetivo sugerido para Hermes:");
        sb.AppendLine("Revisar la última interacción útil de RFS y producir un handoff operativo, breve y verificable. Separar hechos de inferencias y citar evidencia concreta.");
        sb.AppendLine();
        sb.AppendLine("Restricciones:");
        sb.AppendLine("- No modificar archivos.");
        sb.AppendLine("- No hacer commit.");
        sb.AppendLine("- No hacer push.");
        sb.AppendLine("- Separar hechos de inferencias.");
        sb.AppendLine("- Entregar evidencia concreta.");
        sb.AppendLine();
        sb.AppendLine("Entrega esperada:");
        sb.AppendLine("1. Archivos inspeccionados.");
        sb.AppendLine("2. Hallazgos.");
        sb.AppendLine("3. Evidencia.");
        sb.AppendLine("4. Riesgos.");
        sb.AppendLine("5. Próximos pasos.");
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

internal sealed record RfsTuiHermesHandoffDraft(
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

internal sealed record RfsTuiHermesPromptBuildResult(
    bool Success,
    RfsTuiHermesHandoffDraft? Draft,
    string? ErrorMessage)
{
    internal static RfsTuiHermesPromptBuildResult Available(RfsTuiHermesHandoffDraft draft)
        => new(true, draft, null);

    internal static RfsTuiHermesPromptBuildResult Unavailable(string errorMessage)
        => new(false, null, errorMessage);
}
