namespace Rufus.RCK.Workspace;

public sealed record RckWorkspaceLogAnchor
{
    public string Id { get; }

    public string ShortId { get; }

    public string? Label { get; }

    public DateTimeOffset CreatedAtUtc { get; }

    private RckWorkspaceLogAnchor(string id, string shortId, string? label, DateTimeOffset createdAtUtc)
    {
        Id = id;
        ShortId = shortId;
        Label = label;
        CreatedAtUtc = createdAtUtc;
    }

    public static RckWorkspaceLogAnchor Create(string id, string? label, DateTimeOffset createdAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        return new RckWorkspaceLogAnchor(id, ToShortId(id), NormalizeLabel(label), createdAtUtc);
    }

    private static string? NormalizeLabel(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value;

    private static string ToShortId(string value)
        => value.Length <= 8 ? value : value[..8];
}

public sealed record RckWorkspaceLogEntry
{
    public string StateId { get; }

    public string StateShortId { get; }

    public string? DeltaId { get; }

    public string? DeltaShortId { get; }

    public string Mode { get; }

    public string? Prompt { get; }

    public string? AnswerSummary { get; }

    public string? GitCommit { get; }

    public bool GitDirty { get; }

    public IReadOnlyList<GitWorkspaceArtifactChange> Artifacts { get; }

    public DateTimeOffset CreatedAtUtc { get; }

    public string? CreatedBy { get; }

    public string? Label { get; }

    public string? Reason { get; }

    public IReadOnlyList<RckWorkspaceLogAnchor> Anchors { get; }

    public string? PayloadType { get; }

    private RckWorkspaceLogEntry(
        string stateId,
        string stateShortId,
        string? deltaId,
        string? deltaShortId,
        string mode,
        string? prompt,
        string? answerSummary,
        string? gitCommit,
        bool gitDirty,
        IReadOnlyList<GitWorkspaceArtifactChange> artifacts,
        DateTimeOffset createdAtUtc,
        string? createdBy,
        string? label,
        string? reason,
        IReadOnlyList<RckWorkspaceLogAnchor> anchors,
        string? payloadType)
    {
        StateId = stateId;
        StateShortId = stateShortId;
        DeltaId = deltaId;
        DeltaShortId = deltaShortId;
        Mode = mode;
        Prompt = prompt;
        AnswerSummary = answerSummary;
        GitCommit = gitCommit;
        GitDirty = gitDirty;
        Artifacts = artifacts;
        CreatedAtUtc = createdAtUtc;
        CreatedBy = createdBy;
        Label = label;
        Reason = reason;
        Anchors = anchors;
        PayloadType = payloadType;
    }

    public static RckWorkspaceLogEntry Create(
        string stateId,
        string? deltaId,
        string mode,
        string? prompt,
        string? answerSummary,
        string? gitCommit,
        bool gitDirty,
        IReadOnlyList<GitWorkspaceArtifactChange> artifacts,
        DateTimeOffset createdAtUtc,
        string? createdBy,
        string? label,
        string? reason,
        IReadOnlyList<RckWorkspaceLogAnchor> anchors,
        string? payloadType)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stateId);
        ArgumentException.ThrowIfNullOrWhiteSpace(mode);

        return new RckWorkspaceLogEntry(
            stateId,
            ToShortId(stateId),
            deltaId,
            deltaId is null ? null : ToShortId(deltaId),
            mode,
            NormalizeOptional(prompt),
            NormalizeOptional(answerSummary),
            NormalizeOptional(gitCommit),
            gitDirty,
            artifacts,
            createdAtUtc,
            NormalizeOptional(createdBy),
            NormalizeOptional(label),
            NormalizeOptional(reason),
            anchors,
            NormalizeOptional(payloadType));
    }

    private static string? NormalizeOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value;

    private static string ToShortId(string value)
        => value.Length <= 8 ? value : value[..8];
}
