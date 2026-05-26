using System;
using System.Threading;
using System.Threading.Tasks;

namespace Rufus.Cli.Tui;

internal enum RfsTuiHermesRunHealth
{
    Starting,
    Running,
    LongRunning,
    TimedOut,
    Cancelled,
    FailedToStart,
    ExitedWithError,
    Completed,
}

internal sealed record RfsTuiHermesRunOptions
{
    internal const int DefaultMaxPromptBytes = 24_000;
    internal static readonly TimeSpan DefaultTimeout = TimeSpan.FromMilliseconds(300_000);
    internal static readonly TimeSpan DefaultHeartbeatInterval = TimeSpan.FromSeconds(10);

    public TimeSpan Timeout { get; init; } = DefaultTimeout;

    public int MaxPromptBytes { get; init; } = DefaultMaxPromptBytes;

    public TimeSpan HeartbeatInterval { get; init; } = DefaultHeartbeatInterval;
}

internal sealed record RfsTuiHermesRunProgress(
    string WorkingDirectory,
    TimeSpan Elapsed,
    TimeSpan Remaining,
    TimeSpan Timeout,
    int PromptBytes,
    string Transport,
    int? ProcessId,
    RfsTuiHermesRunHealth Health = RfsTuiHermesRunHealth.Running);

internal delegate void RfsTuiHermesRunProgressReporter(RfsTuiHermesRunProgress progress);

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
    int PromptBytes,
    RfsTuiHermesRunHealth Health = RfsTuiHermesRunHealth.Completed);

internal interface IRfsTuiHermesRunner
{
    Task<string> CaptureGitStatusAsync(string workingDirectory, CancellationToken cancellationToken = default);

    Task<RfsTuiHermesRunResult> RunAsync(
        string workingDirectory,
        string prompt,
        string? gitStatusBefore = null,
        RfsTuiHermesRunOptions? options = null,
        RfsTuiHermesRunProgressReporter? progressReporter = null,
        CancellationToken cancellationToken = default);
}
