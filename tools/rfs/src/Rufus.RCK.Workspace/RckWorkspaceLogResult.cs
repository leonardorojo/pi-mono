namespace Rufus.RCK.Workspace;

public sealed record RckWorkspaceLogResult
{
    public bool Success { get; }

    public string? ErrorMessage { get; }

    public string? RepoRoot { get; }

    public bool WorkspaceInitialized { get; }

    public bool RckDirectoryExists { get; }

    public bool RckInitialized { get; }

    public bool HeadExists { get; }

    public bool HeadResolved { get; }

    public string? HeadStateId { get; }

    public IReadOnlyList<RckWorkspaceLogEntry> Entries { get; }

    private RckWorkspaceLogResult(
        bool success,
        string? errorMessage,
        string? repoRoot,
        bool workspaceInitialized,
        bool rckDirectoryExists,
        bool rckInitialized,
        bool headExists,
        bool headResolved,
        string? headStateId,
        IReadOnlyList<RckWorkspaceLogEntry> entries)
    {
        Success = success;
        ErrorMessage = errorMessage;
        RepoRoot = repoRoot;
        WorkspaceInitialized = workspaceInitialized;
        RckDirectoryExists = rckDirectoryExists;
        RckInitialized = rckInitialized;
        HeadExists = headExists;
        HeadResolved = headResolved;
        HeadStateId = headStateId;
        Entries = entries;
    }

    public static RckWorkspaceLogResult Failure(string errorMessage)
        => new(false, errorMessage, null, false, false, false, false, false, null, Array.Empty<RckWorkspaceLogEntry>());

    public static RckWorkspaceLogResult Create(
        string repoRoot,
        bool workspaceInitialized,
        bool rckDirectoryExists,
        bool rckInitialized,
        bool headExists,
        bool headResolved,
        string? headStateId,
        IReadOnlyList<RckWorkspaceLogEntry> entries)
        => new(true, null, repoRoot, workspaceInitialized, rckDirectoryExists, rckInitialized, headExists, headResolved, headStateId, entries);

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

        yield return "rfs log";
        yield return string.Empty;

        if (!WorkspaceInitialized)
        {
            yield return "Workspace:";
            yield return "  initialized: no";
            yield return string.Empty;
            yield return "Hint:";
            yield return "  run `rfs init`";
            yield break;
        }

        if (!RckInitialized)
        {
            yield return "RCK:";
            yield return "  initialized: incomplete";
            yield return $"  HEAD: {(HeadExists ? (string.IsNullOrWhiteSpace(HeadStateId) ? "<invalid>" : HeadStateId) : "missing")}";
            yield return string.Empty;
            yield return "Hint:";
            yield return "  run `rfs init`";
            yield break;
        }

        yield return $"HEAD: {ToShortId(HeadStateId ?? string.Empty)}";
        yield return string.Empty;

        if (Entries.Count == 0)
        {
            yield return "(no active RCK states reachable from HEAD)";
            yield break;
        }

        for (var index = 0; index < Entries.Count; index++)
        {
            var entry = Entries[index];
            yield return $"{index + 1}. {entry.StateShortId}  {entry.Mode}  {FormatUtc(entry.CreatedAtUtc)}";

            if (entry.Prompt is not null)
            {
                yield return $"   prompt: {FormatExcerpt(entry.Prompt)}";
            }

            if (entry.AnswerSummary is not null)
            {
                yield return $"   answer: {FormatExcerpt(entry.AnswerSummary)}";
            }

            if (entry.GitCommit is not null)
            {
                yield return $"   git: {ToShortId(entry.GitCommit)} dirty={entry.GitDirty.ToString().ToLowerInvariant()}";
            }

            if (entry.Artifacts.Count > 0)
            {
                yield return "   artifacts:";
                foreach (var artifact in entry.Artifacts)
                {
                    yield return $"     - {artifact.ChangeType} {artifact.Path}";
                }
            }

            if (entry.DeltaShortId is not null)
            {
                yield return $"   delta: {entry.DeltaShortId}";
            }

            if (entry.Anchors.Count > 0)
            {
                foreach (var anchor in entry.Anchors)
                {
                    yield return anchor.Label is null
                        ? $"   anchor: {anchor.ShortId}"
                        : $"   anchor: {anchor.Label} {anchor.ShortId}";
                }
            }

            yield return string.Empty;
        }
    }

    private static string ToShortId(string value)
        => value.Length <= 8 ? value : value[..8];

    private static string FormatUtc(DateTimeOffset value)
        => value.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ");

    private static string FormatExcerpt(string value, int maxLength = 120)
    {
        var normalized = string.Join(" ", value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        if (normalized.Length <= maxLength)
        {
            return normalized;
        }

        return normalized[..Math.Max(0, maxLength - 1)] + "…";
    }
}
