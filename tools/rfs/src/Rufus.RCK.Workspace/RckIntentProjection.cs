namespace Rufus.RCK.Workspace;

public sealed record RckIntentProjection(
    string Intent,
    IReadOnlyList<string> Entities,
    IReadOnlyList<string> Constraints);
