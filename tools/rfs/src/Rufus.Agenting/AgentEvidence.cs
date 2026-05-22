namespace Rufus.Agenting;

public sealed record AgentEvidence
{
    public string Kind { get; }

    public string Source { get; }

    public string? Detail { get; }

    public AgentEvidence(string kind, string source, string? detail = null)
    {
        Kind = Normalize(kind, nameof(kind));
        Source = Normalize(source, nameof(source));
        Detail = NormalizeOptional(detail, nameof(detail));
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
