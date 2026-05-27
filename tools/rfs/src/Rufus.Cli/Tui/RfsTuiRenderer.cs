using System.Globalization;
using System.Linq;
using Rufus.Cli.PiIntegration;
using Rufus.RCK.Workspace;

namespace Rufus.Cli.Tui;

internal static class RfsTuiRenderer
{
    internal static void WriteAutoInit(RckWorkspaceInitResult initResult)
    {
        WriteTitle("RFS");
        WriteDivider();
        WriteWarningLine("Workspace not initialized.");
        Console.WriteLine();
        WriteInfoLine("Initializing RFS workspace...");
        WriteStatusLine(initResult.ConfigCreated, ".rfs created", ".rfs already existed");
        WriteStatusLine(
            initResult.RckDirectoriesCreated || initResult.HeadCreated || initResult.StateCreated || initResult.AnchorCreated,
            "RCK initialized",
            "RCK already initialized");
        WriteStatusLine(initResult.StateCreated, "genesis state created", "genesis state already existed");
        WriteStatusLine(initResult.AnchorCreated, "genesis anchor created", "genesis anchor already existed");
    }

    internal static void WriteHeader(RckWorkspaceStatus status, string repoName, string? workspaceModel, bool leadingBlankLine = false)
    {
        var resolvedModel = string.IsNullOrWhiteSpace(workspaceModel)
            ? RfsTuiSessionState.DefaultSessionModel
            : workspaceModel.Trim();
        var modelLabel = string.Equals(resolvedModel, RfsTuiSessionState.DefaultSessionModel, StringComparison.Ordinal)
            ? resolvedModel
            : $"{resolvedModel} · session";
        var branchLabel = string.IsNullOrWhiteSpace(status.GitContext.Branch) ? "(detached)" : status.GitContext.Branch;
        var dirtyLabel = status.GitContext.Dirty.ToString().ToLowerInvariant();

        if (leadingBlankLine)
        {
            Console.WriteLine();
        }

        WriteTitle($"RFS · {repoName}");
        WriteDivider();
        WriteLabeledValue("Model", modelLabel);
        WriteLabeledValue("RCK", $"states {status.StateCount} · deltas {status.DeltaCount} · anchors {status.AnchorCount}");
        WriteLabeledValue("Git", $"{branchLabel} · dirty {dirtyLabel}");
        Console.WriteLine();
    }

    internal static void WritePrompt()
    {
        Console.Write(Style("> ", ConsoleColor.DarkGray, bold: true));
    }

    internal static void WriteModeSelectionMenu()
    {
        WriteTitle("¿Cómo querés procesarlo?");
        Console.WriteLine();
        WriteModeOption("1", "Direct", "sin contexto RCK");
        WriteModeOption("2", "Simple", "memoria reciente liviana");
        WriteModeOption("3", "Complete", "TraceSlice + ContextPack validado");
        WriteModeOption("4", "Plan", "plan textual sin modificar código");
        Console.WriteLine();
        WriteMutedLine("Elegí 1-4, o /cancel:");
    }

    internal static void WritePasteCaptureIntro()
    {
        WriteMutedLine("Paste multiline prompt. Finish with /end. Use /cancel to discard.");
    }

    internal static void WritePasteCapturePrompt()
        => Console.Write(Style("paste> ", ConsoleColor.DarkGray, bold: true));

    internal static void WritePasteSelectionWarning()
    {
        WriteWarningLine("Multiline input detected while choosing processing mode.");
        WriteMutedLine("Use /cancel and then /paste for long text.");
    }

    internal static void WriteCapturedPasteReference(RfsTuiPromptDraft draft)
    {
        WriteSuccessLine("Captured long paste:");
        WriteKeyValue("ref", draft.ReferenceLabel());
        WriteKeyValue("lines", draft.LineCount.ToString(CultureInfo.InvariantCulture));
        WriteKeyValue("chars", draft.CharCount.ToString("N0", CultureInfo.InvariantCulture));
        WriteKeyValue("estimated tokens", $"~{draft.EstimatedTokens.ToString("N0", CultureInfo.InvariantCulture)}");
        Console.WriteLine($"  [paste: {draft.ReferenceLabel()} · {draft.LineCount} lines · {draft.CharCount:N0} chars]");
        Console.WriteLine();
    }

    internal static void WriteModeSelectionHelp()
    {
        WriteSectionTitle("Mode selection:");
        WriteModeOption("1", "Direct", "no RCK context");
        WriteModeOption("2", "Simple", "recent memory");
        WriteModeOption("3", "Complete", "governed context");
        WriteModeOption("4", "Plan", "plan only");
        WriteCommandLine("/cancel", "return to prompt");
        WriteCommandLine("/exit", "exit RFS");
    }

    internal static void WriteModeBanner(string modeLabel, string subtitle)
    {
        WriteSectionTitle($"[{modeLabel}]");
        if (!string.IsNullOrWhiteSpace(subtitle))
        {
            WriteMutedLine(subtitle);
        }
    }

    internal static void WritePiRunUnavailable(string message)
    {
        WriteWarningLine(message);
    }

    internal static void WritePiRunPromptSummary(RfsTuiPiPromptDraft draft, string workspaceModel)
    {
        WriteSectionTitle("Prompt operativo para Pi:");
        WriteKeyValue("model", string.IsNullOrWhiteSpace(workspaceModel) ? RfsTuiSessionState.DefaultSessionModel : workspaceModel.Trim());
        WriteKeyValue("repo root", draft.RepoRoot);
        WriteKeyValue("branch", draft.Branch);
        WriteKeyValue("dirty", draft.DirtyState);
        WriteKeyValue("mode", draft.Mode);
        WriteKeyValue("prompt", RfsTuiText.TruncateInline(draft.OriginalPrompt));
        WriteKeyValue("previous answer", RfsTuiText.TruncateInline(draft.PreviousAnswer));
        if (!string.IsNullOrWhiteSpace(draft.ContextSummary))
        {
            WriteKeyValue("context pack", "available");
            foreach (var line in draft.ContextSummary.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries))
            {
                WriteMutedLine($"  {line}");
            }
        }
        else
        {
            WriteKeyValue("context pack", "none");
        }
        Console.WriteLine();
    }

    private static bool PiRunProcessingAnnounced;
    private static bool PiRunCompletedAnnounced;

    internal static void WritePiRunStatusLine(string status)
    {
        WriteMutedLine($"[Pi run] {status}");
    }

    internal static void WritePiRunRuntimeEvent(PiJsonStreamEvent runtimeEvent)
    {
        if (runtimeEvent is null)
        {
            return;
        }

        var type = runtimeEvent.Type.Trim();
        if (type.Length == 0)
        {
            return;
        }

        if (string.Equals(type, "session", StringComparison.Ordinal))
        {
            ResetPiRunRuntimeState();
            WriteMutedLine("[Pi run] session started");
            return;
        }

        if (string.Equals(type, "agent_start", StringComparison.Ordinal))
        {
            WriteMutedLine("[Pi run] agent started");
            return;
        }

        if (string.Equals(type, "message_start", StringComparison.Ordinal) || string.Equals(type, "turn_start", StringComparison.Ordinal) || string.Equals(type, "message_end", StringComparison.Ordinal) || string.Equals(type, "turn_end", StringComparison.Ordinal))
        {
            return;
        }

        if (string.Equals(type, "auto_retry_start", StringComparison.Ordinal))
        {
            return;
        }

        if (string.Equals(type, "message_update", StringComparison.Ordinal))
        {
            var delta = runtimeEvent.Text;
            if (string.IsNullOrWhiteSpace(delta))
            {
                return;
            }

            if (!PiRunProcessingAnnounced)
            {
                WriteMutedLine("[Pi run] processing...");
                PiRunProcessingAnnounced = true;
            }

            return;
        }

        if (string.Equals(type, "tool_execution_start", StringComparison.Ordinal))
        {
            var toolName = RfsTuiText.TruncateInline(runtimeEvent.Name, 48);
            if (string.Equals(toolName, "(none)", StringComparison.Ordinal))
            {
                return;
            }

            var details = RfsTuiText.TruncateInline(runtimeEvent.Details, 80);
            if (string.Equals(details, "(none)", StringComparison.Ordinal))
            {
                WriteMutedLine($"[Pi run] tool started: {toolName}");
            }
            else
            {
                WriteMutedLine($"[Pi run] tool started: {toolName} · {details}");
            }

            return;
        }

        if (string.Equals(type, "tool_execution_update", StringComparison.Ordinal))
        {
            return;
        }

        if (string.Equals(type, "tool_execution_end", StringComparison.Ordinal))
        {
            var toolName = RfsTuiText.TruncateInline(runtimeEvent.Name, 48);
            var summary = RfsTuiText.TruncateInline(runtimeEvent.Summary, 80);
            if (string.Equals(toolName, "(none)", StringComparison.Ordinal) && string.Equals(summary, "(none)", StringComparison.Ordinal))
            {
                return;
            }

            if (string.Equals(summary, "(none)", StringComparison.Ordinal))
            {
                WriteMutedLine($"[Pi run] tool completed: {toolName}");
            }
            else if (string.Equals(toolName, "(none)", StringComparison.Ordinal))
            {
                WriteMutedLine($"[Pi run] tool completed: {summary}");
            }
            else
            {
                WriteMutedLine($"[Pi run] tool completed: {toolName} · {summary}");
            }

            return;
        }

        if (string.Equals(type, "agent_end", StringComparison.Ordinal))
        {
            if (!PiRunCompletedAnnounced)
            {
                WriteMutedLine("[Pi run] completed");
                PiRunCompletedAnnounced = true;
            }

            return;
        }

        if (string.Equals(type, "compaction_end", StringComparison.Ordinal))
        {
            var message = RfsTuiText.TruncateInline(runtimeEvent.Message, 96);
            if (!string.Equals(message, "(none)", StringComparison.Ordinal))
            {
                WriteMutedLine($"[Pi run] compaction failed: {message}");
            }

            return;
        }

        if (string.Equals(type, "auto_retry_end", StringComparison.Ordinal))
        {
            var message = RfsTuiText.TruncateInline(runtimeEvent.Message, 96);
            if (!string.Equals(message, "(none)", StringComparison.Ordinal))
            {
                WriteMutedLine($"[Pi run] retry failed: {message}");
            }

            return;
        }

        var messageText = RfsTuiText.TruncateInline(runtimeEvent.Message, 96);
        if (!string.Equals(messageText, "(none)", StringComparison.Ordinal))
        {
            WriteMutedLine($"[Pi run] {type}: {messageText}");
        }
    }

    private static void ResetPiRunRuntimeState()
    {
        PiRunProcessingAnnounced = false;
        PiRunCompletedAnnounced = false;
    }

    private static void AnnouncePiRunFinalAnswer()
    {
        return;
    }

    internal static void WritePiRunResult(RfsTuiPiRunResult result)
    {
        WriteSectionTitle("[Pi run]");
        WriteKeyValue("health", FormatPiRunHealth(result.Health));
        WriteKeyValue("cwd", result.WorkingDirectory);
        WriteKeyValue("duration", $"{result.DurationMs.ToString(CultureInfo.InvariantCulture)} ms");
        WriteKeyValue("exit code", result.ExitCode?.ToString(CultureInfo.InvariantCulture) ?? "(n/a)");
        WriteKeyValue("timed out", result.TimedOut ? "yes" : "no");
        WriteKeyValue("cancelled", result.Cancelled ? "yes" : "no");
        WriteKeyValue("failed to start", result.FailedToStart ? "yes" : "no");
        WriteKeyValue("prompt bytes", result.PromptBytes.ToString("N0", CultureInfo.InvariantCulture));
        if (!string.IsNullOrWhiteSpace(result.Provider))
        {
            WriteKeyValue("provider", result.Provider);
        }

        if (!string.IsNullOrWhiteSpace(result.Model))
        {
            WriteKeyValue("model", result.Model);
        }

        WritePiRunOutcomeMessage(result.Health);
        WritePiRunActions(result.ToolEvents);

        Console.WriteLine();
        WriteSectionTitle("Pi response:");
        WriteDivider("────────────────");
        if (string.IsNullOrWhiteSpace(result.Stdout))
        {
            WriteMutedLine("(no stdout)");
        }
        else
        {
            Console.WriteLine(result.Stdout.TrimEnd());
        }

        if (!string.IsNullOrWhiteSpace(result.Stderr))
        {
            Console.WriteLine();
            WriteSectionTitle("Pi stderr:");
            WriteDivider("────────────");
            Console.WriteLine(result.Stderr.TrimEnd());
        }
    }

    private static void WritePiRunActions(IReadOnlyList<PiJsonEventRunner.PiJsonToolEvent>? toolEvents)
    {
        if (toolEvents is null || toolEvents.Count == 0)
        {
            return;
        }

        var renderedActions = new List<string>();
        foreach (var toolEvent in toolEvents)
        {
            if (toolEvent is null)
            {
                continue;
            }

            var name = RfsTuiText.TruncateInline(toolEvent.Name, 48);
            var details = RfsTuiText.TruncateInline(toolEvent.Details, 80);
            var summary = RfsTuiText.TruncateInline(toolEvent.Summary, 80);

            if (string.Equals(toolEvent.Type, "tool_execution_start", StringComparison.Ordinal))
            {
                if (string.Equals(name, "(none)", StringComparison.Ordinal) && string.Equals(details, "(none)", StringComparison.Ordinal))
                {
                    continue;
                }

                renderedActions.Add(string.Equals(details, "(none)", StringComparison.Ordinal)
                    ? $"- started: {name}"
                    : $"- started: {name} · {details}");
                continue;
            }

            if (string.Equals(toolEvent.Type, "tool_execution_end", StringComparison.Ordinal))
            {
                if (string.Equals(name, "(none)", StringComparison.Ordinal) && string.Equals(summary, "(none)", StringComparison.Ordinal))
                {
                    continue;
                }

                renderedActions.Add(string.Equals(summary, "(none)", StringComparison.Ordinal)
                    ? $"- completed: {name}"
                    : $"- completed: {name} · {summary}");
            }
        }

        if (renderedActions.Count == 0)
        {
            return;
        }

        Console.WriteLine();
        WriteSectionTitle("Pi actions:");
        WriteDivider("────────────────");
        foreach (var action in renderedActions.Take(8))
        {
            WriteMutedLine(action);
        }

        if (renderedActions.Count > 8)
        {
            WriteMutedLine($"(and {renderedActions.Count - 8} more)");
        }
    }

    private static string FormatPiRunHealth(RfsTuiPiRunHealth health)
        => health switch
        {
            RfsTuiPiRunHealth.Starting => "starting",
            RfsTuiPiRunHealth.Running => "running",
            RfsTuiPiRunHealth.LongRunning => "long-running",
            RfsTuiPiRunHealth.TimedOut => "timed out",
            RfsTuiPiRunHealth.Cancelled => "cancelled",
            RfsTuiPiRunHealth.FailedToStart => "failed to start",
            RfsTuiPiRunHealth.ExitedWithError => "exited with error",
            RfsTuiPiRunHealth.Completed => "completed",
            _ => health.ToString().ToLowerInvariant(),
        };

    private static void WritePiRunOutcomeMessage(RfsTuiPiRunHealth health)
    {
        switch (health)
        {
            case RfsTuiPiRunHealth.Completed:
                WriteSuccessLine("Pi run completed.");
                break;
            case RfsTuiPiRunHealth.Cancelled:
                WriteWarningLine("Pi run was cancelled by user.");
                break;
            case RfsTuiPiRunHealth.TimedOut:
                WriteWarningLine("Pi run timed out before a final response arrived.");
                break;
            case RfsTuiPiRunHealth.FailedToStart:
                WriteWarningLine("Pi run failed to start.");
                break;
            case RfsTuiPiRunHealth.ExitedWithError:
                WriteWarningLine("Pi run exited with error.");
                break;
        }
    }

    internal static void WriteHermesRunHeartbeat(RfsTuiHermesRunProgress progress)
        => WriteMutedLine(FormatHermesRunHeartbeat(progress, RfsTuiTerminal.IsInteractive));

    internal static void WriteSimpleContextSummary(RfsTuiSimpleContextSummary summary)
    {
        WriteSectionTitle("Context:");
        WriteKeyValue("recent interactions", summary.RecentInteractions.ToString(CultureInfo.InvariantCulture));
        WriteKeyValue("anchors", summary.Anchors.ToString(CultureInfo.InvariantCulture));
        WriteKeyValue("artifacts", summary.Artifacts.ToString(CultureInfo.InvariantCulture));
        WriteKeyValue("estimated tokens", summary.EstimatedTokens.ToString("N0", CultureInfo.InvariantCulture));
        WriteKeyValue("transport risk", summary.TransportRisk);
        WriteKeyValue("truncated", summary.Truncated.ToString().ToLowerInvariant());
        WriteOptionalList("warnings", summary.Warnings);
        WriteOptionalList("omissions", summary.Omissions);
    }

    internal static void WritePlanContextSummary(RfsTuiSimpleContextSummary summary)
    {
        WriteSectionTitle("Context:");
        WriteKeyValue("context", "simple");
        WriteKeyValue("recent interactions", summary.RecentInteractions.ToString(CultureInfo.InvariantCulture));
        WriteKeyValue("anchors", summary.Anchors.ToString(CultureInfo.InvariantCulture));
        WriteKeyValue("artifacts", summary.Artifacts.ToString(CultureInfo.InvariantCulture));
        WriteKeyValue("estimated tokens", summary.EstimatedTokens.ToString("N0", CultureInfo.InvariantCulture));
        WriteKeyValue("transport risk", summary.TransportRisk);
        WriteKeyValue("truncated", summary.Truncated.ToString().ToLowerInvariant());
        WriteOptionalList("warnings", summary.Warnings);
        WriteOptionalList("omissions", summary.Omissions);
    }

    internal static void WriteCompleteContextSummary(
        string? validationStatus,
        string? selectionStrategy,
        string? contextPackScope,
        string? intentSource,
        int selectedStateCount,
        int selectedDeltaCount,
        int selectedAnchorCount,
        RckContextUsageReport contextUsageReport,
        IReadOnlyList<string> warnings,
        IReadOnlyList<string> omissions)
    {
        WriteSectionTitle("Context:");
        WriteKeyValue("validation", validationStatus ?? "(unknown)");
        WriteKeyValue("selection", selectionStrategy ?? "(unknown)");
        WriteKeyValue("scope", contextPackScope ?? "(unknown)");
        WriteKeyValue("intent source", intentSource ?? "(unknown)");
        WriteKeyValue("selected states/deltas/anchors", $"{selectedStateCount} / {selectedDeltaCount} / {selectedAnchorCount}");
        WriteKeyValue("estimated tokens", contextUsageReport.EstimatedTokens.ToString("N0", CultureInfo.InvariantCulture));
        WriteKeyValue("transport", contextUsageReport.TransportSizeChars > 32000 ? "stdin" : "argv");
        WriteKeyValue("transport risk", contextUsageReport.TransportRisk);
        WriteOptionalList("warnings", warnings);
        WriteOptionalList("omissions", omissions);
    }

    internal static void WriteCompleteStage(string stageLine)
    {
        Console.WriteLine(stageLine);
    }

    internal static void WriteCompleteStageDetail(string label, string value)
    {
        Console.WriteLine($"  {Style(label + ":", ConsoleColor.DarkGray)} {Style(value, ConsoleColor.White, bold: true)}");
    }

    internal static void WriteResponse(string answer)
    {
        WriteSectionTitle("Respuesta:");
        WriteDivider("────────────────────────────────────────────");

        if (string.IsNullOrWhiteSpace(answer))
        {
            WriteMutedLine("(no assistant output)");
            return;
        }

        var rendered = RfsTuiMarkdownLiteRenderer.Render(answer, RfsTuiAnsi.Enabled);
        if (string.IsNullOrEmpty(rendered))
        {
            return;
        }

        foreach (var answerLine in rendered.Split('\n', StringSplitOptions.None))
        {
            Console.WriteLine(answerLine);
        }
    }

    internal static void WriteRecordedStateDelta(string? stateId, string? deltaId)
    {
        WriteSuccessLine("Recorded State + Delta:");
        WriteKeyValue("state", RfsTuiText.ShortenId(stateId));
        WriteKeyValue("delta", RfsTuiText.ShortenId(deltaId));
    }

    internal static void WriteCompleteFailure(string reason)
    {
        var isIntentFailure = reason.Contains("inferring intent", StringComparison.OrdinalIgnoreCase);
        WriteErrorLine(isIntentFailure
            ? "Complete mode failed while inferring intent."
            : "Complete mode failed while asking main LLM.");
        WriteWarningLine("No State/Delta was recorded.");
        Console.Error.WriteLine();
        WriteErrorHeading("Reason:");
        foreach (var line in reason.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None))
        {
            Console.Error.WriteLine($"  {line}");
        }
    }

    internal static void WriteStatus(RckWorkspaceStatus status, RckWorkspaceModelConfigReadResult modelReadResult, RfsTuiSessionState sessionState)
    {
        WriteTitle($"RFS · {Path.GetFileName(Path.TrimEndingDirectorySeparator(status.RepoRoot))}");
        Console.WriteLine();
        WriteSectionTitle("RCK:");
        WriteKeyValue("head", RfsTuiText.ShortenId(status.Head));
        WriteKeyValue("states/deltas/anchors", $"{status.StateCount} / {status.DeltaCount} / {status.AnchorCount}");
        Console.WriteLine();
        WriteSectionTitle("Git:");
        WriteKeyValue("branch", string.IsNullOrWhiteSpace(status.GitContext.Branch) ? "(detached)" : status.GitContext.Branch);
        WriteKeyValue("dirty", status.GitContext.Dirty.ToString().ToLowerInvariant());
        Console.WriteLine();
        WriteSectionTitle("Model:");
        Console.WriteLine($"  {Style($"{GetCurrentModelLabel(modelReadResult)} · {GetModelSourceLabel(modelReadResult)}", ConsoleColor.White, bold: true)}");
        Console.WriteLine();
        WriteSectionTitle("Session:");
        WriteKeyValue("last mode", sessionState.LastMode);
        WriteKeyValue("last context", sessionState.LastContextKind);
        WriteKeyValue("last trace", sessionState.LastTrace is null ? "unavailable" : "available");
    }

    internal static void WriteLog(RckWorkspaceLogEntry[] entries)
    {
        if (entries.Length == 0)
        {
            WriteMutedLine("No interactions yet.");
            return;
        }

        WriteSectionTitle("Recent interactions:");
        foreach (var entry in entries)
        {
            Console.WriteLine($"- {Style(entry.StateShortId, ConsoleColor.Cyan)} {Style($"[{entry.Mode}]", ConsoleColor.Magenta, bold: true)} {Style(entry.CreatedAtUtc.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) + "Z", ConsoleColor.DarkGray)}");
            WriteIndentedLabeledValue("prompt", RfsTuiText.TruncateInline(entry.Prompt));
            WriteIndentedLabeledValue("answer", RfsTuiText.TruncateInline(entry.AnswerSummary));
            WriteIndentedLabeledValue("delta", RfsTuiText.ShortenId(entry.DeltaShortId));
            if (entry.Anchors.Count > 0)
            {
                WriteIndentedLabeledValue("anchors", entry.Anchors.Count.ToString(CultureInfo.InvariantCulture));
            }
        }
    }

    internal static void WriteContext(RfsTuiSessionState sessionState)
    {
        if (sessionState.LastContextKind == "simple" && sessionState.LastSimpleContext is not null)
        {
            WriteSimpleContextSummary(sessionState.LastSimpleContext);
            return;
        }

        if (sessionState.LastContextKind == "complete" && sessionState.LastCompleteContext is not null)
        {
            var complete = sessionState.LastCompleteContext;
            WriteSectionTitle("Context:");
            WriteKeyValue("validation", complete.ValidationStatus ?? "(unknown)");
            WriteKeyValue("selection", complete.SelectionStrategy ?? "(unknown)");
            WriteKeyValue("scope", complete.ContextPackScope ?? "(unknown)");
            WriteKeyValue("intent source", complete.IntentSource ?? "(unknown)");
            WriteKeyValue("selected states/deltas/anchors", $"{complete.SelectedStateCount} / {complete.SelectedDeltaCount} / {complete.SelectedAnchorCount}");
            WriteKeyValue("estimated tokens", complete.EstimatedTokens.ToString("N0", CultureInfo.InvariantCulture));
            WriteKeyValue("transport", complete.EstimatedChars > 32000 ? "stdin" : "argv");
            WriteKeyValue("transport risk", complete.TransportRisk);
            WriteOptionalList("warnings", complete.Warnings);
            WriteOptionalList("omissions", complete.Omissions);
            return;
        }

        WriteMutedLine("No context has been built yet.");
    }

    internal static void WriteTrace(RfsTuiTraceSummary? trace)
    {
        if (trace is null)
        {
            WriteMutedLine("No TraceSlice has been built in this session yet.");
            return;
        }

        WriteSectionTitle("Last TraceSlice / validation summary:");
        WriteKeyValue("selection strategy", trace.SelectionStrategy ?? "(unknown)");
        WriteKeyValue("validation status", trace.ValidationStatus ?? "(unknown)");
        WriteKeyValue("selected states", trace.SelectedStateCount.ToString(CultureInfo.InvariantCulture));
        WriteKeyValue("selected deltas", trace.SelectedDeltaCount.ToString(CultureInfo.InvariantCulture));
        WriteKeyValue("selected anchors", trace.SelectedAnchorCount.ToString(CultureInfo.InvariantCulture));
        WriteOptionalList("warnings", trace.Warnings);
        WriteOptionalList("omissions", trace.Omissions);
    }

    internal static void WriteHermesHandoffUnavailable(string message)
    {
        WriteWarningLine(message);
    }

    internal static void WriteHermesHandoffDraft(RfsTuiHermesHandoffDraft draft)
    {
        WriteSectionTitle("Hermes handoff draft");
        WriteKeyValue("repo", draft.RepoRoot);
        WriteKeyValue("branch", draft.Branch);
        WriteKeyValue("dirty", draft.DirtyState);
        WriteKeyValue("mode", draft.Mode);
        Console.WriteLine();
        WriteKeyValue("ContextPack", string.IsNullOrWhiteSpace(draft.ContextSummary) ? "(not available)" : "available");
        if (!string.IsNullOrWhiteSpace(draft.ContextSummary))
        {
            Console.WriteLine(draft.ContextSummary);
        }
        Console.WriteLine();
        WriteSectionTitle("Objetivo sugerido para Hermes:");
        Console.WriteLine(draft.SuggestedObjective);
        Console.WriteLine();
        WriteSectionTitle("Restricciones:");
        foreach (var restriction in draft.Restrictions)
        {
            Console.WriteLine($"- {restriction}");
        }
        Console.WriteLine();
        WriteSectionTitle("Entrega esperada:");
        for (var index = 0; index < draft.Deliverables.Count; index++)
        {
            Console.WriteLine($"{index + 1}. {draft.Deliverables[index]}");
        }
        Console.WriteLine();
        WriteSectionTitle("Prompt operativo para Hermes:");
        Console.WriteLine("```");
        Console.WriteLine(draft.PromptText);
        Console.WriteLine("```");
    }

    internal static void WriteHermesRunResult(RfsTuiHermesRunResult result)
    {
        WriteSectionTitle("[Hermes run]");
        WriteKeyValue("health", FormatHermesRunHealth(result.Health));
        WriteKeyValue("transport", "cli-oneshot");
        WriteKeyValue("cwd", result.WorkingDirectory);
        WriteKeyValue("duration", $"{result.DurationMs.ToString(CultureInfo.InvariantCulture)} ms");
        WriteKeyValue("exit code", result.ExitCode?.ToString(CultureInfo.InvariantCulture) ?? "(n/a)");
        WriteKeyValue("timed out", result.TimedOut ? "yes" : "no");
        WriteKeyValue("Git changed", result.DirtyStateChanged ? "yes" : "no");
        WriteKeyValue("prompt bytes", result.PromptBytes.ToString("N0", CultureInfo.InvariantCulture));

        WriteHermesRunOutcomeMessage(result.Health);

        Console.WriteLine();
        if (result.Health is RfsTuiHermesRunHealth.Cancelled or RfsTuiHermesRunHealth.TimedOut)
        {
            WriteSectionTitle("Partial Hermes output:");
        }
        else
        {
            WriteSectionTitle("Hermes response:");
        }

        WriteDivider("────────────────");

        if (string.IsNullOrWhiteSpace(result.Stdout))
        {
            if (result.Health is RfsTuiHermesRunHealth.Cancelled or RfsTuiHermesRunHealth.TimedOut)
            {
                WriteMutedLine("No partial Hermes output was available.");
            }
            else
            {
                WriteMutedLine("(no stdout)");
            }
        }
        else
        {
            Console.WriteLine(result.Stdout.TrimEnd());
        }

        var stderrToDisplay = GetHermesRunStderrForDisplay(result, out var cancelledTracebackSuppressed);
        if (!string.IsNullOrWhiteSpace(stderrToDisplay))
        {
            Console.WriteLine();
            if (result.Health is RfsTuiHermesRunHealth.Cancelled && cancelledTracebackSuppressed)
            {
                WriteSectionTitle("Hermes stderr (filtered):");
            }
            else if (result.Health is RfsTuiHermesRunHealth.Cancelled or RfsTuiHermesRunHealth.TimedOut)
            {
                WriteSectionTitle("Partial Hermes stderr:");
            }
            else
            {
                WriteSectionTitle("Hermes stderr:");
            }

            WriteDivider("──────────────");
            Console.WriteLine(stderrToDisplay.TrimEnd());
        }

        if (result.DirtyStateChanged)
        {
            Console.WriteLine();
            WriteWarningLine("WARNING: repository state changed during Hermes run.");
            WriteSectionTitle("Before:");
            Console.WriteLine(FormatGitStatus(result.GitStatusBefore));
            WriteSectionTitle("After:");
            Console.WriteLine(FormatGitStatus(result.GitStatusAfter));
        }
    }

    internal static string FormatHermesRunHeartbeat(RfsTuiHermesRunProgress progress, bool showCancelHint)
    {
        var elapsed = FormatSeconds(progress.Elapsed);
        var timeout = FormatSeconds(progress.Timeout);
        var remaining = FormatSeconds(progress.Remaining);
        var pidLabel = progress.ProcessId is null ? string.Empty : $" · pid: {progress.ProcessId}";
        var statusLabel = progress.Elapsed >= TimeSpan.FromSeconds(240)
            ? "close to timeout."
            : progress.Elapsed >= TimeSpan.FromSeconds(120)
                ? "still waiting for final response; cli-oneshot is final-only."
                : progress.Elapsed >= TimeSpan.FromSeconds(60)
                    ? "taking longer than usual."
                    : "still running...";
        var cancelHint = showCancelHint
            ? " · press q to cancel"
            : string.Empty;

        return $"[Hermes run] {statusLabel} elapsed: {elapsed} / {timeout} · remaining: {remaining} · cwd: {progress.WorkingDirectory} · prompt bytes: {progress.PromptBytes.ToString("N0", CultureInfo.InvariantCulture)} · transport: {progress.Transport}{pidLabel}{cancelHint}";
    }

    private static string GetHermesRunStderrForDisplay(RfsTuiHermesRunResult result, out bool cancelledTracebackSuppressed)
    {
        cancelledTracebackSuppressed = false;
        if (result.Health is not RfsTuiHermesRunHealth.Cancelled || string.IsNullOrWhiteSpace(result.Stderr))
        {
            return result.Stderr;
        }

        if (!TryStripExpectedCancelledTraceback(result.Stderr, out var filteredStderr))
        {
            return result.Stderr;
        }

        cancelledTracebackSuppressed = true;
        return filteredStderr;
    }

    private static bool TryStripExpectedCancelledTraceback(string stderr, out string filteredStderr)
    {
        filteredStderr = stderr;
        var lines = stderr.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
        var keyboardInterruptLineIndex = FindLastLineIndex(lines, "KeyboardInterrupt");
        if (keyboardInterruptLineIndex < 0)
        {
            return false;
        }

        var tracebackStartLineIndex = FindLastLineIndex(lines, "Traceback (most recent call last):", keyboardInterruptLineIndex);
        if (tracebackStartLineIndex < 0)
        {
            return false;
        }

        filteredStderr = string.Join(
            Environment.NewLine,
            lines.Where((line, index) => index < tracebackStartLineIndex || index > keyboardInterruptLineIndex));

        return !string.Equals(filteredStderr.TrimEnd('\r', '\n'), stderr.TrimEnd('\r', '\n'), StringComparison.Ordinal);
    }

    private static int FindLastLineIndex(string[] lines, string expectedLine, int beforeIndex = int.MaxValue)
    {
        var startIndex = Math.Min(beforeIndex, lines.Length - 1);
        for (var index = startIndex; index >= 0; index--)
        {
            if (string.Equals(lines[index].Trim(), expectedLine, StringComparison.Ordinal))
            {
                return index;
            }
        }

        return -1;
    }

    private static void WriteHermesRunOutcomeMessage(RfsTuiHermesRunHealth health)
    {
        switch (health)
        {
            case RfsTuiHermesRunHealth.Cancelled:
                Console.WriteLine();
                WriteWarningLine("Hermes process was interrupted by user cancellation.");
                WriteMutedLine("The cli-oneshot transport is final-only, so a partial answer may not be available.");
                break;
            case RfsTuiHermesRunHealth.TimedOut:
                Console.WriteLine();
                WriteWarningLine("Hermes run timed out before a final response arrived.");
                WriteMutedLine("The cli-oneshot transport is final-only, so a partial answer may not be available.");
                break;
            case RfsTuiHermesRunHealth.FailedToStart:
                Console.WriteLine();
                WriteWarningLine("Hermes run failed to start.");
                break;
            case RfsTuiHermesRunHealth.ExitedWithError:
                Console.WriteLine();
                WriteWarningLine("Hermes run exited with error.");
                break;
            case RfsTuiHermesRunHealth.Completed:
                Console.WriteLine();
                WriteSuccessLine("Hermes run completed.");
                break;
            default:
                Console.WriteLine();
                WriteMutedLine($"Hermes run health: {FormatHermesRunHealth(health)}.");
                break;
        }
    }

    private static string FormatHermesRunHealth(RfsTuiHermesRunHealth health)
        => health.ToString().ToLowerInvariant();

    private static string FormatGitStatus(string? status)
        => string.IsNullOrWhiteSpace(status) ? "(clean)" : status.TrimEnd();

    private static string FormatSeconds(TimeSpan duration)
    {
        var seconds = Math.Max(0, (int)Math.Round(duration.TotalSeconds, MidpointRounding.AwayFromZero));
        return seconds.ToString(CultureInfo.InvariantCulture) + "s";
    }

    internal static void WriteHelp(IReadOnlyList<RfsTuiCommandInfo> commands)
    {
        WriteSectionTitle("Commands:");
        WriteCommandEntries(commands);
    }

    internal static void WriteCommandSuggestions(string input, IReadOnlyList<RfsTuiCommandInfo> suggestions)
    {
        WriteSectionTitle("Did you mean?");
        WriteCommandEntries(suggestions);
    }

    internal static int WriteCommandPalette(IReadOnlyList<RfsTuiCommandInfo> suggestions)
    {
        if (suggestions.Count == 0)
        {
            WriteMutedLine("No matching commands");
            return 1;
        }

        WriteCommandEntries(suggestions);
        return suggestions.Count;
    }

    internal static void WriteUnknownCommand(string input)
    {
        WriteWarningLine($"Unknown command: {input}");
        WriteMutedLine("Type /help to show available commands.");
    }

    internal static int WriteModelPickerScreen(
        IReadOnlyList<PiRpcAvailableModel> models,
        RfsTuiModelSelectionState selectionState,
        string currentSessionModel)
    {
        var lineCount = 0;

        WriteSectionTitle("Select main model");
        lineCount++;
        WriteMutedLine("↑/↓ move · Enter select · Esc cancel · q cancel");
        lineCount++;
        Console.WriteLine();
        lineCount++;

        if (models.Count == 0)
        {
            WriteWarningLine("No models returned by Pi RPC.");
            lineCount++;
            WriteKeyValue("Current", currentSessionModel);
            lineCount++;
            WriteKeyValue("Selected", "(none)");
            lineCount++;
            return lineCount;
        }

        var idWidth = Math.Max("model".Length, models.Max(model => model.Id.Length));
        var displayWidth = Math.Max(
            "display name".Length,
            models.Max(model => string.IsNullOrWhiteSpace(model.DisplayName) ? 0 : model.DisplayName.Length));
        var providerWidth = Math.Max("provider".Length, models.Max(model => model.Provider.Length));

        for (var index = 0; index < models.Count; index++)
        {
            var model = models[index];
            var selected = index == selectionState.SelectedIndex;
            var marker = selected ? ">" : " ";
            var idLabel = model.Id.PadRight(idWidth);
            var displayLabel = (model.DisplayName ?? string.Empty).PadRight(displayWidth);
            var providerLabel = model.Provider.PadRight(providerWidth);
            var markerStyle = selected ? ConsoleColor.Cyan : ConsoleColor.DarkGray;
            var idStyle = selected ? ConsoleColor.White : ConsoleColor.DarkGray;
            var displayStyle = selected ? ConsoleColor.White : ConsoleColor.DarkGray;
            var providerStyle = selected ? ConsoleColor.DarkGray : ConsoleColor.DarkGray;

            Console.WriteLine(
                $"{Style(marker, markerStyle, bold: selected)} {Style(idLabel, idStyle, bold: selected)}  {Style(displayLabel, displayStyle, bold: selected)}  {Style(providerLabel, providerStyle)}");
            lineCount++;
        }

        Console.WriteLine();
        lineCount++;
        WriteKeyValue("Current", currentSessionModel);
        lineCount++;
        WriteKeyValue("Selected", selectionState.SelectedModelId ?? "(none)");
        lineCount++;

        return lineCount;
    }

    private static void WriteModeOption(string number, string mode, string description)
    {
        var paddedMode = mode.PadRight(9);
        Console.WriteLine($"  {Style(number, ConsoleColor.Cyan, bold: true)} {Style(paddedMode, ConsoleColor.White, bold: true)} — {Style(description, ConsoleColor.DarkGray)}");
    }

    private static void WriteCommandLine(string command, string description)
        => WriteCommandLine(command, description, 18);

    private static void WriteCommandLine(string command, string description, int width)
    {
        var paddedCommand = command.PadRight(width);
        Console.WriteLine($"  {Style(paddedCommand, ConsoleColor.Cyan, bold: true)} {Style(description, ConsoleColor.DarkGray)}");
    }

    private static void WriteCommandEntries(IReadOnlyList<RfsTuiCommandInfo> commands)
    {
        if (commands.Count == 0)
        {
            return;
        }

        var width = Math.Max(18, commands.Max(command => command.Usage.Length));
        foreach (var command in commands)
        {
            WriteCommandLine(command.Usage, command.Description, width);
        }
    }

    private static void WriteLabeledValue(string label, string value)
    {
        Console.WriteLine($"{Style(label + ":", ConsoleColor.DarkGray)} {Style(value, ConsoleColor.White, bold: true)}");
    }

    private static void WriteKeyValue(string label, string value)
    {
        Console.WriteLine($"  {Style(label + ":", ConsoleColor.DarkGray)} {Style(value, ConsoleColor.White, bold: true)}");
    }

    private static void WriteIndentedLabeledValue(string label, string value)
    {
        Console.WriteLine($"  {Style(label + ":", ConsoleColor.DarkGray)} {value}");
    }

    private static void WriteIndentedLabel(string label)
    {
        Console.WriteLine($"  {Style(label + ":", ConsoleColor.DarkGray)}");
    }

    private static void WriteOptionalList(string label, IReadOnlyList<string> values)
    {
        if (values.Count == 0)
        {
            return;
        }

        WriteIndentedLabel(label);
        foreach (var value in values)
        {
            Console.WriteLine($"    - {value}");
        }
    }

    private static void WriteStatusLine(bool condition, string successText, string fallbackText)
    {
        Console.WriteLine(condition ? $"{Style("✓", ConsoleColor.Green, bold: true)} {successText}" : $"{Style("•", ConsoleColor.DarkGray)} {fallbackText}");
    }

    private static void WriteTitle(string text)
    {
        Console.WriteLine(Style(text, ConsoleColor.Cyan, bold: true));
    }

    private static void WriteSectionTitle(string text)
    {
        Console.WriteLine(Style(text, ConsoleColor.Cyan, bold: true));
    }

    private static void WriteMutedLine(string text)
    {
        Console.WriteLine(Style(text, ConsoleColor.DarkGray));
    }

    private static void WriteInfoLine(string text)
    {
        Console.WriteLine(Style(text, ConsoleColor.Blue));
    }

    internal static void WriteWarningLine(string text)
    {
        Console.WriteLine(Style(text, ConsoleColor.Yellow));
    }

    private static void WriteSuccessLine(string text)
    {
        Console.WriteLine(Style(text, ConsoleColor.Green, bold: true));
    }

    private static void WriteErrorLine(string text)
    {
        Console.Error.WriteLine(Style(text, ConsoleColor.Red, bold: true));
    }

    private static void WriteErrorHeading(string text)
    {
        Console.Error.WriteLine(Style(text, ConsoleColor.Red, bold: true));
    }

    private static void WriteDivider(string? text = null)
    {
        Console.WriteLine(Style(text ?? "────────────────────────", ConsoleColor.DarkGray));
    }

    private static string GetCurrentModelLabel(RckWorkspaceModelConfigReadResult readResult)
        => readResult.HasConfiguredDefaultModel ? readResult.DefaultModel!.Trim() : "(inherited)";

    private static string GetModelSourceLabel(RckWorkspaceModelConfigReadResult readResult)
        => readResult.HasConfiguredDefaultModel ? "workspace" : readResult.ConfigExists ? "inherited" : "default";

    private static string Style(string text, ConsoleColor color, bool bold = false)
    {
        if (!RfsTuiAnsi.Enabled)
        {
            return text;
        }

        var colorCode = color switch
        {
            ConsoleColor.Black => "30",
            ConsoleColor.DarkRed => "31",
            ConsoleColor.DarkGreen => "32",
            ConsoleColor.DarkYellow => "33",
            ConsoleColor.DarkBlue => "34",
            ConsoleColor.DarkMagenta => "35",
            ConsoleColor.DarkCyan => "36",
            ConsoleColor.DarkGray => "90",
            ConsoleColor.Red => "91",
            ConsoleColor.Green => "92",
            ConsoleColor.Yellow => "93",
            ConsoleColor.Blue => "94",
            ConsoleColor.Magenta => "95",
            ConsoleColor.Cyan => "96",
            ConsoleColor.Gray => "37",
            ConsoleColor.White => "97",
            _ => "39",
        };

        var prefix = bold ? "\u001b[1m" : string.Empty;
        prefix += $"\u001b[{colorCode}m";
        return $"{prefix}{text}\u001b[0m";
    }
}

internal static class RfsTuiAnsi
{
    internal static bool Enabled => RfsTuiTerminal.UseAnsiStyle;
}

internal static class RfsTuiText
{
    internal static string ShortenId(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "(unknown)";
        }

        var trimmed = value.Trim();
        return trimmed.Length <= 8 ? trimmed : trimmed[..8];
    }

    internal static string TruncateInline(string? value, int maxLength = 72)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "(none)";
        }

        var singleLine = value.Replace("\r", " ", StringComparison.Ordinal).Replace("\n", " ", StringComparison.Ordinal).Trim();
        if (singleLine.Length <= maxLength)
        {
            return singleLine;
        }

        return singleLine[..Math.Max(0, maxLength - 1)] + "…";
    }
}
