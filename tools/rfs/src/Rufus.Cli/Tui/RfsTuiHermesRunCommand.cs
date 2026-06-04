using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Rufus.RCK.Workspace;

namespace Rufus.Cli.Tui;

internal static class RfsTuiHermesRunCommand
{
    private static readonly IRfsTuiHermesRunner Runner = new RealHermesRunner();

    internal static Task<bool> ExecuteAsync(RckWorkspaceStatus status, RfsTuiSessionState sessionState)
        => ExecuteAsync(status, sessionState, Runner, CancellationToken.None);

    internal static Task<bool> ExecuteAsync(RckWorkspaceStatus status, RfsTuiSessionState sessionState, CancellationToken cancellationToken)
        => ExecuteAsync(status, sessionState, Runner, cancellationToken);

    internal static Task<bool> ExecuteAsync(RckWorkspaceStatus status, RfsTuiSessionState sessionState, IRfsTuiHermesRunner runner)
        => ExecuteAsync(status, sessionState, runner, CancellationToken.None);

    internal static async Task<bool> ExecuteAsync(RckWorkspaceStatus status, RfsTuiSessionState sessionState, IRfsTuiHermesRunner runner, CancellationToken cancellationToken)
    {
        var draftResult = RfsTuiHermesPromptBuilder.TryBuild(status, sessionState);
        if (!draftResult.Success || draftResult.Draft is null)
        {
            RfsTuiRenderer.WriteHermesHandoffUnavailable("No hay una respuesta previa para ejecutar handoff con Hermes.");
            return true;
        }

        var prompt = draftResult.Draft.PromptText;
        var promptBytes = Encoding.UTF8.GetByteCount(prompt);
        if (promptBytes > RfsTuiHermesRunOptions.DefaultMaxPromptBytes)
        {
            RfsTuiRenderer.WriteWarningLine("Hermes prompt too large for argv transport. Use /hermes draft to copy the draft manually, or implement future stdin/file transport.");
            return true;
        }

        string gitStatusBefore;
        try
        {
            gitStatusBefore = await runner.CaptureGitStatusAsync(status.RepoRoot, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Failed to capture git status before Hermes run: {ex.Message}");
            return true;
        }

        if (!string.IsNullOrWhiteSpace(gitStatusBefore))
        {
            RfsTuiRenderer.WriteWarningLine("Warning: repository is already dirty before Hermes run. Proceeding with guarded execution. Changes after run may be harder to attribute.");
        }

        RfsTuiRenderer.WriteModeBanner("Hermes run", "Executing Hermes CLI one-shot transport...");
        var result = await runner.RunAsync(
            status.RepoRoot,
            prompt,
            gitStatusBefore,
            new RfsTuiHermesRunOptions
            {
                MaxPromptBytes = RfsTuiHermesRunOptions.DefaultMaxPromptBytes,
                Timeout = RfsTuiHermesRunOptions.DefaultTimeout,
            },
            RfsTuiRenderer.WriteHermesRunHeartbeat,
            cancellationToken).ConfigureAwait(false);

        RfsTuiRenderer.WriteHermesRunResult(result);
        if (!ShouldAutoRecord(result))
        {
            RfsTuiRenderer.WriteWarningLine("Hermes run did not produce a recordable final answer; State + Delta not recorded.");
            return true;
        }

        var recordResult = RckInteractionRecorder.RecordTui(
            new RckTuiInteractionRecordInput(
                prompt,
                result.Stdout.TrimEnd(),
                sessionState.CurrentSessionModelProvider,
                sessionState.CurrentSessionModel,
                mode: "tui-hermes-run"),
            status.RepoRoot);

        if (!recordResult.Success)
        {
            if (!string.IsNullOrWhiteSpace(recordResult.ErrorMessage))
            {
                Console.Error.WriteLine(recordResult.ErrorMessage);
            }

            return true;
        }

        Console.WriteLine("Recorded Hermes run State + Delta:");
        Console.WriteLine($"  state: {recordResult.StateId}");
        Console.WriteLine($"  delta: {recordResult.DeltaId}");
        return true;
    }

    private static bool ShouldAutoRecord(RfsTuiHermesRunResult result)
        => result.Health == RfsTuiHermesRunHealth.Completed && !string.IsNullOrWhiteSpace(result.Stdout);
}
