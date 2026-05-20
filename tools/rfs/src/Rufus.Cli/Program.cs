using System.Diagnostics;

if (args.Length == 0)
{
    Console.WriteLine("rfs - Rufus CLI proof of concept");
    Console.WriteLine("Usage:");
    Console.WriteLine("  rfs --version");
    Console.WriteLine("  rfs pi [message]");
    Console.WriteLine("  rfs ask \"message\"");
    Console.WriteLine("  rfs agent [--raw] \"task\"");
    return 0;
}

if (args[0] == "--version")
{
    Console.WriteLine("rfs 0.0.1-poc");
    return 0;
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
    var rawOutput = false;

    if (agentArgs.Length > 0 && agentArgs[0] == "--raw")
    {
        rawOutput = true;
        agentArgs = agentArgs.Skip(1).ToArray();
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

    if (rawOutput)
    {
        process.OutputDataReceived += (_, eventArgs) =>
        {
            if (eventArgs.Data is not null)
            {
                Console.Out.WriteLine(eventArgs.Data);
            }
        };

        process.ErrorDataReceived += (_, eventArgs) =>
        {
            if (eventArgs.Data is not null)
            {
                Console.Error.WriteLine(eventArgs.Data);
            }
        };

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        await process.WaitForExitAsync();
        process.WaitForExit();
        return process.ExitCode;
    }

    void WriteOutLine(string text)
    {
        Console.Out.WriteLine(text);
        Console.Out.Flush();
    }

    void WriteOut(string text)
    {
        Console.Out.Write(text);
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

    var labelsById = new Dictionary<string, string>(StringComparer.Ordinal);
    var answerHeaderPrinted = false;
    var assistantPrinted = false;
    var assistantLineOpen = false;
    var streamGate = new object();

    PrintHeader();

    void HandleAssistantLine(string payload)
    {
        if (!answerHeaderPrinted)
        {
            PrintAnswerHeader();
            answerHeaderPrinted = true;
        }

        if (string.IsNullOrWhiteSpace(payload))
        {
            return;
        }

        assistantPrinted = true;
        if (!assistantLineOpen)
        {
            WriteOut("[assistant] ");
            assistantLineOpen = true;
        }

        WriteOut(payload);
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

                if (assistantLineOpen)
                {
                    WriteOutLine(string.Empty);
                    assistantLineOpen = false;
                }

                if (!assistantPrinted)
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
    var prompt = string.Join(" ", args.Skip(1));

    if (string.IsNullOrWhiteSpace(prompt))
    {
        Console.Error.WriteLine("Missing prompt.");
        return 1;
    }

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
        UseShellExecute = false
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

    await process.WaitForExitAsync();
    return process.ExitCode;
}

Console.Error.WriteLine($"Unknown command: {args[0]}");
return 1;

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
