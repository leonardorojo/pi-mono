namespace Rufus.Agenting;

public sealed record AgentExecutionModel
{
    public string Provider { get; }

    public string Model { get; }

    public AgentExecutionModel(string provider, string model)
    {
        Provider = Normalize(provider, nameof(provider));
        Model = Normalize(model, nameof(model));
    }

    private static string Normalize(string value, string paramName)
    {
        return string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException($"{paramName} cannot be empty.", paramName)
            : value;
    }
}
