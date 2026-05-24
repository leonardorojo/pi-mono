using System.Globalization;
using System.Linq;
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
        var modelLabel = string.IsNullOrWhiteSpace(workspaceModel) ? "(inherited)" : workspaceModel.Trim();
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

        foreach (var answerLine in answer.Split('\n', StringSplitOptions.None))
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
            : "Complete mode failed before the main LLM answered.");
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
    internal static bool Enabled => !Console.IsOutputRedirected && string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("NO_COLOR"));
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
