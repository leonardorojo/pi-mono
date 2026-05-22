namespace Rufus.RCK.Core.Agents;

public interface IAgent
{
    string Id { get; }

    AgentDescriptor Descriptor { get; }

    Task<AgentTaskResult> ExecuteAsync(AgentTask task, CancellationToken cancellationToken = default);
}
