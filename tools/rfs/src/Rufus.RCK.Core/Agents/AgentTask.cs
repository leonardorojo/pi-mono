namespace Rufus.RCK.Core.Agents;

public sealed record AgentTask
{
    public string Id { get; }

    public string Kind { get; }

    public string Goal { get; }

    public string? Input { get; }

    public string? ExpectedOutput { get; }

    public AgentTask(string id, string kind, string goal, string? input = null, string? expectedOutput = null)
    {
        Id = Normalize(id, nameof(id));
        Kind = Normalize(kind, nameof(kind));
        Goal = Normalize(goal, nameof(goal));
        Input = NormalizeOptional(input, nameof(input));
        ExpectedOutput = NormalizeOptional(expectedOutput, nameof(expectedOutput));
    }

    private static string Normalize(string value, string paramName)
    {
        return string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException($"{paramName} cannot be empty.", paramName)
            : value;
    }

    private static string? NormalizeOptional(string? value, string paramName)
    {
        if (value is null)
        {
            return null;
        }

        return string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException($"{paramName} cannot be empty.", paramName)
            : value;
    }
}
