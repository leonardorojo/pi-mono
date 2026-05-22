namespace Rufus.RCK.Workspace;

public sealed record RckAgentTaskRecordInput(
    string TaskId,
    string TaskKind,
    string AgentId,
    string Status,
    string TaskSummary,
    string GoalSummary,
    string InputSummary,
    string ExecutionProvider,
    string ExecutionModel,
    string OutputKind,
    string OutputSummary,
    RckIntentProjection OutputData,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<string> Errors);
