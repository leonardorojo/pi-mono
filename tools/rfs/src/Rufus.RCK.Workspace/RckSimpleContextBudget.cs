namespace Rufus.RCK.Workspace;

public sealed record RckSimpleContextBudget(
    int TargetChars,
    int MaxChars,
    int HardMaxChars,
    int EstimatedChars,
    int EstimatedTokens,
    bool Truncated);
