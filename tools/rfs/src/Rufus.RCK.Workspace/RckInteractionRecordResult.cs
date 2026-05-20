using Rufus.RCK.Core.Model;

namespace Rufus.RCK.Workspace;

public sealed record RckInteractionRecordResult
{
    public bool Success { get; }

    public string? ErrorMessage { get; }

    public string? RepoRoot { get; }

    public RckWorkspacePaths? Paths { get; }

    public bool StateCreated { get; }

    public bool DeltaCreated { get; }

    public bool HeadUpdated { get; }

    public bool AnchorCreated { get; }

    public string? AnchorLabel { get; }

    public RckStateId? StateId { get; }

    public RckDeltaId? DeltaId { get; }

    public RckAnchorId? AnchorId { get; }

    private RckInteractionRecordResult(
        bool success,
        string? errorMessage,
        string? repoRoot,
        RckWorkspacePaths? paths,
        bool stateCreated,
        bool deltaCreated,
        bool headUpdated,
        bool anchorCreated,
        string? anchorLabel,
        RckStateId? stateId,
        RckDeltaId? deltaId,
        RckAnchorId? anchorId)
    {
        Success = success;
        ErrorMessage = errorMessage;
        RepoRoot = repoRoot;
        Paths = paths;
        StateCreated = stateCreated;
        DeltaCreated = deltaCreated;
        HeadUpdated = headUpdated;
        AnchorCreated = anchorCreated;
        AnchorLabel = anchorLabel;
        StateId = stateId;
        DeltaId = deltaId;
        AnchorId = anchorId;
    }

    public static RckInteractionRecordResult Failure(string errorMessage)
    {
        return new RckInteractionRecordResult(
            success: false,
            errorMessage: errorMessage,
            repoRoot: null,
            paths: null,
            stateCreated: false,
            deltaCreated: false,
            headUpdated: false,
            anchorCreated: false,
            anchorLabel: null,
            stateId: null,
            deltaId: null,
            anchorId: null);
    }

    public static RckInteractionRecordResult SuccessResult(
        string repoRoot,
        RckWorkspacePaths paths,
        bool stateCreated,
        bool deltaCreated,
        bool headUpdated,
        bool anchorCreated,
        string? anchorLabel,
        RckStateId stateId,
        RckDeltaId deltaId,
        RckAnchorId? anchorId)
    {
        return new RckInteractionRecordResult(
            success: true,
            errorMessage: null,
            repoRoot: repoRoot,
            paths: paths,
            stateCreated: stateCreated,
            deltaCreated: deltaCreated,
            headUpdated: headUpdated,
            anchorCreated: anchorCreated,
            anchorLabel: anchorLabel,
            stateId: stateId,
            deltaId: deltaId,
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

        yield return StateCreated
            ? $"[rck] state: created {StateId}"
            : $"[rck] state: existing {StateId}";
        yield return DeltaCreated
            ? $"[rck] delta: created {DeltaId}"
            : $"[rck] delta: existing {DeltaId}";
        yield return $"[rck] head: {StateId}";
        if (AnchorCreated && AnchorLabel is not null && AnchorId is not null)
        {
            yield return $"[rck] anchor: {AnchorLabel} {AnchorId}";
        }
    }
}
