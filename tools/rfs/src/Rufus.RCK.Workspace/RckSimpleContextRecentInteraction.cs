namespace Rufus.RCK.Workspace;

public sealed record RckSimpleContextRecentInteraction(
    string StateId,
    string StateShortId,
    string? DeltaId,
    string? DeltaShortId,
    string Mode,
    RckSimpleContextPrompt Prompt,
    string AnswerSummary,
    DateTimeOffset? CreatedAtUtc,
    string? GitCommit,
    IReadOnlyList<RckSimpleContextArtifactRef> ArtifactRefs);
