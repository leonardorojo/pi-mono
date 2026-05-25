using System.Text.Json;
using Rufus.Agenting;
using Rufus.Agenting.Answering;
using Rufus.Cli.PiIntegration;

namespace Rufus.Cli.Answering;

public sealed record PrincipalAnswerLlmResult(
    bool Success,
    string Prompt,
    string Answer,
    string? ErrorMessage,
    string? Provider,
    string? Model,
    string? Transport,
    int? EstimatedTokens = null,
    IReadOnlyList<string>? Warnings = null,
    IReadOnlyList<string>? Errors = null);

public interface IPrincipalAnswerLlmTransport
{
    Task<PrincipalAnswerLlmResult> AskAsync(
        string workingDirectory,
        string prompt,
        string? workspaceModel,
        CancellationToken cancellationToken = default);
}

public sealed class PiJsonPrincipalAnswerLlmTransport : IPrincipalAnswerLlmTransport
{
    private const int StdinPromptThresholdChars = 32_000;

    public async Task<PrincipalAnswerLlmResult> AskAsync(
        string workingDirectory,
        string prompt,
        string? workspaceModel,
        CancellationToken cancellationToken = default)
    {
        var trimmedPrompt = prompt.Trim();
        var transport = trimmedPrompt.Length > StdinPromptThresholdChars ? "stdin" : "argv";
        var askResult = await PiJsonEventRunner.RunAskAsync(workingDirectory, prompt, workspaceModel, cancellationToken).ConfigureAwait(false);

        return new PrincipalAnswerLlmResult(
            askResult.Success,
            askResult.Prompt,
            askResult.Answer,
            askResult.ErrorMessage,
            askResult.Provider,
            askResult.Model,
            transport,
            EstimatedTokens: null,
            Warnings: Array.Empty<string>(),
            Errors: askResult.Success ? Array.Empty<string>() : new[] { askResult.ErrorMessage ?? "Pi JSON ask failed." });
    }
}

public sealed class PiPrincipalAnswerAgent : IAgent
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly string _workingDirectory;
    private readonly IPrincipalAnswerLlmTransport _transport;

    public PiPrincipalAnswerAgent(
        string workingDirectory,
        AgentExecutionModel executionModel,
        IPrincipalAnswerLlmTransport? transport = null)
    {
        _workingDirectory = string.IsNullOrWhiteSpace(workingDirectory)
            ? throw new ArgumentException("workingDirectory cannot be empty.", nameof(workingDirectory))
            : workingDirectory;
        _transport = transport ?? new PiJsonPrincipalAnswerLlmTransport();
        Descriptor = new AgentDescriptor(
            id: "pi-principal-answer",
            name: "Pi Principal Answer Agent",
            role: "Produce the final answer from a validated ContextPack and user prompt.",
            executionModel: executionModel ?? throw new ArgumentNullException(nameof(executionModel)),
            capabilities: new[] { PrincipalAnswerAgentConstants.TaskKind });
    }

    public string Id => Descriptor.Id;

    public AgentDescriptor Descriptor { get; }

    public async Task<AgentTaskResult> ExecuteAsync(AgentTask task, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(task);

        if (!string.Equals(task.Kind, PrincipalAnswerAgentConstants.TaskKind, StringComparison.Ordinal))
        {
            return FailedTask(task, $"Unsupported agent task kind '{task.Kind}'. Expected '{PrincipalAnswerAgentConstants.TaskKind}'.");
        }

        if (string.IsNullOrWhiteSpace(task.Input))
        {
            return FailedTask(task, "Principal answer task input is missing JSON.");
        }

        PrincipalAnswerAgentInput input;
        try
        {
            input = JsonSerializer.Deserialize<PrincipalAnswerAgentInput>(task.Input, JsonOptions)
                ?? throw new JsonException("Principal answer task input deserialized to null.");
        }
        catch (Exception ex) when (ex is JsonException or ArgumentException)
        {
            return FailedTask(task, $"Invalid PrincipalAnswerAgentInput JSON: {ex.Message}");
        }

        if (string.IsNullOrWhiteSpace(input.PromptToSend))
        {
            return FailedTask(task, "PrincipalAnswerAgentInput.PromptToSend is required.");
        }

        var llmResult = await _transport
            .AskAsync(_workingDirectory, input.PromptToSend, Descriptor.ExecutionModel.Model, cancellationToken)
            .ConfigureAwait(false);

        if (!llmResult.Success)
        {
            return FailedTask(
                task,
                llmResult.ErrorMessage ?? "Principal answer transport failed.",
                input,
                llmResult);
        }

        if (string.IsNullOrWhiteSpace(llmResult.Answer))
        {
            return FailedTask(task, "Principal answer transport returned an empty final answer.", input, llmResult);
        }

        var output = new PrincipalAnswerAgentOutput(
            FinalAnswer: llmResult.Answer,
            AnswerSummary: SummarizeAnswer(llmResult.Answer),
            Provider: llmResult.Provider ?? Descriptor.ExecutionModel.Provider,
            Model: llmResult.Model ?? Descriptor.ExecutionModel.Model,
            Transport: llmResult.Transport,
            EstimatedTokens: llmResult.EstimatedTokens,
            Warnings: llmResult.Warnings,
            Errors: llmResult.Errors);

        var evidence = BuildEvidence(input, llmResult);
        return new AgentTaskResult(
            task.Id,
            AgentTaskStatus.Succeeded,
            Id,
            Descriptor.ExecutionModel,
            output: JsonSerializer.Serialize(output, JsonOptions),
            summary: output.AnswerSummary ?? output.FinalAnswer,
            evidence: evidence,
            warnings: llmResult.Warnings,
            errors: Array.Empty<string>());
    }

    private static string SummarizeAnswer(string answer)
    {
        var firstLine = answer
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault();

        if (string.IsNullOrWhiteSpace(firstLine))
        {
            return answer.Length <= 160 ? answer : answer[..160];
        }

        return firstLine.Length <= 160 ? firstLine : firstLine[..160];
    }

    private static IReadOnlyList<AgentEvidence> BuildEvidence(PrincipalAnswerAgentInput input, PrincipalAnswerLlmResult llmResult)
    {
        var evidence = new List<AgentEvidence>
        {
            new("agent", "pi-principal-answer", "Principal answer agent executed the final answer step."),
            new("execution-model", $"{llmResult.Provider ?? "pi"}/{llmResult.Model ?? "(workspace-default)"}", $"provider={llmResult.Provider ?? "pi"}; model={llmResult.Model ?? "(workspace-default)"}"),
            new("prompt", "user-prompt", input.UserPrompt),
            new("prompt-to-send", "validated-main-llm-prompt", input.PromptToSend),
            new("context-summary", input.ContextPackScope, input.ContextSummary),
            new("selection", "states/deltas/anchors", $"{input.SelectedStateIds.Count} / {input.SelectedDeltaIds.Count} / {input.SelectedAnchorIds.Count}"),
        };

        if (!string.IsNullOrWhiteSpace(llmResult.Transport))
        {
            evidence.Add(new AgentEvidence("transport", llmResult.Transport, $"transport={llmResult.Transport}"));
        }

        if (llmResult.EstimatedTokens is not null)
        {
            evidence.Add(new AgentEvidence("estimated-tokens", llmResult.EstimatedTokens.Value.ToString(), $"estimatedTokens={llmResult.EstimatedTokens.Value}"));
        }

        return evidence;
    }

    private AgentTaskResult FailedTask(AgentTask task, string error, PrincipalAnswerAgentInput? input = null, PrincipalAnswerLlmResult? llmResult = null)
    {
        var errors = new List<string> { error };
        if (llmResult?.Errors is { Count: > 0 })
        {
            errors.AddRange(llmResult.Errors);
        }

        var evidence = new List<AgentEvidence>
        {
            new("agent", "pi-principal-answer", "Principal answer agent failed before recording or state mutation."),
            new("execution-model", $"{Descriptor.ExecutionModel.Provider}/{Descriptor.ExecutionModel.Model}", $"provider={Descriptor.ExecutionModel.Provider}; model={Descriptor.ExecutionModel.Model}"),
        };

        if (input is not null)
        {
            evidence.Add(new AgentEvidence("context-pack-scope", input.ContextPackScope, input.ContextSummary));
            evidence.Add(new AgentEvidence("selection", "states/deltas/anchors", $"{input.SelectedStateIds.Count} / {input.SelectedDeltaIds.Count} / {input.SelectedAnchorIds.Count}"));
        }

        return new AgentTaskResult(
            task.Id,
            AgentTaskStatus.Failed,
            Id,
            Descriptor.ExecutionModel,
            output: null,
            summary: error,
            evidence: evidence,
            warnings: Array.Empty<string>(),
            errors: errors);
    }
}
