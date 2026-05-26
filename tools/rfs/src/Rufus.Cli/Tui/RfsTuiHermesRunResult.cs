using System;
using System.Threading;
using System.Threading.Tasks;

namespace Rufus.Cli.Tui;

internal sealed record RfsTuiHermesRunOptions
{
    internal const int DefaultMaxPromptBytes = 24_000;
    internal static readonly TimeSpan DefaultTimeout = TimeSpan.FromMilliseconds(300_000);

    public TimeSpan Timeout { get; init; } = DefaultTimeout;

    public int MaxPromptBytes { get; init; } = DefaultMaxPromptBytes;
}

internal sealed record RfsTuiHermesRunResult(
    string Stdout,
    string Stderr,
    int? ExitCode,
    DateTimeOffset StartedAt,
    DateTimeOffset FinishedAt,
    long DurationMs,
    bool TimedOut,
    string WorkingDirectory,
    string GitStatusBefore,
    string GitStatusAfter,
    bool DirtyStateChanged,
    int PromptBytes);

internal interface IRfsTuiHermesRunner
{
    Task<string> CaptureGitStatusAsync(string workingDirectory, CancellationToken cancellationToken = default);

    Task<RfsTuiHermesRunResult> RunAsync(
        string workingDirectory,
        string prompt,
        string? gitStatusBefore = null,
        RfsTuiHermesRunOptions? options = null,
        CancellationToken cancellationToken = default);
}
