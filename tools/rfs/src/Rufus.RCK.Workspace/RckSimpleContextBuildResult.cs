namespace Rufus.RCK.Workspace;

public sealed record RckSimpleContextBuildResult(
    RckSimpleContext Context,
    string PromptToSend,
    IReadOnlyList<string> Omissions,
    IReadOnlyList<string> Warnings);
