namespace Rufus.Agenting.TraceSlice;

public sealed record TraceSliceProposalMaterializationPolicy(
    bool IncludeStatePayloads,
    bool IncludeDeltaDecodedOps,
    bool IncludeArtifactContents,
    bool IncludeGitDiffs,
    bool IncludeStdoutStderr,
    bool IncludeJsonl);
