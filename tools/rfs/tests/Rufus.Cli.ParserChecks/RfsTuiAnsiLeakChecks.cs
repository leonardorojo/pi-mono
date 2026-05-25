using System.Diagnostics;
using Rufus.Cli.Tui;

internal static class RfsTuiAnsiLeakChecks
{
    internal static async Task Run(List<string> failures)
    {
        RunPlainRendererCase(failures);
        await RunCapturedTuiCase(failures);
    }

    private static void RunPlainRendererCase(List<string> failures)
    {
        var originalOut = Console.Out;
        var originalNoColor = Environment.GetEnvironmentVariable("NO_COLOR");
        using var stdout = new StringWriter();

        try
        {
            Environment.SetEnvironmentVariable("NO_COLOR", "1");
            Console.SetOut(stdout);
            RfsTuiRenderer.WriteResponse("# Título\n\n- item\n\nTexto con \\rho y a^2");
        }
        finally
        {
            Console.SetOut(originalOut);
            Environment.SetEnvironmentVariable("NO_COLOR", originalNoColor);
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

    private static Task<(int ExitCode, string Stdout, string Stderr)> RunProcessAsync(string workingDirectory, params string[] commandLine)
        => RunProcessAsyncWithInput(workingDirectory, null, commandLine);

    private static async Task<(int ExitCode, string Stdout, string Stderr)> RunProcessAsyncWithInput(string workingDirectory, string? standardInput, params string[] commandLine)
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
