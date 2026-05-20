using System.Diagnostics;
using System.Text.RegularExpressions;

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
    var preferredPath = InferPreferredPath(task);

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

    var capturedLines = new List<(bool IsError, string Text)>();
    var captureGate = new object();
    var currentActionLabel = string.Empty;
    var currentActionVisible = false;
    var answerLines = new List<string>();

    process.OutputDataReceived += (_, eventArgs) =>
    {
        if (eventArgs.Data is null)
        {
            return;
        }

        lock (captureGate)
        {
            capturedLines.Add((false, eventArgs.Data));
        }
    };

    process.ErrorDataReceived += (_, eventArgs) =>
    {
        if (eventArgs.Data is null)
        {
            return;
        }

        lock (captureGate)
        {
            capturedLines.Add((true, eventArgs.Data));
        }
    };

    process.BeginOutputReadLine();
    process.BeginErrorReadLine();
    await process.WaitForExitAsync();
    process.WaitForExit();

    void AppendAnswer(string text)
    {
        if (!string.IsNullOrWhiteSpace(text))
        {
            answerLines.Add(text);
        }
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

    bool ShouldRenderAction(string details)
    {
        if (string.IsNullOrWhiteSpace(preferredPath))
        {
            return true;
        }

        var toolPath = ExtractToolPath(details);
        if (string.IsNullOrWhiteSpace(toolPath))
        {
            return true;
        }

        return toolPath == preferredPath || toolPath.StartsWith($"{preferredPath}/", StringComparison.Ordinal);
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

    void PrintActionStart(string details)
    {
        currentActionLabel = FormatToolLabel(details);
        currentActionVisible = ShouldRenderAction(details);

        if (currentActionVisible)
        {
            Console.WriteLine($"→ {currentActionLabel}");
        }
    }

    void PrintActionEnd(string details)
    {
        if (!currentActionVisible)
        {
            currentActionLabel = string.Empty;
            return;
        }

        var label = string.IsNullOrWhiteSpace(currentActionLabel) ? FormatToolLabel(details) : currentActionLabel;
        Console.WriteLine($"✓ {label}");

        var summary = string.IsNullOrWhiteSpace(details) ? string.Empty : details;
        if (!string.IsNullOrWhiteSpace(summary) && summary != label)
        {
            Console.WriteLine($"  {summary}");
        }

        currentActionLabel = string.Empty;
        currentActionVisible = false;
    }

    Console.WriteLine("Rufus Agent");
    Console.WriteLine("───────────");
    Console.WriteLine("Task");
    Console.WriteLine(task);
    Console.WriteLine("Mode: headless, read-only");
    Console.WriteLine("Scope: repository root");
    Console.WriteLine();
    Console.WriteLine("Actions");
    Console.WriteLine("───────────");

    foreach (var (isError, text) in capturedLines)
    {
        var trimmedText = text.Trim();

        if (trimmedText.StartsWith("[agent:start]", StringComparison.Ordinal))
        {
            continue;
        }

        if (trimmedText.StartsWith("[tool:start]", StringComparison.Ordinal))
        {
            PrintActionStart(trimmedText["[tool:start]".Length..].Trim());
            continue;
        }

        if (trimmedText.StartsWith("[tool:end]", StringComparison.Ordinal))
        {
            PrintActionEnd(trimmedText["[tool:end]".Length..].Trim());
            continue;
        }

        if (trimmedText.StartsWith("[assistant]", StringComparison.Ordinal))
        {
            AppendAnswer(trimmedText["[assistant]".Length..].TrimStart());
            continue;
        }

        if (trimmedText.StartsWith("[agent:end]", StringComparison.Ordinal))
        {
            continue;
        }

        if (isError)
        {
            AppendAnswer(trimmedText);
            continue;
        }

        AppendAnswer(trimmedText);
    }

    Console.WriteLine();
    Console.WriteLine("Answer");
    Console.WriteLine("────────────────────────────────────────────");

    if (answerLines.Count == 0)
    {
        Console.WriteLine("(no assistant output)");
    }
    else
    {
        foreach (var line in answerLines)
        {
            Console.WriteLine(line);
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

static string InferPreferredPath(string task)
{
    var match = Regex.Match(task, "\\b(?:inspect|list|read|open|show)\\s+([A-Za-z0-9_.\\/-]+)\\b", RegexOptions.IgnoreCase);
    return match.Success ? match.Groups[1].Value : string.Empty;
}
