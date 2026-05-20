using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
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

if (args[0] == "init")
{
    var initResult = RckWorkspaceInitializer.Initialize();
    foreach (var line in initResult.FormatConsoleLines())
    {
        Console.WriteLine(line);
    }

    return initResult.Success ? 0 : 1;
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

    var task = string.Join(" ", agentArgs).Trim();

    if (string.IsNullOrWhiteSpace(task))
    {
        Console.Error.WriteLine("Missing task.");
        return 1;
    }

    const string helperRelativePath = "tools/rfs/bridge/rfs-agent.mjs";
    var helperPath = FindRepoFile(helperRelativePath);
    if (helperPath is null)
    {
        Console.Error.WriteLine($"rfs agent helper not found: {helperRelativePath}");
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

                if (assistantPrinted)
                {
                    var formattedAnswer = FormatAssistantPayload(assistantBuffer.ToString());
                    if (formattedAnswer.Length == 0)
                    {
                        WriteOutLine("(no assistant output)");
                    }
                    else
                    {
                        foreach (var answerLine in formattedAnswer.Split('\n', StringSplitOptions.None))
                        {
                            WriteOutLine(answerLine);
                        }
                    }
                }
                else
                {
                    WriteOutLine("(no assistant output)");
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

    return process.ExitCode;
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

    var task = prompt;

    const string helperRelativePath = "tools/rfs/bridge/rfs-ask.mjs";
    var helperPath = FindRepoFile(helperRelativePath);
    if (helperPath is null)
    {
        Console.Error.WriteLine($"rfs ask helper not found: {helperRelativePath}");
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
    Console.WriteLine(task);
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
Console.Error.WriteLine($"Unknown command: {args[0]}");
return 1;

static void PrintHelp()
{
    Console.WriteLine("rfs - Rufus CLI proof of concept");
    Console.WriteLine("Usage:");
    Console.WriteLine("  rfs --version");
    Console.WriteLine("  rfs help");
    Console.WriteLine("  rfs init   = bootstrap .rfs + RCK genesis state/anchor");
    Console.WriteLine("  rfs pi [message]");
    Console.WriteLine("  rfs ask [--record] <prompt>");
    Console.WriteLine("  rfs agent <task>");
    Console.WriteLine();
    Console.WriteLine("Modos:");
    Console.WriteLine("  pi     = passthrough interactivo a Pi TUI");
    Console.WriteLine("  ask    = prompt único headless sin tools");
    Console.WriteLine("  agent  = agente headless con tools read-only + streaming");
}

static bool IsHelpCommand(string command)
{
    return command is "help" or "--help" or "-h";
}
static string? FindRepoFile(string relativePath)
{
    var currentDirectory = Directory.GetCurrentDirectory();
    var current = new DirectoryInfo(currentDirectory);

    while (current is not null)
    {
        var candidate = Path.Combine(current.FullName, relativePath);
        if (File.Exists(candidate))
        {
            return candidate;
        }

        current = current.Parent;
    }

    return null;
}
