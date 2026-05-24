using System.Text;
using Rufus.Agenting;
using Rufus.Agenting.Intent;
using Rufus.Cli.PiIntegration;

namespace Rufus.Cli.Intent;

public interface IIntentLlmTransport
{
    Task<PiJsonAskResult> AskAsync(
        string workingDirectory,
        string prompt,
        string model,
        CancellationToken cancellationToken = default);
}

public sealed class PiJsonIntentLlmTransport : IIntentLlmTransport
{
    public Task<PiJsonAskResult> AskAsync(
        string workingDirectory,
        string prompt,
        string model,
        CancellationToken cancellationToken = default)
    {
        return PiJsonEventRunner.RunAskAsync(workingDirectory, prompt, model, cancellationToken);
    }
}

public sealed class PiIntentInferenceAgent : IAgent
{
    private const string AgentId = "pi-intent-inference";
    private const string ExecutionProvider = "pi";
    private const string SupportedKind = "infer-intent";
    private const string DefaultExecutionModel = "claude-haiku-4.5";

    private readonly IIntentLlmTransport _transport;
    private readonly string _workingDirectory;
    private readonly string _executionModel;

    public string Id => AgentId;

    public AgentDescriptor Descriptor { get; }

    public PiIntentInferenceAgent(string? workingDirectory = null, string? model = null, IIntentLlmTransport? transport = null)
    {
        _transport = transport ?? new PiJsonIntentLlmTransport();
        _workingDirectory = string.IsNullOrWhiteSpace(workingDirectory)
            ? Directory.GetCurrentDirectory()
            : workingDirectory;

        _executionModel = string.IsNullOrWhiteSpace(model)
            ? DefaultExecutionModel
            : model.Trim();

        Descriptor = new AgentDescriptor(
            id: Id,
            name: "LLM Intent Inference Agent",
            role: "Infer operational intent from user prompt using LLM.",
            executionModel: new AgentExecutionModel(ExecutionProvider, _executionModel),
            capabilities: new[] { "infer-intent" });
    }

    public async Task<AgentTaskResult> ExecuteAsync(AgentTask task, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(task);
        cancellationToken.ThrowIfCancellationRequested();

        if (!string.Equals(task.Kind, SupportedKind, StringComparison.Ordinal))
        {
            return CreateFailure(task, $"PiIntentInferenceAgent only accepts tasks with Kind='{SupportedKind}', received '{task.Kind}'.", task.Input ?? task.Goal);
        }

        var sourcePrompt = string.IsNullOrWhiteSpace(task.Input) ? task.Goal : task.Input;
        var llmPrompt = BuildPrompt(sourcePrompt);

        var askResult = await _transport.AskAsync(
            _workingDirectory,
            llmPrompt,
            _executionModel,
            cancellationToken);

        if (!askResult.Success || string.IsNullOrWhiteSpace(askResult.Answer))
        {
            return CreateFailure(
                task,
                askResult.ErrorMessage ?? "rfs intent --llm: Pi JSON request failed.",
                sourcePrompt,
                askResult.Provider,
                askResult.Model);
        }

        if (!PromptIntentJsonCodec.TryParse(askResult.Answer, out var promptIntent, out var parseError))
        {
            var preview = askResult.Answer.Trim();
            if (preview.Length > 240)
            {
                preview = preview[..240] + "...";
            }

            return CreateFailure(
                task,
                string.IsNullOrWhiteSpace(parseError)
                    ? $"rfs intent --llm: invalid PromptIntent JSON from LLM. Preview: {preview}"
                    : $"{parseError} Preview: {preview}",
                sourcePrompt,
                askResult.Provider,
                askResult.Model,
                preview);
        }

        var outputJson = PromptIntentJsonCodec.Write(promptIntent!);
        var evidence = new[]
        {
            new AgentEvidence("input", "task.input", sourcePrompt),
            new AgentEvidence("agent", Id, Descriptor.Name),
            new AgentEvidence("execution-model", $"{ExecutionProvider}/{_executionModel}", $"provider={ExecutionProvider}; model={_executionModel}"),
            new AgentEvidence("prompt", "llm-prompt", "intent-only JSON request"),
        };

        return new AgentTaskResult(
            task.Id,
            AgentTaskStatus.Succeeded,
            Id,
            Descriptor.ExecutionModel,
            output: outputJson,
            summary: promptIntent!.Summary,
            evidence: evidence);
    }

    private static string BuildPrompt(string sourcePrompt)
    {
        var builder = new StringBuilder();
        builder.AppendLine("Return only a single JSON object and nothing else.");
        builder.AppendLine("Do not use markdown fences.");
        builder.AppendLine("Do not add commentary, labels, or explanations.");
        builder.AppendLine("Infer only from the user prompt.");
        builder.AppendLine("If the prompt is ambiguous, make the summary say so.");
        builder.AppendLine("Do not invent file reads, diffs, or runtime facts.");
        builder.AppendLine("Required JSON shape:");
        builder.AppendLine("{");
        builder.AppendLine("  \"intent\": \"...\",");
        builder.AppendLine("  \"summary\": \"...\",");
        builder.AppendLine("  \"entities\": [],");
        builder.AppendLine("  \"constraints\": []");
        builder.AppendLine("}");
        builder.AppendLine();
        builder.AppendLine("User prompt:");
        builder.AppendLine(sourcePrompt);
        return builder.ToString();
    }

    private AgentTaskResult CreateFailure(
        AgentTask task,
        string errorMessage,
        string sourcePrompt,
        string? provider = null,
        string? model = null,
        string? outputPreview = null)
    {
        var evidence = new List<AgentEvidence>
        {
            new("input", "task.input", sourcePrompt),
            new("agent", AgentId, "LLM Intent Inference Agent"),
            new("execution-model", $"{ExecutionProvider}/{_executionModel}", $"provider={ExecutionProvider}; model={_executionModel}"),
        };

        if (!string.IsNullOrWhiteSpace(provider) || !string.IsNullOrWhiteSpace(model))
        {
            evidence.Add(new AgentEvidence("transport", "pi", $"provider={(string.IsNullOrWhiteSpace(provider) ? "(unknown)" : provider)}; model={(string.IsNullOrWhiteSpace(model) ? "(unknown)" : model)}"));
        }

        if (!string.IsNullOrWhiteSpace(outputPreview))
        {
            evidence.Add(new AgentEvidence("output-preview", "llm-answer", outputPreview));
        }

        return new AgentTaskResult(
            task.Id,
            AgentTaskStatus.Failed,
            AgentId,
            new AgentExecutionModel(ExecutionProvider, _executionModel),
            summary: "LLM intent inference failed.",
            evidence: evidence,
            errors: new[] { errorMessage });
    }
}
