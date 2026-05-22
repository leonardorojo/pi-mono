using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Rufus.Agenting;
using Rufus.Agenting.Intent;
using Rufus.Agenting.TraceSlice;
using Rufus.Cli.PiIntegration;
using Rufus.RCK.Workspace;

if (args.Length == 0 || IsHelpCommand(args[0]))
{
    PrintHelp();
    return 0;
}

if (args[0] == "--version")
{
    Console.WriteLine("rfs 0.0.1-poc");
    return 0;
}

if (args[0] == "status")
{
    try
    {
        var status = RckWorkspaceStatusReader.Read();
        foreach (var line in status.FormatConsoleLines())
        {
            Console.WriteLine(line);
        }

        return 0;
    }
    catch (InvalidOperationException ex)
    {
        Console.Error.WriteLine(ex.Message);
        return 1;
    }
}

if (args[0] == "init")
{
    var initResult = RckWorkspaceInitializer.Initialize();
    foreach (var line in initResult.FormatConsoleSummaryLines())
    {
        Console.WriteLine(line);
    }

    return initResult.Success ? 0 : 1;
}

if (args[0] == "log")
{
    var logResult = RckWorkspaceLogReader.Read();
    foreach (var line in logResult.FormatConsoleLines())
    {
        Console.WriteLine(line);
    }

    return logResult.Success ? 0 : 1;
}

if (args[0] == "context-pack")
{
    if (args.Length == 2 && string.Equals(args[1], "--trace-slice", StringComparison.Ordinal))
    {
        Console.Error.WriteLine("Usage: rfs context-pack --trace-slice <prompt>");
        return 1;
    }

    if (args.Length >= 2 && string.Equals(args[1], "--trace-slice", StringComparison.Ordinal))
    {
        var prompt = string.Join(" ", args.Skip(2)).Trim();
        if (string.IsNullOrWhiteSpace(prompt))
        {
            Console.Error.WriteLine("Missing prompt.");
            return 1;
        }

        var traceSliceContextPackResult = RckTraceSliceContextPackBuilder.Build(prompt, Directory.GetCurrentDirectory(), 5);
        if (!traceSliceContextPackResult.Success)
        {
            if (!string.IsNullOrWhiteSpace(traceSliceContextPackResult.ErrorMessage))
            {
                Console.Error.WriteLine(traceSliceContextPackResult.ErrorMessage);
            }

            return 1;
        }

        Console.WriteLine(traceSliceContextPackResult.Json);
        return 0;
    }

    if (args.Length > 1)
    {
        Console.Error.WriteLine("Usage: rfs context-pack [--trace-slice <prompt>]");
        return 1;
    }

    var contextPackResult = RckWorkspaceContextPackReader.Read();
    if (!contextPackResult.Success)
    {
        if (!string.IsNullOrWhiteSpace(contextPackResult.ErrorMessage))
        {
            Console.Error.WriteLine(contextPackResult.ErrorMessage);
        }

        return 1;
    }

    Console.WriteLine(contextPackResult.ToJson());

    return 0;
}


if (args[0] == "trace-slice")
{
    var prompt = string.Join(" ", args.Skip(1)).Trim();
    if (string.IsNullOrWhiteSpace(prompt))
    {
        Console.Error.WriteLine("Missing prompt.");
        return 1;
    }

    var traceSliceResult = RckTraceSliceBuilder.Build(prompt, Directory.GetCurrentDirectory(), 5);
    if (!traceSliceResult.Success)
    {
        if (!string.IsNullOrWhiteSpace(traceSliceResult.ErrorMessage))
        {
            Console.Error.WriteLine(traceSliceResult.ErrorMessage);
        }

        return 1;
    }

    Console.WriteLine(traceSliceResult.Json);
    return 0;
}

if (args[0] == "trace-slice-proposal")
{
    var prompt = string.Join(" ", args.Skip(1)).Trim();
    if (string.IsNullOrWhiteSpace(prompt))
    {
        Console.Error.WriteLine("Missing prompt.");
        return 1;
    }

    var quickIndexResult = RckTraceSliceProposalInputBuilder.Build(Directory.GetCurrentDirectory(), 5);
    if (!quickIndexResult.Success || quickIndexResult.DagQuickIndex is null)
    {
        if (!string.IsNullOrWhiteSpace(quickIndexResult.ErrorMessage))
        {
            Console.Error.WriteLine(quickIndexResult.ErrorMessage);
        }

        return 1;
    }

    var intentAgent = new IntentInferenceAgent();
    var intentTask = new AgentTask(
        id: $"intent-{Guid.NewGuid():N}",
        kind: "infer-intent",
        goal: "infer deterministic operational intent for a trace slice proposal",
        input: prompt,
        expectedOutput: "PromptIntent JSON");
    var intentResult = await intentAgent.ExecuteAsync(intentTask);
    if (intentResult.Status == AgentTaskStatus.Failed || string.IsNullOrWhiteSpace(intentResult.Output))
    {
        foreach (var error in intentResult.Errors)
        {
            Console.Error.WriteLine(error);
        }

        return 1;
    }

    if (!TryBuildTraceSliceProposalIntent(intentResult.Output, out var proposalIntent, out var intentErrorMessage))
    {
        Console.Error.WriteLine(intentErrorMessage);
        return 1;
    }

    var plannerInputJson = JsonSerializer.Serialize(new
    {
        prompt,
        intent = proposalIntent,
        dagQuickIndex = quickIndexResult.DagQuickIndex,
    }, new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    });

    var plannerAgent = new TraceSlicePlannerAgent();
    var plannerTask = new AgentTask(
        id: $"trace-slice-proposal-{Guid.NewGuid():N}",
        kind: "propose-trace-slice",
        goal: "build a deterministic anchor-aware trace slice proposal",
        input: plannerInputJson,
        expectedOutput: "TraceSliceProposal JSON");
    var plannerResult = await plannerAgent.ExecuteAsync(plannerTask);
    if (plannerResult.Status == AgentTaskStatus.Failed || string.IsNullOrWhiteSpace(plannerResult.Output))
    {
        foreach (var error in plannerResult.Errors)
        {
            Console.Error.WriteLine(error);
        }

        return 1;
    }

    Console.WriteLine(plannerResult.Output);
    return 0;
}

if (args[0] == "model")
{
    if (args.Length < 2 || args.Length > 3)
    {
        Console.Error.WriteLine("Usage: rfs model get|set|list <model>");
        return 1;
    }

    if (args[1] == "get")
    {
        if (args.Length != 2)
        {
            Console.Error.WriteLine("Usage: rfs model get");
            return 1;
        }

        var modelConfig = RckWorkspaceModelConfigStore.Read();
        if (!modelConfig.Success)
        {
            if (!string.IsNullOrWhiteSpace(modelConfig.ErrorMessage))
            {
                Console.Error.WriteLine(modelConfig.ErrorMessage);
            }

            return 1;
        }

        Console.WriteLine("rfs model get");
        Console.WriteLine($"  source: {(modelConfig.HasConfiguredDefaultModel ? "workspace" : "default (Pi/RFS)")}");
        Console.WriteLine($"  model: {(modelConfig.HasConfiguredDefaultModel ? modelConfig.DefaultModel : "(inherited)")}");
        return 0;
    }

    if (args[1] == "set")
    {
        if (args.Length != 3)
        {
            Console.Error.WriteLine("Usage: rfs model set <model>");
            return 1;
        }

        var setResult = RckWorkspaceModelConfigStore.SetDefaultModel(args[2]);
        if (!setResult.Success)
        {
            if (!string.IsNullOrWhiteSpace(setResult.ErrorMessage))
            {
                Console.Error.WriteLine(setResult.ErrorMessage);
            }

            return 1;
        }

        Console.WriteLine("rfs model set");
        Console.WriteLine("  source: workspace");
        Console.WriteLine($"  model: {setResult.DefaultModel}");
        Console.WriteLine("  config: .rfs/config.json");
        return 0;
    }

    if (args[1] == "list")
    {
        if (args.Length != 2)
        {
            Console.Error.WriteLine("Usage: rfs model list");
            return 1;
        }

        var modelListResult = await PiRpcClient.GetAvailableModelsAsync(Directory.GetCurrentDirectory());
        if (!modelListResult.Success)
        {
            if (!string.IsNullOrWhiteSpace(modelListResult.ErrorMessage))
            {
                Console.Error.WriteLine(modelListResult.ErrorMessage);
            }

            return 1;
        }

        var currentWorkspaceModel = RckWorkspaceModelConfigStore.TryReadDefaultModel(Directory.GetCurrentDirectory());
        var orderedModels = modelListResult.Models
            .OrderBy(model => model.Provider, StringComparer.OrdinalIgnoreCase)
            .ThenBy(model => model.Id, StringComparer.OrdinalIgnoreCase)
            .ToList();

        Console.WriteLine("Available models:");
        Console.WriteLine();

        if (orderedModels.Count == 0)
        {
            Console.WriteLine("  (no models returned by Pi RPC)");
        }
        else
        {
            var modelWidth = Math.Max(
                "Model".Length,
                orderedModels.Max(model => model.Id.Length + (string.IsNullOrWhiteSpace(model.DisplayName) ? 0 : model.DisplayName!.Length + 3)));
            var providerWidth = Math.Max("Provider".Length, orderedModels.Max(model => model.Provider.Length));

            foreach (var model in orderedModels)
            {
                var marker = string.Equals(model.Id, currentWorkspaceModel, StringComparison.Ordinal) ? "*" : " ";
                var modelLabel = string.IsNullOrWhiteSpace(model.DisplayName)
                    ? model.Id
                    : $"{model.Id} - {model.DisplayName}";

                Console.WriteLine($"{marker} {modelLabel.PadRight(modelWidth)}  {model.Provider.PadRight(providerWidth)}");
            }
        }

        Console.WriteLine();
        Console.WriteLine("Current workspace model:");
        Console.WriteLine($"  {(string.IsNullOrWhiteSpace(currentWorkspaceModel) ? "(inherited)" : currentWorkspaceModel)}");

        return 0;
    }

    Console.Error.WriteLine("Unknown model command.");
    return 1;
}

if (args[0] == "pi")
{
    var message = string.Join(" ", args.Skip(1));

    var psi = new ProcessStartInfo
    {
        FileName = "pi",
        UseShellExecute = false
    };

    if (!string.IsNullOrWhiteSpace(message))
    {
        psi.ArgumentList.Add(message);
    }

    ApplyWorkspaceModelEnvironment(psi);

    Process? process;

    try
    {
        process = Process.Start(psi);
    }
    catch (Exception)
    {
        Console.Error.WriteLine("Failed to start pi.");
        return 1;
    }

    if (process is null)
    {
        Console.Error.WriteLine("Failed to start pi.");
        return 1;
    }

    await process.WaitForExitAsync();
    return process.ExitCode;
}

if (args[0] == "agent")
{
    var agentArgs = args.Skip(1).ToArray();
    if (agentArgs.Length > 0 && agentArgs[0] == "--raw")
    {
        Console.Error.WriteLine("rfs agent --raw is no longer supported. Use rfs agent \"<task>\".");
        return 1;
    }

    var recordInteraction = agentArgs.Any(arg => string.Equals(arg, "--record", StringComparison.Ordinal));
    var task = string.Join(" ", agentArgs.Where(arg => !string.Equals(arg, "--record", StringComparison.Ordinal))).Trim();

    if (string.IsNullOrWhiteSpace(task))
    {
        Console.Error.WriteLine("Missing task.");
        return 1;
    }

    const string helperRelativePath = "rfs-agent.mjs";
    var helperPath = FindBridgeHelperPath(helperRelativePath);
    if (helperPath is null)
    {
        Console.Error.WriteLine($"rfs agent helper not found: tools/rfs/bridge/{helperRelativePath}");
        return 1;
    }

    var psi = new ProcessStartInfo
    {
        FileName = "node",
        UseShellExecute = false,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        WorkingDirectory = Directory.GetCurrentDirectory()
    };

    psi.Environment["RFS_REPO_ROOT"] = Directory.GetCurrentDirectory();
    ApplyWorkspaceModelEnvironment(psi);
    psi.ArgumentList.Add(helperPath);
    psi.ArgumentList.Add(task);

    Process? process;

    try
    {
        process = Process.Start(psi);
    }
    catch (Exception)
    {
        Console.Error.WriteLine("Failed to start rfs agent helper.");
        return 1;
    }

    if (process is null)
    {
        Console.Error.WriteLine("Failed to start rfs agent helper.");
        return 1;
    }

    var finalAssistantAnswer = string.Empty;
    var recordedTools = new List<RckInteractionTool>();

    void WriteOutLine(string text)
    {
        Console.Out.WriteLine(text);
        Console.Out.Flush();
    }

    void WriteErrorLine(string text)
    {
        Console.Error.WriteLine(text);
        Console.Error.Flush();
    }

    string? ExtractToolPath(string details)
    {
        if (string.IsNullOrWhiteSpace(details))
        {
            return null;
        }

        var firstSpaceIndex = details.IndexOf(' ');
        if (firstSpaceIndex < 0)
        {
            return null;
        }

        var toolArgs = details[(firstSpaceIndex + 1)..].Trim();
        foreach (var part in toolArgs.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (part.StartsWith("path=", StringComparison.Ordinal))
            {
                return part["path=".Length..];
            }
        }

        return null;
    }

    string FormatToolLabel(string details)
    {
        if (string.IsNullOrWhiteSpace(details))
        {
            return "tool";
        }

        var firstSpaceIndex = details.IndexOf(' ');
        if (firstSpaceIndex < 0)
        {
            return details;
        }

        var toolName = details[..firstSpaceIndex].Trim();
        var toolPath = ExtractToolPath(details);

        if (!string.IsNullOrWhiteSpace(toolPath) && (toolName == "list_directory" || toolName == "read_file"))
        {
            return $"{toolName} {toolPath}";
        }

        return details;
    }

    string FormatStartLabel(string toolName, string details)
    {
        var labelInput = string.IsNullOrWhiteSpace(details) ? toolName : $"{toolName} {details}";
        return FormatToolLabel(labelInput);
    }

    void PrintHeader()
    {
        WriteOutLine(string.Empty);
        WriteOutLine("Rufus Agent");
        WriteOutLine("───────────");
        WriteOutLine("Task");
        WriteOutLine(task);
        WriteOutLine("Mode: headless, read-only");
        WriteOutLine("Scope: repository root");
        WriteOutLine(string.Empty);
        WriteOutLine("Actions");
        WriteOutLine("───────────");
    }

    void PrintAnswerHeader()
    {
        WriteOutLine(string.Empty);
        WriteOutLine("Answer");
        WriteOutLine("────────────────────────────────────────────");
    }

    string FormatAssistantPayload(string payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
        {
            return string.Empty;
        }

        var normalized = payload.Replace("\r\n", "\n").Replace("\r", "\n");
        var outputLines = new List<string>();

        foreach (var rawLine in normalized.Split('\n'))
        {
            var line = rawLine.TrimEnd();
            var trimmed = line.Trim();

            if (trimmed.Length == 0)
            {
                if (outputLines.Count > 0 && outputLines[^1].Length > 0)
                {
                    outputLines.Add(string.Empty);
                }

                continue;
            }

            line = Regex.Replace(line, @"^\s*#+\s+(?=#{2,4}\s)", string.Empty);

            if (Regex.IsMatch(trimmed, @"^#{1,6}\s*$"))
            {
                continue;
            }

            var formattedLine = Regex.Replace(line, @"(?<=[^\s#])(#{2,4}\s)", "\n$1");
            formattedLine = Regex.Replace(formattedLine, @"(?<=\S)(-\s)", "\n$1");
            formattedLine = Regex.Replace(formattedLine, @"(?<=\S)(\d+\.\s)", "\n$1");

            foreach (var piece in formattedLine.Split('\n', StringSplitOptions.None))
            {
                var candidate = piece.TrimEnd();
                if (candidate.Length == 0)
                {
                    if (outputLines.Count > 0 && outputLines[^1].Length > 0)
                    {
                        outputLines.Add(string.Empty);
                    }

                    continue;
                }

                outputLines.Add(candidate);
            }
        }

        while (outputLines.Count > 0 && outputLines[^1].Length == 0)
        {
            outputLines.RemoveAt(outputLines.Count - 1);
        }

        return string.Join("\n", outputLines);
    }

    var labelsById = new Dictionary<string, string>(StringComparer.Ordinal);
    var answerHeaderPrinted = false;
    var assistantPrinted = false;
    var assistantBuffer = new StringBuilder();
    var streamGate = new object();

    PrintHeader();

    void HandleAssistantLine(string payload)
    {
        if (payload.Length == 0)
        {
            if (assistantBuffer.Length > 0)
            {
                assistantBuffer.AppendLine();
            }

            return;
        }

        assistantPrinted = true;
        assistantBuffer.Append(payload);
    }

    void HandleToolStart(string payload)
    {
        var parts = payload.Split(' ', 3, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2)
        {
            WriteOutLine($"→ {payload}");
            return;
        }

        var toolCallId = parts[0].StartsWith("id=", StringComparison.Ordinal) ? parts[0]["id=".Length..] : string.Empty;
        var toolName = parts[1].StartsWith("name=", StringComparison.Ordinal) ? parts[1]["name=".Length..] : parts[0];
        var details = parts.Length > 2 ? parts[2] : string.Empty;
        var label = FormatStartLabel(toolName, details);

        lock (streamGate)
        {
            if (!string.IsNullOrWhiteSpace(toolCallId))
            {
                labelsById[toolCallId] = label;
            }
        }

        WriteOutLine($"→ {label}");
    }

    void HandleToolEnd(string payload)
    {
        var parts = payload.Split(' ', 3, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2)
        {
            WriteOutLine($"✓ {payload}");
            return;
        }

        var toolCallId = parts[0].StartsWith("id=", StringComparison.Ordinal) ? parts[0]["id=".Length..] : string.Empty;
        var toolName = parts[1].StartsWith("name=", StringComparison.Ordinal) ? parts[1]["name=".Length..] : parts[0];
        var summary = parts.Length > 2 ? parts[2] : string.Empty;
        var label = string.Empty;

        lock (streamGate)
        {
            if (!string.IsNullOrWhiteSpace(toolCallId) && labelsById.TryGetValue(toolCallId, out var storedLabel))
            {
                label = storedLabel;
                labelsById.Remove(toolCallId);
            }
        }

        if (string.IsNullOrWhiteSpace(label))
        {
            label = FormatStartLabel(toolName, summary);
        }

        if (recordInteraction && !string.IsNullOrWhiteSpace(toolName))
        {
            recordedTools.Add(RckInteractionTool.Completed(toolName));
        }

        WriteOutLine($"✓ {label}");
        if (!string.IsNullOrWhiteSpace(summary) && summary != label)
        {
            WriteOutLine($"  {summary}");
        }
    }

    async Task PumpOutputAsync()
    {
        while (true)
        {
            var line = await process.StandardOutput.ReadLineAsync();
            if (line is null)
            {
                break;
            }

            if (line.StartsWith("[agent:start]", StringComparison.Ordinal))
            {
                continue;
            }

            if (line.StartsWith("[tool:start] ", StringComparison.Ordinal))
            {
                HandleToolStart(line["[tool:start] ".Length..]);
                continue;
            }

            if (line.StartsWith("[tool:end] ", StringComparison.Ordinal))
            {
                HandleToolEnd(line["[tool:end] ".Length..]);
                continue;
            }

            if (line.StartsWith("[assistant] ", StringComparison.Ordinal))
            {
                HandleAssistantLine(line["[assistant] ".Length..]);
                continue;
            }

            if (line.StartsWith("[agent:end]", StringComparison.Ordinal))
            {
                if (!answerHeaderPrinted)
                {
                    PrintAnswerHeader();
                    answerHeaderPrinted = true;
                }

                var formattedAnswer = assistantPrinted ? FormatAssistantPayload(assistantBuffer.ToString()) : string.Empty;
                finalAssistantAnswer = string.IsNullOrWhiteSpace(formattedAnswer)
                    ? assistantBuffer.ToString().Trim()
                    : formattedAnswer;
                if (finalAssistantAnswer.Length == 0)
                {
                    WriteOutLine("(no assistant output)");
                }
                else
                {
                    foreach (var answerLine in finalAssistantAnswer.Split('\n', StringSplitOptions.None))
                    {
                        WriteOutLine(answerLine);
                    }
                }

                continue;
            }

            HandleAssistantLine(line);
        }
    }

    async Task PumpErrorAsync()
    {
        while (true)
        {
            var line = await process.StandardError.ReadLineAsync();
            if (line is null)
            {
                break;
            }

            WriteErrorLine(line);
        }
    }

    var stdoutTask = PumpOutputAsync();
    var stderrTask = PumpErrorAsync();

    await process.WaitForExitAsync();
    await Task.WhenAll(stdoutTask, stderrTask);

    if (!answerHeaderPrinted)
    {
        PrintAnswerHeader();
        if (!assistantPrinted)
        {
            WriteOutLine("(no assistant output)");
        }
    }

    if (recordInteraction && process.ExitCode == 0)
    {
        if (string.IsNullOrWhiteSpace(finalAssistantAnswer))
        {
            Console.Error.WriteLine("rfs agent --record did not capture a final assistant answer.");
            return 1;
        }

        var recordResult = RckInteractionRecorder.RecordAgent(task, finalAssistantAnswer, recordedTools);
        if (!recordResult.Success)
        {
            if (!string.IsNullOrWhiteSpace(recordResult.ErrorMessage))
            {
                Console.Error.WriteLine(recordResult.ErrorMessage);
            }

            return 1;
        }

        foreach (var line in recordResult.FormatConsoleLines())
        {
            Console.WriteLine(line);
        }
    }

    return process.ExitCode;
}

if (args[0] == "agent-json")
{
    const string experimentalWarning = "Experimental: relies on Pi --tools enforcement for read-only behavior.";
    Console.Error.WriteLine(experimentalWarning);

    var task = string.Join(" ", args.Skip(1));
    if (string.IsNullOrWhiteSpace(task))
    {
        Console.Error.WriteLine("Missing task.");
        return 1;
    }

    var agentResult = await PiJsonEventRunner.RunAgentAsync(
        Directory.GetCurrentDirectory(),
        task,
        RckWorkspaceModelConfigStore.TryReadDefaultModel(Directory.GetCurrentDirectory()));

    if (!agentResult.Success)
    {
        if (!string.IsNullOrWhiteSpace(agentResult.ErrorMessage))
        {
            Console.Error.WriteLine(agentResult.ErrorMessage);
        }

        return 1;
    }

    Console.WriteLine();
    Console.WriteLine("Rufus Agent JSON Prototype");
    Console.WriteLine("────────────────────────────");
    Console.WriteLine("Task:");
    Console.WriteLine($"  {agentResult.Task}");
    Console.WriteLine();
    Console.WriteLine("Warning:");
    Console.WriteLine($"  {experimentalWarning}");
    Console.WriteLine();

    if (agentResult.ToolEvents != null && agentResult.ToolEvents.Count > 0)
    {
        Console.WriteLine("Actions:");
        foreach (var ev in agentResult.ToolEvents)
        {
            var label = ev.Name ?? ev.Type;
            if (!string.IsNullOrWhiteSpace(ev.Details))
            {
                label += $" {ev.Details}";
            }

            if (!string.IsNullOrWhiteSpace(ev.Summary))
            {
                Console.WriteLine($"→ {label} — {ev.Summary}");
            }
            else
            {
                Console.WriteLine($"→ {label}");
            }
        }

        Console.WriteLine();
    }

    Console.WriteLine("Answer:");
    Console.WriteLine("────────────────────────────────────────────");
    if (string.IsNullOrWhiteSpace(agentResult.Answer))
    {
        Console.WriteLine("(no assistant output)");
    }
    else
    {
        foreach (var answerLine in agentResult.Answer.Split('\n', StringSplitOptions.None))
        {
            Console.WriteLine(answerLine);
        }
    }

    if (!string.IsNullOrWhiteSpace(agentResult.Provider) || !string.IsNullOrWhiteSpace(agentResult.Model))
    {
        Console.WriteLine();
        Console.WriteLine("Source:");
        Console.WriteLine($"  provider: {(string.IsNullOrWhiteSpace(agentResult.Provider) ? "(unknown)" : agentResult.Provider)}");
        Console.WriteLine($"  model: {(string.IsNullOrWhiteSpace(agentResult.Model) ? "(unknown)" : agentResult.Model)}");
    }

    return 0;
}

if (args[0] == "intent")
{
    var intentArgs = args.Skip(1).ToArray();
    var recordIntent = intentArgs.Any(arg => string.Equals(arg, "--record", StringComparison.Ordinal));
    var prompt = string.Join(" ", intentArgs.Where(arg => !string.Equals(arg, "--record", StringComparison.Ordinal))).Trim();
    if (string.IsNullOrWhiteSpace(prompt))
    {
        Console.Error.WriteLine("Missing prompt.");
        return 1;
    }

    var intentAgent = new IntentInferenceAgent();
    var task = new AgentTask(
        id: $"intent-{Guid.NewGuid():N}",
        kind: "infer-intent",
        goal: "inferir intent operativo del prompt",
        input: prompt,
        expectedOutput: "PromptIntent JSON");
    var result = await intentAgent.ExecuteAsync(task);

    Console.WriteLine();
    Console.WriteLine("Rufus Intent");
    Console.WriteLine("────────────");
    Console.WriteLine("Prompt:");
    Console.WriteLine($"  {prompt}");
    Console.WriteLine();
    Console.WriteLine("Result:");
    Console.WriteLine($"  Status: {result.Status}");
    Console.WriteLine($"  AgentId: {result.AgentId}");
    Console.WriteLine($"  ExecutionModel: {result.ExecutionModel.Provider}/{result.ExecutionModel.Model}");
    Console.WriteLine($"  Summary: {FormatOptional(result.Summary)}");
    Console.WriteLine("  Output:");
    WriteIndentedBlock(result.Output, "    ");
    Console.WriteLine("  Evidence:");
    WriteEvidenceBlock(result.Evidence, "    ");
    Console.WriteLine("  Warnings:");
    WriteStringListBlock(result.Warnings, "    ");

    if (result.Errors.Count > 0)
    {
        Console.WriteLine("  Errors:");
        WriteStringListBlock(result.Errors, "    ");
    }

    if (recordIntent)
    {
        if (!TryBuildIntentRecordInput(task, result, prompt, out var recordInput, out var errorMessage))
        {
            Console.Error.WriteLine(errorMessage);
            return 1;
        }

        var recorderResult = RckAgentTaskRecorder.RecordIntent(recordInput, Directory.GetCurrentDirectory());
        foreach (var line in recorderResult.FormatConsoleLines())
        {
            Console.WriteLine(line);
        }

        if (!recorderResult.Success)
        {
            return 1;
        }
    }

    return result.Status == AgentTaskStatus.Failed ? 1 : 0;
}

if (args[0] == "ask-json")
{
    var prompt = string.Join(" ", args.Skip(1));
    if (string.IsNullOrWhiteSpace(prompt))
    {
        Console.Error.WriteLine("Missing prompt.");
        return 1;
    }

    var askJsonResult = await PiJsonEventRunner.RunAskAsync(
        Directory.GetCurrentDirectory(),
        prompt,
        RckWorkspaceModelConfigStore.TryReadDefaultModel(Directory.GetCurrentDirectory()));

    if (!askJsonResult.Success)
    {
        if (!string.IsNullOrWhiteSpace(askJsonResult.ErrorMessage))
        {
            Console.Error.WriteLine(askJsonResult.ErrorMessage);
        }

        return 1;
    }

    Console.WriteLine();
    Console.WriteLine("Rufus Ask JSON Prototype");
    Console.WriteLine("────────────────────────");
    Console.WriteLine("Prompt:");
    Console.WriteLine($"  {askJsonResult.Prompt}");
    Console.WriteLine();
    Console.WriteLine("Answer:");
    foreach (var answerLine in askJsonResult.Answer.Split('\n', StringSplitOptions.None))
    {
        Console.WriteLine(answerLine.Length == 0 ? string.Empty : $"  {answerLine}");
    }

    if (!string.IsNullOrWhiteSpace(askJsonResult.Provider) || !string.IsNullOrWhiteSpace(askJsonResult.Model))
    {
        Console.WriteLine();
        Console.WriteLine("Source:");
        Console.WriteLine($"  provider: {(string.IsNullOrWhiteSpace(askJsonResult.Provider) ? "(unknown)" : askJsonResult.Provider)}");
        Console.WriteLine($"  model: {(string.IsNullOrWhiteSpace(askJsonResult.Model) ? "(unknown)" : askJsonResult.Model)}");
    }

    return 0;
}

if (args[0] == "ask")
{
    var askArgs = args.Skip(1).ToArray();
    var recordInteraction = askArgs.Any(arg => string.Equals(arg, "--record", StringComparison.Ordinal));
    var prompt = string.Join(" ", askArgs.Where(arg => !string.Equals(arg, "--record", StringComparison.Ordinal)));

    if (string.IsNullOrWhiteSpace(prompt))
    {
        Console.Error.WriteLine("Missing prompt.");
        return 1;
    }

    if (IsLegacyAskBridgeEnabled())
    {
        const string helperRelativePath = "rfs-ask.mjs";
        var helperPath = FindBridgeHelperPath(helperRelativePath);
        if (helperPath is null)
        {
            Console.Error.WriteLine($"rfs ask helper not found: tools/rfs/bridge/{helperRelativePath}");
            return 1;
        }

        var psi = new ProcessStartInfo
        {
            FileName = "node",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        psi.ArgumentList.Add(helperPath);
        psi.ArgumentList.Add(prompt);
        ApplyWorkspaceModelEnvironment(psi);

        Process? process;

        try
        {
            process = Process.Start(psi);
        }
        catch (Exception)
        {
            Console.Error.WriteLine("Failed to start rfs ask helper.");
            return 1;
        }

        if (process is null)
        {
            Console.Error.WriteLine("Failed to start rfs ask helper.");
            return 1;
        }

        Console.WriteLine();
        Console.WriteLine("Rufus Ask");
        Console.WriteLine("─────────");
        Console.WriteLine(prompt);
        Console.WriteLine();
        Console.WriteLine("Answer");
        Console.WriteLine("────────────────────────────────────────────");

        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();

        await process.WaitForExitAsync();
        var stdoutText = await stdoutTask;
        var stderrText = await stderrTask;

        if (!string.IsNullOrWhiteSpace(stderrText))
        {
            Console.Error.Write(stderrText);
        }

        var finalAssistantAnswer = stdoutText.TrimEnd();
        if (string.IsNullOrWhiteSpace(finalAssistantAnswer))
        {
            Console.WriteLine("(no assistant output)");
        }
        else
        {
            Console.Write(stdoutText);
            if (!stdoutText.EndsWith(Environment.NewLine, StringComparison.Ordinal))
            {
                Console.WriteLine();
            }
        }

        if (recordInteraction && process.ExitCode == 0)
        {
            var recordResult = RckInteractionRecorder.RecordAsk(prompt, finalAssistantAnswer);
            if (!recordResult.Success)
            {
                if (!string.IsNullOrWhiteSpace(recordResult.ErrorMessage))
                {
                    Console.Error.WriteLine(recordResult.ErrorMessage);
                }

                return 1;
            }

            foreach (var line in recordResult.FormatConsoleLines())
            {
                Console.WriteLine(line);
            }
        }

        return process.ExitCode;
    }

    var askJsonResult = await PiJsonEventRunner.RunAskAsync(
        Directory.GetCurrentDirectory(),
        prompt,
        RckWorkspaceModelConfigStore.TryReadDefaultModel(Directory.GetCurrentDirectory()));

    if (!askJsonResult.Success)
    {
        if (!string.IsNullOrWhiteSpace(askJsonResult.ErrorMessage))
        {
            Console.Error.WriteLine(askJsonResult.ErrorMessage);
        }

        return 1;
    }

    Console.WriteLine();
    Console.WriteLine("Rufus Ask");
    Console.WriteLine("─────────");
    Console.WriteLine(prompt);
    Console.WriteLine();
    Console.WriteLine("Answer");
    Console.WriteLine("────────────────────────────────────────────");

    if (string.IsNullOrWhiteSpace(askJsonResult.Answer))
    {
        Console.WriteLine("(no assistant output)");
    }
    else
    {
        foreach (var answerLine in askJsonResult.Answer.Split('\n', StringSplitOptions.None))
        {
            Console.WriteLine(answerLine);
        }
    }

    if (recordInteraction)
    {
        var recordResult = RckInteractionRecorder.RecordAsk(prompt, askJsonResult.Answer);
        if (!recordResult.Success)
        {
            if (!string.IsNullOrWhiteSpace(recordResult.ErrorMessage))
            {
                Console.Error.WriteLine(recordResult.ErrorMessage);
            }

            return 1;
        }

        foreach (var line in recordResult.FormatConsoleLines())
        {
            Console.WriteLine(line);
        }
    }

    return 0;
}
Console.Error.WriteLine($"Unknown command: {args[0]}");
return 1;

static void PrintHelp()
{
    Console.WriteLine("rfs - Rufus CLI proof of concept");
    Console.WriteLine("Usage:");
    Console.WriteLine("  rfs --version");
    Console.WriteLine("  rfs help");
    Console.WriteLine("  rfs init   = bootstrap .rfs + RCK genesis state/anchor");
    Console.WriteLine("  rfs status = show local rfs/RCK workspace status");
    Console.WriteLine("  rfs log    = show active RCK cognitive history");
    Console.WriteLine("  rfs context-pack = export full RCK DAG context pack as JSON");
    Console.WriteLine("  rfs context-pack --trace-slice <prompt>");
    Console.WriteLine("  rfs trace-slice <prompt>");
    Console.WriteLine("  rfs trace-slice-proposal <prompt>");
    Console.WriteLine("  rfs model get");
    Console.WriteLine("  rfs model set <model>");
    Console.WriteLine("  rfs model list");
    Console.WriteLine("  rfs pi [message]");
    Console.WriteLine("  rfs ask [--record] <prompt>");
    Console.WriteLine("  rfs ask-json <prompt>");
    Console.WriteLine("  rfs agent [--record] <task>");
    Console.WriteLine("  rfs agent-json <task>");
    Console.WriteLine("  rfs intent [--record] <prompt>");
    Console.WriteLine();
    Console.WriteLine("Modos:");
    Console.WriteLine("  pi         = passthrough interactivo a Pi TUI");
    Console.WriteLine("  ask        = prompt único headless con JSON Event Stream; fallback legacy vía RFS_USE_LEGACY_ASK_BRIDGE=1");
    Console.WriteLine("  ask-json   = prototipo experimental con Pi JSON Event Stream");
    Console.WriteLine("  agent      = agente headless con tools read-only + streaming");
    Console.WriteLine("  agent-json = prototipo experimental con Pi JSON Event Stream; relies on Pi --tools enforcement for read-only behavior");
    Console.WriteLine("  intent     = ejecuta IntentInferenceAgent mock/determinístico con opción --record para RCK");
    Console.WriteLine("  trace-slice = exporta un corte determinístico del RCK activo como JSON");
    Console.WriteLine("  trace-slice-proposal = prototipo experimental determinístico/anchor-aware; emite JSON puro sin escribir RCK");
    Console.WriteLine("  context-pack --trace-slice = materializa un context-pack focalizado desde TraceSlice v0 sin escribir RCK");
}

static bool IsLegacyAskBridgeEnabled()
{
    return string.Equals(Environment.GetEnvironmentVariable("RFS_USE_LEGACY_ASK_BRIDGE"), "1", StringComparison.Ordinal);
}

static string FormatOptional(string? value)
{
    return string.IsNullOrWhiteSpace(value) ? "(none)" : value;
}

static bool TryBuildIntentRecordInput(
    AgentTask task,
    AgentTaskResult result,
    string prompt,
    out RckAgentTaskRecordInput recordInput,
    out string? errorMessage)
{
    recordInput = null!;
    errorMessage = null;

    if (string.IsNullOrWhiteSpace(result.Output))
    {
        errorMessage = "[rck] record: intent inference output is empty.";
        return false;
    }

    try
    {
        using var document = JsonDocument.Parse(result.Output);
        var root = document.RootElement;

        var intent = GetRequiredString(root, "Intent");
        var summary = GetRequiredString(root, "Summary");
        var entities = GetStringArray(root, "Entities");
        var constraints = GetStringArray(root, "Constraints");

        var executionModel = result.ExecutionModel;
        recordInput = new RckAgentTaskRecordInput(
            TaskId: task.Id,
            TaskKind: task.Kind,
            AgentId: result.AgentId,
            Status: result.Status.ToString(),
            TaskSummary: FormatOptional(result.Summary),
            GoalSummary: prompt,
            InputSummary: prompt,
            ExecutionProvider: executionModel.Provider,
            ExecutionModel: executionModel.Model,
            OutputKind: "intent",
            OutputSummary: summary,
            OutputData: new RckIntentProjection(intent, entities, constraints),
            Warnings: result.Warnings,
            Errors: result.Errors);
        return true;
    }
    catch (Exception ex)
    {
        errorMessage = $"[rck] record: failed to project intent result: {ex.Message}";
        return false;
    }
}

static string GetRequiredString(JsonElement element, string propertyName)
{
    if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.String)
    {
        throw new InvalidDataException($"Missing string property '{propertyName}'.");
    }

    return property.GetString() ?? string.Empty;
}

static IReadOnlyList<string> GetStringArray(JsonElement element, string propertyName)
{
    if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.Array)
    {
        return Array.Empty<string>();
    }

    var values = new List<string>();
    foreach (var item in property.EnumerateArray())
    {
        if (item.ValueKind != JsonValueKind.String)
        {
            continue;
        }

        var value = item.GetString();
        if (!string.IsNullOrWhiteSpace(value))
        {
            values.Add(value);
        }
    }

    return values;
}

static void WriteIndentedBlock(string? value, string indent)
{
    if (string.IsNullOrWhiteSpace(value))
    {
        Console.WriteLine($"{indent}(none)");
        return;
    }

    foreach (var line in value.Split('\n', StringSplitOptions.None))
    {
        Console.WriteLine($"{indent}{line}");
    }
}

static void WriteStringListBlock(System.Collections.IEnumerable values, string indent)
{
    var count = 0;
    foreach (var value in values)
    {
        if (value is string text)
        {
            Console.WriteLine($"{indent}- {text}");
            count++;
        }
    }

    if (count == 0)
    {
        Console.WriteLine($"{indent}(none)");
    }
}

static void WriteEvidenceBlock(System.Collections.IEnumerable evidenceItems, string indent)
{
    var count = 0;
    foreach (var item in evidenceItems)
    {
        var kind = (string?)item.GetType().GetProperty("Kind")?.GetValue(item);
        var source = (string?)item.GetType().GetProperty("Source")?.GetValue(item);
        var detail = (string?)item.GetType().GetProperty("Detail")?.GetValue(item);
        var line = $"{indent}- {FormatOptional(kind)} | {FormatOptional(source)}";
        if (!string.IsNullOrWhiteSpace(detail))
        {
            line += $" | {detail}";
        }

        Console.WriteLine(line);
        count++;
    }

    if (count == 0)
    {
        Console.WriteLine($"{indent}(none)");
    }
}

static void ApplyWorkspaceModelEnvironment(ProcessStartInfo processStartInfo)
{
    var workspaceModel = RckWorkspaceModelConfigStore.TryReadDefaultModel(Directory.GetCurrentDirectory());
    if (!string.IsNullOrWhiteSpace(workspaceModel))
    {
        processStartInfo.Environment["RUFUSCHAT_LLM_MODEL"] = workspaceModel;
    }
}

static string? FindBridgeHelperPath(string helperFileName)
{
    var bridgeRoot = FindRfsBridgeRoot();
    if (bridgeRoot is null)
    {
        return null;
    }

    var helperPath = Path.Combine(bridgeRoot, helperFileName);
    return File.Exists(helperPath) ? helperPath : null;
}

static string? FindRfsBridgeRoot()
{
    var current = new DirectoryInfo(AppContext.BaseDirectory);

    while (current is not null)
    {
        var candidate = Path.Combine(current.FullName, "tools", "rfs", "bridge");
        if (Directory.Exists(candidate))
        {
            return candidate;
        }

        current = current.Parent;
    }

    return null;
}

static bool IsHelpCommand(string command)
{
    return command is "help" or "--help" or "-h";
}

static bool TryBuildTraceSliceProposalIntent(string intentOutputJson, out object intentProjection, out string? errorMessage)
{
    intentProjection = null!;
    errorMessage = null;

    try
    {
        using var document = JsonDocument.Parse(intentOutputJson);
        var root = document.RootElement;
        intentProjection = new
        {
            kind = GetRequiredString(root, "Intent"),
            summary = GetRequiredString(root, "Summary"),
            source = "intent-inference-agent",
        };
        return true;
    }
    catch (Exception ex)
    {
        errorMessage = $"rfs trace-slice-proposal: failed to project intent result: {ex.Message}";
        return false;
    }
}
