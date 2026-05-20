using Rufus.RCK.Core.Model;

namespace Rufus.RCK.Workspace;

public sealed record RckWorkspaceInitResult
{
    public bool Success { get; }

    public string? ErrorMessage { get; }

    public string? RepoRoot { get; }

    public RckWorkspacePaths? Paths { get; }

    public bool ConfigCreated { get; }

    public bool RckDirectoriesCreated { get; }

    public bool HeadCreated { get; }

    public bool StateCreated { get; }

    public bool AnchorCreated { get; }

    public RckStateId? StateId { get; }

    public RckAnchorId? AnchorId { get; }

    private RckWorkspaceInitResult(
        bool success,
        string? errorMessage,
        string? repoRoot,
        RckWorkspacePaths? paths,
        bool configCreated,
        bool rckDirectoriesCreated,
        bool headCreated,
        bool stateCreated,
        bool anchorCreated,
        RckStateId? stateId,
        RckAnchorId? anchorId)
    {
        Success = success;
        ErrorMessage = errorMessage;
        RepoRoot = repoRoot;
        Paths = paths;
        ConfigCreated = configCreated;
        RckDirectoriesCreated = rckDirectoriesCreated;
        HeadCreated = headCreated;
        StateCreated = stateCreated;
        AnchorCreated = anchorCreated;
        StateId = stateId;
        AnchorId = anchorId;
    }

    public static RckWorkspaceInitResult Failure(string errorMessage)
    {
        return new RckWorkspaceInitResult(
            success: false,
            errorMessage: errorMessage,
            repoRoot: null,
            paths: null,
            configCreated: false,
            rckDirectoriesCreated: false,
            headCreated: false,
            stateCreated: false,
            anchorCreated: false,
            stateId: null,
            anchorId: null);
    }

    public static RckWorkspaceInitResult SuccessResult(
        string repoRoot,
        RckWorkspacePaths paths,
        bool configCreated,
        bool rckDirectoriesCreated,
        bool headCreated,
        bool stateCreated,
        bool anchorCreated,
        RckStateId stateId,
        RckAnchorId anchorId)
    {
        return new RckWorkspaceInitResult(
            success: true,
            errorMessage: null,
            repoRoot: repoRoot,
            paths: paths,
            configCreated: configCreated,
            rckDirectoriesCreated: rckDirectoriesCreated,
            headCreated: headCreated,
            stateCreated: stateCreated,
            anchorCreated: anchorCreated,
            stateId: stateId,
            anchorId: anchorId);
    }

    public IEnumerable<string> FormatConsoleLines()
    {
        if (!Success)
        {
            if (!string.IsNullOrWhiteSpace(ErrorMessage))
            {
                yield return ErrorMessage;
            }

            yield break;
        }

        yield return $"rfs init: workspace {RepoRoot}";
        yield return ConfigCreated
            ? $"config: created {Paths!.ConfigPath}"
            : $"config: skipped {Paths!.ConfigPath}";
        yield return RckDirectoriesCreated
            ? $"rck directories: created {Paths!.RckDirectory}"
            : $"rck directories: skipped {Paths!.RckDirectory}";
        yield return HeadCreated
            ? $"head: created {Paths!.HeadPath} -> {StateId}"
            : $"head: skipped {Paths!.HeadPath} -> {StateId}";
        yield return StateCreated
            ? $"genesis state: created {Paths!.StatesDirectory}{Path.DirectorySeparatorChar}{StateId}.json"
            : $"genesis state: skipped {Paths!.StatesDirectory}{Path.DirectorySeparatorChar}{StateId}.json";
        if (StateCreated && StateId is not null)
        {
            yield return $"  state id: {StateId}";
        }
        yield return AnchorCreated
            ? $"genesis anchor: created {Paths!.AnchorsDirectory}{Path.DirectorySeparatorChar}{AnchorId}.json"
            : $"genesis anchor: skipped {Paths!.AnchorsDirectory}{Path.DirectorySeparatorChar}{AnchorId}.json";
        if (AnchorCreated && AnchorId is not null)
        {
            yield return $"  anchor id: {AnchorId}";
        }
    }
}
