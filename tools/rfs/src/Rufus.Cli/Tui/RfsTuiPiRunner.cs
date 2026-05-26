using Rufus.Cli.PiIntegration;

namespace Rufus.Cli.Tui;

internal interface IRfsTuiPiRunner
{
    Task<RfsTuiPiRunResult> RunAsync(
        string workingDirectory,
        string prompt,
        string? workspaceModel = null,
        Action<PiJsonStreamEvent>? eventReporter = null,
        CancellationToken cancellationToken = default);
}

internal sealed class RealPiRunner : IRfsTuiPiRunner
{
    public async Task<RfsTuiPiRunResult> RunAsync(
        string workingDirectory,
        string prompt,
        string? workspaceModel = null,
        Action<PiJsonStreamEvent>? eventReporter = null,
        CancellationToken cancellationToken = default)
    {
        var detailed = await PiJsonEventRunner.RunAgentDetailedAsync(
            workingDirectory,
            prompt,
            workspaceModel,
            eventReporter,
            cancellationToken).ConfigureAwait(false);

        return new RfsTuiPiRunResult(
            Stdout: detailed.Answer,
            Stderr: detailed.StdErr,
            ExitCode: detailed.ExitCode,
            StartedAt: detailed.StartedAt,
            FinishedAt: detailed.FinishedAt,
            DurationMs: detailed.DurationMs,
            TimedOut: detailed.TimedOut,
            Cancelled: detailed.Cancelled,
            FailedToStart: detailed.FailedToStart,
            WorkingDirectory: detailed.WorkingDirectory,
            PromptBytes: detailed.PromptBytes,
            Provider: detailed.Provider,
            Model: detailed.Model,
            Health: DetermineHealth(detailed));
    }

    private static RfsTuiPiRunHealth DetermineHealth(PiJsonAgentDetailedResult detailed)
    {
        if (detailed.FailedToStart)
        {
            return RfsTuiPiRunHealth.FailedToStart;
        }

        if (detailed.Cancelled)
        {
            return RfsTuiPiRunHealth.Cancelled;
        }

        if (detailed.TimedOut)
        {
            return RfsTuiPiRunHealth.TimedOut;
        }

        return detailed.Success
            ? RfsTuiPiRunHealth.Completed
            : RfsTuiPiRunHealth.ExitedWithError;
    }
}
