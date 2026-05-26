namespace Rufus.Cli.Tui;

internal enum RfsTuiPiRunHealth
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

internal sealed record RfsTuiPiRunResult(
    string Stdout,
    string Stderr,
    int? ExitCode,
    DateTimeOffset StartedAt,
    DateTimeOffset FinishedAt,
    long DurationMs,
    bool TimedOut,
    bool Cancelled,
    bool FailedToStart,
    string WorkingDirectory,
    int PromptBytes,
    string? Provider,
    string? Model,
    RfsTuiPiRunHealth Health = RfsTuiPiRunHealth.Completed);
