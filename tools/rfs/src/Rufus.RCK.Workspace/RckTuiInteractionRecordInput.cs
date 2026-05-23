namespace Rufus.RCK.Workspace;

public sealed record RckTuiInteractionRecordInput
{
    public string Prompt { get; }

    public string Answer { get; }

    public string? Provider { get; }

    public string? Model { get; }

    public RckTuiInteractionRecordInput(string prompt, string answer, string? provider = null, string? model = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(prompt);

        Prompt = prompt;
        Answer = answer ?? string.Empty;
        Provider = provider;
        Model = model;
    }
}
