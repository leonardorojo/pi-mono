using Rufus.Cli.Tui;
using Rufus.RCK.Workspace;

namespace Rufus.Cli.TraceSlice;

public sealed record TraceSliceAnchorSelectionAgentInput(
    string UserPrompt,
    RckTraceSliceProposalIntentProjection Intent,
    RckDagQuickIndexV1 DagQuickIndex,
    IReadOnlyList<string> PolicyHints);
