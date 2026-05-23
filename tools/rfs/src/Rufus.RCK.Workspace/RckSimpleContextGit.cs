namespace Rufus.RCK.Workspace;

public sealed record RckSimpleContextGit(
    string? Branch,
    string? Commit,
    bool Dirty,
    int ChangedArtifactsCount,
    IReadOnlyList<RckSimpleContextArtifactRef> ChangedArtifacts);
