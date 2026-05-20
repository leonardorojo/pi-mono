namespace Rufus.RCK.Workspace;

public sealed record RckInteractionTool(string Name, string Status)
{
    public static RckInteractionTool Completed(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return new RckInteractionTool(name, "completed");
    }
}
