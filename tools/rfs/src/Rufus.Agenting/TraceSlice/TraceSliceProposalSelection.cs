namespace Rufus.Agenting.TraceSlice;

public sealed record TraceSliceProposalSelection(
    IReadOnlyList<string> StateIds,
    IReadOnlyList<string> DeltaIds,
    IReadOnlyList<string> AnchorIds,
    IReadOnlyList<string> ArtifactRefs);
