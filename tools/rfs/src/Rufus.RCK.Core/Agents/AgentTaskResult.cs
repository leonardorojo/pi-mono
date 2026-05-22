namespace Rufus.RCK.Core.Agents;

public sealed record AgentTaskResult
{
    public string TaskId { get; }

    public AgentTaskStatus Status { get; }

    public string AgentId { get; }

    public AgentExecutionModel ExecutionModel { get; }

    public string? Output { get; }

    public string? Summary { get; }

    public IReadOnlyList<AgentEvidence> Evidence { get; }

    public IReadOnlyList<string> Warnings { get; }

    public IReadOnlyList<string> Errors { get; }

    public AgentTaskResult(
        string taskId,
        AgentTaskStatus status,
        string agentId,
        AgentExecutionModel executionModel,
        string? output = null,
        string? summary = null,
        IEnumerable<AgentEvidence>? evidence = null,
        IEnumerable<string>? warnings = null,
        IEnumerable<string>? errors = null)
    {
        TaskId = Normalize(taskId, nameof(taskId));
        Status = status;
        AgentId = Normalize(agentId, nameof(agentId));
        ExecutionModel = executionModel ?? throw new ArgumentNullException(nameof(executionModel));
        Output = NormalizeOptional(output, nameof(output));
        Summary = NormalizeOptional(summary, nameof(summary));
        Evidence = (evidence ?? Array.Empty<AgentEvidence>()).ToArray();
        Warnings = NormalizeStrings(warnings);
        Errors = NormalizeStrings(errors);

        if (Status == AgentTaskStatus.Failed && Errors.Count == 0)
        {
            throw new ArgumentException("Errors cannot be empty when status is Failed.", nameof(errors));
        }
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

    private static IReadOnlyList<string> NormalizeStrings(IEnumerable<string>? values)
    {
        if (values is null)
        {
            return Array.Empty<string>();
        }

        return values.Select((value, index) =>
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException($"Value at index {index} cannot be empty.");
            }

            return value;
        }).ToArray();
    }
}
