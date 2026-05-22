namespace Rufus.RCK.Core.Agents;

public sealed record AgentDescriptor
{
    public string Id { get; }

    public string Name { get; }

    public string Role { get; }

    public AgentExecutionModel ExecutionModel { get; }

    public IReadOnlyList<string> Capabilities { get; }

    public AgentDescriptor(string id, string name, string role, AgentExecutionModel executionModel, IEnumerable<string> capabilities)
    {
        Id = Normalize(id, nameof(id));
        Name = Normalize(name, nameof(name));
        Role = Normalize(role, nameof(role));
        ExecutionModel = executionModel ?? throw new ArgumentNullException(nameof(executionModel));
        Capabilities = NormalizeCapabilities(capabilities);
    }

    private static string Normalize(string value, string paramName)
    {
        return string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException($"{paramName} cannot be empty.", paramName)
            : value;
    }

    private static IReadOnlyList<string> NormalizeCapabilities(IEnumerable<string> capabilities)
    {
        ArgumentNullException.ThrowIfNull(capabilities);

        var capabilityList = capabilities
            .Select(capability => Normalize(capability, nameof(capabilities)))
            .ToArray();

        return capabilityList.Length == 0
            ? throw new ArgumentException("capabilities cannot be empty.", nameof(capabilities))
            : capabilityList;
    }
}
