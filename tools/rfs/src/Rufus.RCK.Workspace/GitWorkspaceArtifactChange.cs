namespace Rufus.RCK.Workspace;

public sealed record GitWorkspaceArtifactChange(
    string Kind,
    string Path,
    string ChangeType,
    string GitStatus,
    string Source);
