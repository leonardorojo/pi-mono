namespace Rufus.RCK.Workspace;

public sealed record RckWorkspacePaths(string RepoRoot)
{
    public string WorkspaceDirectory => Path.Combine(RepoRoot, ".rfs");

    public string ConfigPath => Path.Combine(WorkspaceDirectory, "config.json");

    public string RckDirectory => Path.Combine(WorkspaceDirectory, "rck");

    public string HeadPath => Path.Combine(RckDirectory, "HEAD");

    public string StatesDirectory => Path.Combine(RckDirectory, "states");

    public string DeltasDirectory => Path.Combine(RckDirectory, "deltas");

    public string AnchorsDirectory => Path.Combine(RckDirectory, "anchors");
}
