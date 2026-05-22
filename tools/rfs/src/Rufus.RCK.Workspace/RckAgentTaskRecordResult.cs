namespace Rufus.RCK.Workspace;

public sealed record RckAgentTaskRecordResult
{
    public RckAgentTaskRecordResult(
        bool success,
        string? errorMessage,
        string? repoRoot,
        RckWorkspacePaths? paths,
        string? headStateId,
        string? stateId,
        string? deltaId,
        bool stateCreated,
        bool deltaCreated,
        bool headUpdated)
    {
        Success = success;
        ErrorMessage = errorMessage;
        RepoRoot = repoRoot;
        Paths = paths;
        HeadStateId = headStateId;
        StateId = stateId;
        DeltaId = deltaId;
        StateCreated = stateCreated;
        DeltaCreated = deltaCreated;
        HeadUpdated = headUpdated;
    }

    public bool Success { get; }

    public string? ErrorMessage { get; }

    public string? RepoRoot { get; }

    public RckWorkspacePaths? Paths { get; }

    public string? HeadStateId { get; }

    public string? StateId { get; }

    public string? DeltaId { get; }

    public bool StateCreated { get; }

    public bool DeltaCreated { get; }

    public bool HeadUpdated { get; }

    public static RckAgentTaskRecordResult Failure(string errorMessage) =>
        new(false, errorMessage, null, null, null, null, null, false, false, false);

    public static RckAgentTaskRecordResult SuccessResult(
        string repoRoot,
        RckWorkspacePaths paths,
        string headStateId,
        string stateId,
        string deltaId,
        bool stateCreated,
        bool deltaCreated,
        bool headUpdated) =>
        new(true, null, repoRoot, paths, headStateId, stateId, deltaId, stateCreated, deltaCreated, headUpdated);

    public IEnumerable<string> FormatConsoleLines()
    {
        if (!Success)
        {
            yield return $"[rck] record failed: {ErrorMessage}";
            yield break;
        }

        if (StateCreated)
        {
            yield return $"[rck] state written: {StateId}";
        }

        if (DeltaCreated)
        {
            yield return $"[rck] delta written: {DeltaId}";
        }

        if (HeadUpdated)
        {
            yield return $"[rck] head updated: {HeadStateId} -> {StateId}";
        }
    }
}
