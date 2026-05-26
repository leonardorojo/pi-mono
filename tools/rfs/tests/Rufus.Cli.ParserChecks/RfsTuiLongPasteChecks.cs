using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Rufus.Cli.ParserChecks;

internal static class RfsTuiLongPasteChecks
{
    public static async Task RunAsync(List<string> failures)
    {
        Console.WriteLine("LongPasteChecks");

        await RunShortPromptCaseAsync(failures);
        await RunManualPasteCaptureCaseAsync(failures);
        await RunManualPasteCancelCaseAsync(failures);
        await RunPasteBurstDuringModeSelectionCaseAsync(failures);
        await RunRedirectedMultilineBurstCaseAsync(failures);
        await RunRedirectedLongLineCaseAsync(failures);
    }

    private static async Task RunShortPromptCaseAsync(List<string> failures)
    {
        const string name = "long paste prompt short normal still selects mode";
        var tempRoot = CreateTempRoot("rfs-long-paste-short-prompt-checks");
        try
        {
            if (!await InitializeRepoAsync(tempRoot, failures, name))
            {
                return;
            }

            var result = await RunScriptedTuiAsync("hola\n/exit\n", tempRoot);
            if (result.ExitCode != 0)
            {
                failures.Add($"[{name}] expected exit code 0 but got {result.ExitCode}. stderr: {result.Stderr}");
                return;
            }

            ExpectContains(result.Stdout, name, failures,
                "¿Cómo querés procesarlo?",
                "Direct",
                "Elegí 1-4, o /cancel:");
        }
        finally
        {
            TryDeleteDirectory(tempRoot);
        }
    }

    private static async Task RunManualPasteCaptureCaseAsync(List<string> failures)
    {
        const string name = "long paste /paste captures multiline prompt and stores temp paste file";
        var tempRoot = CreateTempRoot("rfs-long-paste-manual-checks");
        try
        {
            if (!await InitializeRepoAsync(tempRoot, failures, name))
            {
                return;
            }

            var result = await RunScriptedTuiAsync("/paste\npaste line 1\npaste line 2\n/end\n1\n/exit\n", tempRoot);
            if (result.ExitCode != 0)
            {
                failures.Add($"[{name}] expected exit code 0 but got {result.ExitCode}. stderr: {result.Stderr}");
                return;
            }

            ExpectContains(result.Stdout, name, failures,
                "Paste multiline prompt. Finish with /end. Use /cancel to discard.",
                "Captured long paste:",
                "[paste:",
                "lines:",
                "chars:",
                "estimated tokens:",
                "¿Cómo querés procesarlo?");

            if (result.Stdout.Contains("Invalid mode. Choose 1, 2, 3, 4, /cancel, or /exit.", StringComparison.Ordinal))
            {
                failures.Add($"[{name}] expected no Invalid mode spam after /paste capture.");
            }

            var pasteDirectory = Path.Combine(tempRoot, ".rfs", "tmp", "pastes");
            if (!Directory.Exists(pasteDirectory))
            {
                failures.Add($"[{name}] expected temp paste directory '{pasteDirectory}' to exist.");
            }
            else
            {
                var files = Directory.GetFiles(pasteDirectory, "*_paste.md", SearchOption.TopDirectoryOnly);
                if (files.Length != 1)
                {
                    failures.Add($"[{name}] expected exactly one temp paste file but found {files.Length}.");
                }
                else
                {
                    var content = await File.ReadAllTextAsync(files[0]);
                    if (!content.Contains("paste line 1", StringComparison.Ordinal) || !content.Contains("paste line 2", StringComparison.Ordinal))
                    {
                        failures.Add($"[{name}] expected paste file to contain captured lines.");
                    }
                }
            }
        }
        finally
        {
            TryDeleteDirectory(tempRoot);
        }
    }

    private static async Task RunManualPasteCancelCaseAsync(List<string> failures)
    {
        const string name = "long paste /paste cancel returns to prompt";
        var tempRoot = CreateTempRoot("rfs-long-paste-cancel-checks");
        try
        {
            if (!await InitializeRepoAsync(tempRoot, failures, name))
            {
                return;
            }

            var result = await RunScriptedTuiAsync("/paste\npaste line 1\n/cancel\n/exit\n", tempRoot);
            if (result.ExitCode != 0)
            {
                failures.Add($"[{name}] expected exit code 0 but got {result.ExitCode}. stderr: {result.Stderr}");
                return;
            }

            ExpectContains(result.Stdout, name, failures,
                "Paste multiline prompt. Finish with /end. Use /cancel to discard.",
                "Paste discarded.");

            if (result.Stdout.Contains("Captured long paste:", StringComparison.Ordinal))
            {
                failures.Add($"[{name}] expected cancel to discard the captured paste.");
            }
        }
        finally
        {
            TryDeleteDirectory(tempRoot);
        }
    }

    private static async Task RunPasteBurstDuringModeSelectionCaseAsync(List<string> failures)
    {
        const string name = "long paste burst during mode selection shows one warning and recovers";
        var tempRoot = CreateTempRoot("rfs-long-paste-burst-checks");
        try
        {
            if (!await InitializeRepoAsync(tempRoot, failures, name))
            {
                return;
            }

            var result = await RunScriptedTuiAsync("Implement reset board action\nfirst paste line\nsecond paste line\n/cancel\n/exit\n", tempRoot);
            if (result.ExitCode != 0)
            {
                failures.Add($"[{name}] expected exit code 0 but got {result.ExitCode}. stderr: {result.Stderr}");
                return;
            }

            ExpectContains(result.Stdout, name, failures,
                "Multiline input detected while choosing processing mode.",
                "Use /cancel and then /paste for long text.",
                "Prompt cancelled.");

            if (result.Stdout.Contains("Invalid mode. Choose 1, 2, 3, 4, /cancel, or /exit.", StringComparison.Ordinal))
            {
                failures.Add($"[{name}] expected paste burst recovery to avoid Invalid mode spam.");
            }
        }
        finally
        {
            TryDeleteDirectory(tempRoot);
        }
    }

    private static async Task RunRedirectedMultilineBurstCaseAsync(List<string> failures)
    {
        const string name = "long paste redirected multiline input is grouped";
        var tempRoot = CreateTempRoot("rfs-long-paste-redirected-checks");
        try
        {
            if (!await InitializeRepoAsync(tempRoot, failures, name))
            {
                return;
            }

            var result = await RunRedirectedTuiAsync("Implement reset board action\nfirst redirected line\nsecond redirected line\n/cancel\n/exit\n", tempRoot);
            if (result.ExitCode != 0)
            {
                failures.Add($"[{name}] expected exit code 0 but got {result.ExitCode}. stderr: {result.Stderr}");
                return;
            }

            if (!result.Stdout.Contains("¿Cómo querés procesarlo?", StringComparison.Ordinal))
            {
                failures.Add($"[{name}] expected mode menu to appear for redirected multiline burst.");
            }
            if (result.Stdout.Contains("Invalid mode. Choose 1, 2, 3, 4, /cancel, or /exit.", StringComparison.Ordinal))
            {
                failures.Add($"[{name}] expected redirected multiline burst to avoid Invalid mode spam.");
            }
        }
        finally
        {
            TryDeleteDirectory(tempRoot);
        }
    }

    private static async Task RunRedirectedLongLineCaseAsync(List<string> failures)
    {
        const string name = "long paste redirected long line stays recoverable";
        var tempRoot = CreateTempRoot("rfs-long-paste-longline-checks");
        try
        {
            if (!await InitializeRepoAsync(tempRoot, failures, name))
            {
                return;
            }

            var longLine = new string('a', 1500);
            var result = await RunRedirectedTuiAsync($"{longLine}\n/exit\n", tempRoot);
            if (result.ExitCode != 0)
            {
                failures.Add($"[{name}] expected exit code 0 but got {result.ExitCode}. stderr: {result.Stderr}");
                return;
            }

            if (!result.Stdout.Contains("¿Cómo querés procesarlo?", StringComparison.Ordinal))
            {
                failures.Add($"[{name}] expected mode menu to appear for long redirected input.");
            }
            if (result.Stdout.Contains("Invalid mode. Choose 1, 2, 3, 4, /cancel, or /exit.", StringComparison.Ordinal))
            {
                failures.Add($"[{name}] expected long redirected line to avoid Invalid mode spam.");
            }
        }
        finally
        {
            TryDeleteDirectory(tempRoot);
        }
    }

    private static void ExpectContains(string text, string name, List<string> failures, params string[] fragments)
    {
        foreach (var fragment in fragments)
        {
            if (!text.Contains(fragment, StringComparison.Ordinal))
            {
                failures.Add($"[{name}] expected stdout to contain '{fragment}' but it was missing.");
            }
        }
    }

    private static async Task<bool> InitializeRepoAsync(string tempRoot, List<string> failures, string name)
    {
        var gitInit = await RunProcessAsync(tempRoot, "git", "init");
        if (gitInit.ExitCode != 0)
        {
            failures.Add($"[{name}] failed to initialize a temporary git repo: {gitInit.Stderr}");
            return false;
        }

        var repoRoot = GetRepoRoot();
        var cliProjectPath = Path.Combine(repoRoot, "tools", "rfs", "src", "Rufus.Cli", "Rufus.Cli.csproj");
        var initResult = await RunProcessAsync(tempRoot, "dotnet", "run", "--project", cliProjectPath, "--", "init");
        if (initResult.ExitCode != 0)
        {
            failures.Add($"[{name}] expected rfs init to succeed but got exit code {initResult.ExitCode}. stderr: {initResult.Stderr}");
            return false;
        }

        return true;
    }

    private static async Task<(int ExitCode, string Stdout, string Stderr)> RunScriptedTuiAsync(string input, string? workingDirectory = null)
    {
        var repoRoot = GetRepoRoot();
        var cliProjectPath = Path.Combine(repoRoot, "tools", "rfs", "src", "Rufus.Cli", "Rufus.Cli.csproj");
        var cwd = workingDirectory ?? Path.GetTempPath();
        var command = $"cd {ShellQuote(cwd)} && dotnet run --project {ShellQuote(cliProjectPath)} --";
        return await RunProcessAsyncWithInput(cwd, input, "script", "-qec", command, "/dev/null");
    }

    private static async Task<(int ExitCode, string Stdout, string Stderr)> RunRedirectedTuiAsync(string input, string workingDirectory)
    {
        var repoRoot = GetRepoRoot();
        var cliProjectPath = Path.Combine(repoRoot, "tools", "rfs", "src", "Rufus.Cli", "Rufus.Cli.csproj");
        return await RunProcessAsyncWithInput(workingDirectory, input, "dotnet", "run", "--project", cliProjectPath, "--");
    }

    private static async Task<(int ExitCode, string Stdout, string Stderr)> RunProcessAsync(string workingDirectory, params string[] commandLine)
        => await RunProcessAsyncWithInput(workingDirectory, null, commandLine);

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

    private static string GetRepoRoot()
        => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "..", ".."));

    private static string ShellQuote(string value)
        => "'" + value.Replace("'", "'\"'\"'") + "'";

    private static string CreateTempRoot(string prefix)
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), prefix, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        return tempRoot;
    }

    private static void TryDeleteDirectory(string path)
    {
        try { Directory.Delete(path, recursive: true); } catch { }
    }
}
