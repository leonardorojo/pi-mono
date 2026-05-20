using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Rufus.RCK.Core.Model;

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
    return InitializeWorkspace();
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

static void PrintHelp()
{
    Console.WriteLine("rfs - Rufus CLI proof of concept");
    Console.WriteLine("Usage:");
    Console.WriteLine("  rfs --version");
    Console.WriteLine("  rfs help");
    Console.WriteLine("  rfs init   = bootstrap .rfs + RCK genesis state/anchor");
    Console.WriteLine("  rfs pi [message]");
    Console.WriteLine("  rfs ask <prompt>");
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

static int InitializeWorkspace()
{
    var repoRoot = FindRepoRoot();
    if (repoRoot is null)
    {
        Console.Error.WriteLine("rfs init: repository root not found.");
        return 1;
    }

    var workspaceDirectory = Path.Combine(repoRoot, ".rfs");
    var rckDirectory = Path.Combine(workspaceDirectory, "rck");
    var statesDirectory = Path.Combine(rckDirectory, "states");
    var deltasDirectory = Path.Combine(rckDirectory, "deltas");
    var anchorsDirectory = Path.Combine(rckDirectory, "anchors");

    Directory.CreateDirectory(workspaceDirectory);
    Directory.CreateDirectory(rckDirectory);
    Directory.CreateDirectory(statesDirectory);
    Directory.CreateDirectory(deltasDirectory);
    Directory.CreateDirectory(anchorsDirectory);

    var configPath = Path.Combine(workspaceDirectory, "config.json");
    if (!File.Exists(configPath))
    {
        const string configContent = "{\n  \"schemaVersion\": 1,\n  \"type\": \"rufus.workspace\",\n  \"createdBy\": \"rfs init\"\n}\n";
        File.WriteAllText(configPath, configContent, new UTF8Encoding(false));
        Console.WriteLine($"Initialized {configPath}");
    }
    else
    {
        Console.WriteLine($"{configPath} already exists");
    }

    var gitInfo = CaptureGitInfo(repoRoot);
    var workspaceName = Path.GetFileName(Path.TrimEndingDirectorySeparator(repoRoot));
    var statePayload = BuildInitialStatePayload(repoRoot, workspaceName, gitInfo);
    var state = RckState.Create(
        statePayload,
        meta: new RckStateMeta(DateTimeOffset.UtcNow, "rfs init", "genesis", "initial rfs workspace state"));

    var statePath = Path.Combine(statesDirectory, $"{state.Id}.json");
    if (!File.Exists(statePath))
    {
        File.WriteAllText(statePath, SerializeStateEnvelope(state), new UTF8Encoding(false));
        Console.WriteLine($"Initialized {statePath}");
        Console.WriteLine($"  state id: {state.Id}");
    }
    else
    {
        Console.WriteLine($"{statePath} already exists");
    }

    var anchor = RckAnchor.Create(
        state.Id,
        meta: new RckAnchorMeta(DateTimeOffset.UtcNow, "rfs init", "genesis", "initial rfs workspace anchor"));

    var anchorPath = Path.Combine(anchorsDirectory, $"{anchor.Id}.json");
    if (!File.Exists(anchorPath))
    {
        File.WriteAllText(anchorPath, SerializeAnchorEnvelope(anchor), new UTF8Encoding(false));
        Console.WriteLine($"Initialized {anchorPath}");
        Console.WriteLine($"  anchor id: {anchor.Id}");
    }
    else
    {
        Console.WriteLine($"{anchorPath} already exists");
    }

    return 0;
}

static (string? Branch, string? Commit, bool Dirty) CaptureGitInfo(string repoRoot)
{
    var branch = RunGit(repoRoot, "rev-parse", "--abbrev-ref", "HEAD");
    if (string.Equals(branch, "HEAD", StringComparison.Ordinal))
    {
        branch = null;
    }

    var commit = RunGit(repoRoot, "rev-parse", "HEAD");
    var dirtyStatus = RunGit(repoRoot, "status", "--porcelain") ?? string.Empty;
    var dirty = !string.IsNullOrWhiteSpace(dirtyStatus);

    return (branch, commit, dirty);
}

static string BuildInitialStatePayload(string repoRoot, string workspaceName, (string? Branch, string? Commit, bool Dirty) gitInfo)
{
    var payload = new
    {
        type = "rufus.initial-state",
        schemaVersion = 1,
        workspace = new
        {
            type = "rufus.workspace",
            root = repoRoot,
            name = workspaceName,
        },
        git = new
        {
            branch = gitInfo.Branch,
            commit = gitInfo.Commit,
            dirty = gitInfo.Dirty,
        },
        rfs = new
        {
            initializedBy = "rfs init",
        },
    };

    return JsonSerializer.Serialize(payload);
}

static string SerializeStateEnvelope(RckState state)
{
    var envelope = new
    {
        schemaVersion = 1,
        type = "rufus.rck.state",
        id = state.Id.ToString(),
        payloadCanonicalJson = state.PayloadCanonicalJson,
        refs = state.Refs.Select(SerializeRckRef).ToArray(),
        meta = new
        {
            createdAtUtc = state.Meta.CreatedAtUtc,
            state.Meta.CreatedBy,
            state.Meta.Label,
            state.Meta.Reason,
        },
    };

    return JsonSerializer.Serialize(envelope, new JsonSerializerOptions { WriteIndented = true });
}

static string SerializeAnchorEnvelope(RckAnchor anchor)
{
    var envelope = new
    {
        schemaVersion = 1,
        type = "rufus.rck.anchor",
        id = anchor.Id.ToString(),
        stateId = anchor.StateId.ToString(),
        parentAnchorIds = anchor.ParentAnchorIds.Select(parent => parent.ToString()).ToArray(),
        meta = new
        {
            createdAtUtc = anchor.Meta.CreatedAtUtc,
            anchor.Meta.CreatedBy,
            anchor.Meta.Label,
            anchor.Meta.Reason,
        },
    };

    return JsonSerializer.Serialize(envelope, new JsonSerializerOptions { WriteIndented = true });
}

static object SerializeRckRef(RckRef rckRef)
{
    return new
    {
        rckRef.Id,
        rckRef.Kind,
        uri = rckRef.Uri.ToString(),
        hash = rckRef.Hash?.Value,
        mediaType = rckRef.MediaType,
        meta = rckRef.Meta is null
            ? null
            : new
            {
                createdAtUtc = rckRef.Meta.CreatedAtUtc,
                rckRef.Meta.CreatedBy,
                rckRef.Meta.Label,
                rckRef.Meta.Reason,
            },
    };
}

static string? RunGit(string workingDirectory, params string[] arguments)
{
    var psi = new ProcessStartInfo
    {
        FileName = "git",
        UseShellExecute = false,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        WorkingDirectory = workingDirectory,
    };

    foreach (var argument in arguments)
    {
        psi.ArgumentList.Add(argument);
    }

    using var process = Process.Start(psi);
    if (process is null)
    {
        return null;
    }

    var output = process.StandardOutput.ReadToEnd();
    _ = process.StandardError.ReadToEnd();
    process.WaitForExit();

    if (process.ExitCode != 0)
    {
        return null;
    }

    return output.Trim();
}

static string? FindRepoRoot()
{
    var current = new DirectoryInfo(Directory.GetCurrentDirectory());

    while (current is not null)
    {
        var gitEntry = Path.Combine(current.FullName, ".git");
        if (Directory.Exists(gitEntry) || File.Exists(gitEntry))
        {
            return current.FullName;
        }

        current = current.Parent;
    }

    return null;
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
