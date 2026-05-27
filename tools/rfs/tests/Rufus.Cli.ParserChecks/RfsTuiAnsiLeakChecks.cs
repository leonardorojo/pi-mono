using System.Diagnostics;
using Rufus.Cli.PiIntegration;
using Rufus.Cli.Tui;
using Rufus.RCK.Workspace;

internal static class RfsTuiAnsiLeakChecks
{
    internal static async Task Run(List<string> failures)
    {
        RunPlainTerminalOverrideCase(failures);
        RunNoColorCase(failures);
        RunDumbTerminalCase(failures);
        await RunCapturedTuiCase(failures);
    }

    private static void RunPlainTerminalOverrideCase(List<string> failures)
    {
        var originalOut = Console.Out;
        var originalError = Console.Error;
        var originalPlain = Environment.GetEnvironmentVariable("RFS_TUI_PLAIN");
        var originalNoColor = Environment.GetEnvironmentVariable("NO_COLOR");
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();

        try
        {
            Environment.SetEnvironmentVariable("RFS_TUI_PLAIN", "1");
            Environment.SetEnvironmentVariable("NO_COLOR", null);
            Console.SetOut(stdout);
            Console.SetError(stderr);

            if (RfsTuiTerminal.IsInteractive)
            {
                failures.Add("[tui ansi] expected RFS_TUI_PLAIN=1 to disable interactive mode.");
            }

            if (RfsTuiTerminal.UseColor || RfsTuiTerminal.UseCursorControl || RfsTuiTerminal.UseLivePalette || RfsTuiTerminal.UseAnsiSgr || RfsTuiTerminal.UseAnsiStyle)
            {
                failures.Add("[tui ansi] expected RFS_TUI_PLAIN=1 to disable all ANSI/cursor capabilities.");
            }

            RfsTuiRenderer.WritePrompt();
            RfsTuiRenderer.WriteResponse("# Título\n\n- item\n\nTexto con \\rho y a^2");
            RfsTuiRenderer.WriteHelp(new[]
            {
                new RfsTuiCommandInfo(RfsTuiCommandKind.Help, "/help", "/help", "Show this help"),
            });

            var status = new RckWorkspaceStatus(
                "/repo",
                WorkspaceExists: true,
                ConfigExists: true,
                RckExists: true,
                HeadExists: true,
                Head: "abc12345",
                StateCount: 1,
                DeltaCount: 2,
                AnchorCount: 3,
                GitContext: new GitWorkspaceContext("main", "abc12345", false, Array.Empty<GitWorkspaceArtifactChange>()));
            RfsTuiRenderer.WriteStatus(status, new RckWorkspaceModelConfigReadResult(true, null, "/repo", "/repo/.rfs/config.json", true, "gpt-5.4-mini"), new RfsTuiSessionState());

            var models = new[]
            {
                new PiRpcAvailableModel("gpt-5.4-mini", "openai", "GPT-5.4 Mini"),
            };
            var selectionState = new RfsTuiModelSelectionState(models, "gpt-5.4-mini");
            RfsTuiRenderer.WriteModelPickerScreen(models, selectionState, "gpt-5.4-mini");

            var output = stdout.ToString() + stderr.ToString();
            AssertPlainOutput(failures, output, "plain override");
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalError);
            Environment.SetEnvironmentVariable("RFS_TUI_PLAIN", originalPlain);
            Environment.SetEnvironmentVariable("NO_COLOR", originalNoColor);
        }
    }

    private static void RunNoColorCase(List<string> failures)
    {
        var originalOut = Console.Out;
        var originalNoColor = Environment.GetEnvironmentVariable("NO_COLOR");
        var originalPlain = Environment.GetEnvironmentVariable("RFS_TUI_PLAIN");
        using var stdout = new StringWriter();

        try
        {
            Environment.SetEnvironmentVariable("RFS_TUI_PLAIN", null);
            Environment.SetEnvironmentVariable("NO_COLOR", "1");
            Console.SetOut(stdout);
            RfsTuiRenderer.WriteResponse("# Título\n\n- item\n\nTexto con \\rho y a^2");
        }
        finally
        {
            Console.SetOut(originalOut);
            Environment.SetEnvironmentVariable("NO_COLOR", originalNoColor);
            Environment.SetEnvironmentVariable("RFS_TUI_PLAIN", originalPlain);
        }

        var output = stdout.ToString();
        if (output.Contains("\u001b", StringComparison.Ordinal))
        {
            failures.Add("[tui ansi] expected plain renderer output with NO_COLOR set to contain no ANSI escape sequences.");
        }

        if (!output.Contains("Título", StringComparison.Ordinal) || !output.Contains("• item", StringComparison.Ordinal) || !output.Contains("ρ", StringComparison.Ordinal))
        {
            failures.Add("[tui ansi] expected plain renderer output to remain readable without ANSI.");
        }
    }

    private static void RunDumbTerminalCase(List<string> failures)
    {
        var originalTerm = Environment.GetEnvironmentVariable("TERM");
        var originalPlain = Environment.GetEnvironmentVariable("RFS_TUI_PLAIN");
        var originalNoColor = Environment.GetEnvironmentVariable("NO_COLOR");

        try
        {
            Environment.SetEnvironmentVariable("RFS_TUI_PLAIN", null);
            Environment.SetEnvironmentVariable("NO_COLOR", null);
            Environment.SetEnvironmentVariable("TERM", "dumb");

            if (RfsTuiTerminal.IsInteractive)
            {
                failures.Add("[tui ansi] expected TERM=dumb to disable interactive mode.");
            }

            if (RfsTuiTerminal.UseCursorControl || RfsTuiTerminal.UseLivePalette || RfsTuiTerminal.UseColor || RfsTuiTerminal.UseAnsiSgr || RfsTuiTerminal.UseAnsiStyle)
            {
                failures.Add("[tui ansi] expected TERM=dumb to disable all TUI chrome.");
            }
        }
        finally
        {
            Environment.SetEnvironmentVariable("TERM", originalTerm);
            Environment.SetEnvironmentVariable("RFS_TUI_PLAIN", originalPlain);
            Environment.SetEnvironmentVariable("NO_COLOR", originalNoColor);
        }
    }

    private static async Task RunCapturedTuiCase(List<string> failures)
    {
        var toolsRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        var cliProjectPath = Path.Combine(toolsRoot, "src", "Rufus.Cli", "Rufus.Cli.csproj");
        var tempRoot = Path.Combine(Path.GetTempPath(), "rfs-tui-ansi-leak-checks", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var initResult = await RunProcessAsync(tempRoot, "git", "init");
            if (initResult.ExitCode != 0)
            {
                failures.Add($"[tui ansi] failed to initialize temporary git repo: {initResult.Stderr}");
                return;
            }

            var runResult = await RunProcessAsyncWithInput(
                tempRoot,
                "/help\n/exit\n",
                new Dictionary<string, string?>
                {
                    ["RFS_TUI_PLAIN"] = "1",
                    ["NO_COLOR"] = "1",
                },
                "dotnet",
                "run",
                "--project",
                cliProjectPath,
                "--");

            if (runResult.ExitCode != 0)
            {
                failures.Add($"[tui ansi] expected captured TUI run to exit cleanly but got {runResult.ExitCode}. stderr: {runResult.Stderr}");
                return;
            }

            var combinedOutput = runResult.Stdout + runResult.Stderr;
            if (combinedOutput.Contains("\u001b", StringComparison.Ordinal) || combinedOutput.Contains("[1F", StringComparison.Ordinal) || combinedOutput.Contains("[2K", StringComparison.Ordinal))
            {
                failures.Add("[tui ansi] expected captured/non-interactive TUI output to contain no ANSI cursor or color sequences.");
            }

            if (!combinedOutput.Contains("Model:", StringComparison.Ordinal) || !combinedOutput.Contains("/exit", StringComparison.Ordinal))
            {
                failures.Add("[tui ansi] expected captured TUI run to remain readable and include the help surface.");
            }
        }
        finally
        {
            try
            {
                Directory.Delete(tempRoot, recursive: true);
            }
            catch
            {
            }
        }
    }

    private static void AssertPlainOutput(List<string> failures, string output, string scenario)
    {
        if (output.Contains("\u001b", StringComparison.Ordinal))
        {
            failures.Add($"[tui ansi] {scenario}: expected no ANSI escape sequences.");
        }

        if (output.Contains("[1m", StringComparison.Ordinal) ||
            output.Contains("[0m", StringComparison.Ordinal) ||
            output.Contains("[96m", StringComparison.Ordinal) ||
            output.Contains("[90m", StringComparison.Ordinal) ||
            output.Contains("[1F", StringComparison.Ordinal) ||
            output.Contains("[2K", StringComparison.Ordinal))
        {
            failures.Add($"[tui ansi] {scenario}: expected no raw SGR or cursor-control fragments.");
        }
    }

    private static Task<(int ExitCode, string Stdout, string Stderr)> RunProcessAsync(string workingDirectory, params string[] commandLine)
        => RunProcessAsyncWithInput(workingDirectory, null, null, commandLine);

    private static Task<(int ExitCode, string Stdout, string Stderr)> RunProcessAsyncWithInput(
        string workingDirectory,
        string? standardInput,
        params string[] commandLine)
        => RunProcessAsyncWithInput(workingDirectory, standardInput, null, commandLine);

    private static async Task<(int ExitCode, string Stdout, string Stderr)> RunProcessAsyncWithInput(
        string workingDirectory,
        string? standardInput,
        IReadOnlyDictionary<string, string?>? environmentVariables,
        params string[] commandLine)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = commandLine[0],
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = standardInput is not null,
            UseShellExecute = false,
        };

        if (environmentVariables is not null)
        {
            foreach (var pair in environmentVariables)
            {
                startInfo.Environment[pair.Key] = pair.Value;
            }
        }

        for (var i = 1; i < commandLine.Length; i++)
        {
            startInfo.ArgumentList.Add(commandLine[i]);
        }

        using var process = Process.Start(startInfo);
        if (process is null)
        {
            return (-1, string.Empty, "failed to start process");
        }

        Task? stdinTask = null;
        if (standardInput is not null)
        {
            stdinTask = process.StandardInput.WriteAsync(standardInput);
            process.StandardInput.Close();
        }

        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();
        if (stdinTask is not null)
        {
            await stdinTask;
        }

        await process.WaitForExitAsync();
        return (process.ExitCode, await stdoutTask, await stderrTask);
    }
}
