namespace Rufus.RCK.Workspace;

public sealed record RckSimpleContextArtifactRef(
    string Path,
    string Status,
    string? Kind,
    long? SizeBytes);
