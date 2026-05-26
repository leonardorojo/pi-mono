using System;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Rufus.Cli.Tui;

internal sealed class RealHermesRunner : IRfsTuiHermesRunner
{
    public async Task<string> CaptureGitStatusAsync(string workingDirectory, CancellationToken cancellationToken = default)
    {
        var result = await RunGitAsync(workingDirectory, cancellationToken).ConfigureAwait(false);
        if (result.ExitCode != 0)
        {
            var stderr = string.IsNullOrWhiteSpace(result.Stderr) ? "git status --short failed." : result.Stderr.TrimEnd();
            throw new InvalidOperationException(stderr);
        }

        return TrimTrailingNewLines(result.Stdout);
    }

    public async Task<RfsTuiHermesRunResult> RunAsync(
        string workingDirectory,
        string prompt,
        string? gitStatusBefore = null,
        RfsTuiHermesRunOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(workingDirectory))
        {
            throw new ArgumentException("workingDirectory cannot be empty.", nameof(workingDirectory));
        }

        if (prompt is null)
        {
            throw new ArgumentNullException(nameof(prompt));
        }

        options ??= new RfsTuiHermesRunOptions();
        var resolvedGitStatusBefore = gitStatusBefore ?? await CaptureGitStatusAsync(workingDirectory, cancellationToken).ConfigureAwait(false);
        var promptBytes = Encoding.UTF8.GetByteCount(prompt);
        var startedAt = DateTimeOffset.UtcNow;
        var stopwatch = Stopwatch.StartNew();
        var finishedAt = startedAt;
        string stdout = string.Empty;
        string stderr = string.Empty;
        int? exitCode = null;
        var timedOut = false;
        var workingDirectoryPath = Path.GetFullPath(workingDirectory);
        var gitStatusAfter = resolvedGitStatusBefore;

        try
        {
            using var process = new Process
            {
                StartInfo = CreateHermesStartInfo(workingDirectoryPath, prompt),
            };

            if (!process.Start())
            {
                stderr = "Failed to start hermes process.";
            }
            else
            {
                var stdoutTask = process.StandardOutput.ReadToEndAsync();
                var stderrTask = process.StandardError.ReadToEndAsync();
                var exitedTask = process.WaitForExitAsync(cancellationToken);
                var timeoutTask = Task.Delay(options.Timeout, cancellationToken);
                var completedTask = await Task.WhenAny(exitedTask, timeoutTask).ConfigureAwait(false);

                timedOut = completedTask == timeoutTask && !cancellationToken.IsCancellationRequested;
                if (timedOut)
                {
                    TryKill(process);
                    await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
                }
                else
                {
                    await exitedTask.ConfigureAwait(false);
                }

                stdout = await stdoutTask.ConfigureAwait(false);
                stderr = await stderrTask.ConfigureAwait(false);
                exitCode = process.HasExited ? process.ExitCode : null;
            }
        }
        catch (Exception ex)
        {
            stderr = string.IsNullOrWhiteSpace(stderr)
                ? $"Failed to run hermes process: {ex.Message}"
                : $"{stderr.TrimEnd()}\n{ex.Message}";
        }
        finally
        {
            stopwatch.Stop();
            finishedAt = DateTimeOffset.UtcNow;
        }

        try
        {
            gitStatusAfter = await CaptureGitStatusAsync(workingDirectory, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            stderr = string.IsNullOrWhiteSpace(stderr)
                ? $"{stderr}{ex.Message}".Trim()
                : $"{stderr.TrimEnd()}\n{ex.Message}";
        }

        return new RfsTuiHermesRunResult(
            Stdout: stdout,
            Stderr: stderr,
            ExitCode: exitCode,
            StartedAt: startedAt,
            FinishedAt: finishedAt,
            DurationMs: stopwatch.ElapsedMilliseconds,
            TimedOut: timedOut,
            WorkingDirectory: workingDirectoryPath,
            GitStatusBefore: resolvedGitStatusBefore,
            GitStatusAfter: gitStatusAfter,
            DirtyStateChanged: !string.Equals(NormalizeStatus(resolvedGitStatusBefore), NormalizeStatus(gitStatusAfter), StringComparison.Ordinal),
            PromptBytes: promptBytes);
    }

    private static ProcessStartInfo CreateHermesStartInfo(string workingDirectory, string prompt)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "hermes",
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };

        startInfo.ArgumentList.Add("-z");
        startInfo.ArgumentList.Add(prompt);
        return startInfo;
    }

    private static async Task<(string Stdout, string Stderr, int ExitCode)> RunGitAsync(string workingDirectory, CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "git",
            WorkingDirectory = Path.GetFullPath(workingDirectory),
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };

        startInfo.ArgumentList.Add("status");
        startInfo.ArgumentList.Add("--short");

        using var process = new Process { StartInfo = startInfo };
        if (!process.Start())
        {
            return (string.Empty, "Failed to start git process.", -1);
        }

        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        return (await stdoutTask.ConfigureAwait(false), await stderrTask.ConfigureAwait(false), process.ExitCode);
    }

    private static string NormalizeStatus(string? status)
        => string.IsNullOrWhiteSpace(status) ? string.Empty : TrimTrailingNewLines(status);

    private static string TrimTrailingNewLines(string value)
        => value.TrimEnd('\r', '\n');

    private static void TryKill(Process process)
    {
        try
        {
            process.Kill(entireProcessTree: true);
        }
        catch
        {
        }
    }
}
