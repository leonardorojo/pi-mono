namespace Rufus.RCK.Workspace;

public sealed record RckSimpleContextAnchorRef(
    string Id,
    string ShortId,
    string? Label,
    DateTimeOffset CreatedAtUtc,
    string StateId,
    string StateShortId);
