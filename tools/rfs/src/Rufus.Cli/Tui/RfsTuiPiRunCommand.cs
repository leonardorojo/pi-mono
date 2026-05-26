using Rufus.Cli.PiIntegration;
using Rufus.RCK.Workspace;

namespace Rufus.Cli.Tui;

internal static class RfsTuiPiRunCommand
{
    private static readonly IRfsTuiPiRunner Runner = new RealPiRunner();

    internal static Task<bool> ExecuteAsync(RckWorkspaceStatus status, RfsTuiSessionState sessionState)
        => ExecuteAsync(status, sessionState, Runner, CancellationToken.None);

    internal static Task<bool> ExecuteAsync(RckWorkspaceStatus status, RfsTuiSessionState sessionState, CancellationToken cancellationToken)
        => ExecuteAsync(status, sessionState, Runner, cancellationToken);

    internal static Task<bool> ExecuteAsync(RckWorkspaceStatus status, RfsTuiSessionState sessionState, IRfsTuiPiRunner runner)
        => ExecuteAsync(status, sessionState, runner, CancellationToken.None);

    internal static async Task<bool> ExecuteAsync(
        RckWorkspaceStatus status,
        RfsTuiSessionState sessionState,
        IRfsTuiPiRunner runner,
        CancellationToken cancellationToken)
    {
        var draftResult = RfsTuiPiPromptBuilder.TryBuild(status, sessionState);
        if (!draftResult.Success || draftResult.Draft is null)
        {
            RfsTuiRenderer.WritePiRunUnavailable(draftResult.ErrorMessage ?? "No hay una interacción previa para construir el prompt de Pi.");
            return true;
        }

        var draft = draftResult.Draft;
        var workspaceModel = sessionState.ResolveMainModel();

        RfsTuiRenderer.WriteModeBanner("Pi run", "Executing Pi JSON event stream...");
        RfsTuiRenderer.WritePiRunPromptSummary(draft, workspaceModel);
        RfsTuiRenderer.WritePiRunStatusLine("starting");
        RfsTuiRenderer.WritePiRunStatusLine("running");

        var result = await runner.RunAsync(
            status.RepoRoot,
            draft.PromptText,
            workspaceModel,
            eventReporter: streamEvent => RfsTuiRenderer.WritePiRunRuntimeEvent(streamEvent),
            cancellationToken).ConfigureAwait(false);

        RfsTuiRenderer.WritePiRunResult(result);
        return true;
    }
}
