using Rufus.Cli.Tui;
using Rufus.RCK.Workspace;

namespace Rufus.Cli.TraceSlice;

public sealed record TraceSliceAnchorSelectionAgentInput(
    string UserPrompt,
    RckTraceSliceProposalIntentProjection Intent,
    RckTraceSliceProposalDagQuickIndex DagQuickIndex,
    IReadOnlyList<string> PolicyHints);
