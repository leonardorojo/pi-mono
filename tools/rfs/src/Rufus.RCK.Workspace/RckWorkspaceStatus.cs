namespace Rufus.RCK.Workspace;

public sealed record RckWorkspaceStatus(
    string RepoRoot,
    bool WorkspaceExists,
    bool ConfigExists,
    bool RckExists,
    bool HeadExists,
    string? Head,
    int StateCount,
    int DeltaCount,
    int AnchorCount,
    GitWorkspaceContext GitContext)
{
    public bool Initialized => WorkspaceExists;

    public IEnumerable<string> FormatConsoleLines()
    {
        yield return "rfs status";
        yield return string.Empty;
        yield return "Workspace:";
        yield return $"  initialized: {(Initialized ? "yes" : "no")}";

        if (!Initialized)
        {
            yield return string.Empty;
            yield return "Hint:";
            yield return "  run `rfs init`";
            yield break;
        }

        yield return $"  root: {RepoRoot}";
        yield return $"  config: {(ConfigExists ? ".rfs/config.json" : "missing")}";
        yield return string.Empty;
        yield return "RCK:";
        yield return $"  initialized: {(RckExists ? "yes" : "no")}";
        yield return $"  head: {(HeadExists ? (string.IsNullOrWhiteSpace(Head) ? "<invalid>" : Head) : "<missing>")}";
        yield return $"  states: {StateCount}";
        yield return $"  deltas: {DeltaCount}";
        yield return $"  anchors: {AnchorCount}";
        yield return string.Empty;
        yield return "Git:";
        yield return $"  branch: {GitContext.Branch ?? "(detached)"}";
        yield return $"  commit: {GitContext.Commit ?? "<unknown>"}";
        yield return $"  dirty: {GitContext.Dirty.ToString().ToLowerInvariant()}";
    }
}
