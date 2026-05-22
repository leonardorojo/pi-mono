namespace Rufus.Agenting.Intent;

public sealed record PromptIntent
{
    public string Intent { get; }

    public string Summary { get; }

    public IReadOnlyList<string> Entities { get; }

    public IReadOnlyList<string> Constraints { get; }

    public PromptIntent(string intent, string summary, IEnumerable<string> entities, IEnumerable<string> constraints)
    {
        Intent = Normalize(intent, nameof(intent));
        Summary = Normalize(summary, nameof(summary));
        Entities = NormalizeItems(entities, nameof(entities));
        Constraints = NormalizeItems(constraints, nameof(constraints));
    }

    private static string Normalize(string value, string paramName)
    {
        return string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException($"{paramName} cannot be empty.", paramName)
            : value;
    }

    private static IReadOnlyList<string> NormalizeItems(IEnumerable<string> items, string paramName)
    {
        ArgumentNullException.ThrowIfNull(items);

        return items
            .Select(item => Normalize(item, paramName))
            .ToArray();
    }
}
