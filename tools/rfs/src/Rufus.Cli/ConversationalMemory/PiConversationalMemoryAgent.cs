using System.Text;
using System.Text.Json;
using Rufus.Agenting;
using Rufus.Cli.PiIntegration;
using Rufus.RCK.Workspace;

namespace Rufus.Cli.ConversationalMemory;

public interface IConversationalMemoryLlmTransport
{
    Task<PiJsonAskResult> AskAsync(
        string workingDirectory,
        string prompt,
        string model,
        CancellationToken cancellationToken = default);
}

public sealed class PiJsonConversationalMemoryLlmTransport : IConversationalMemoryLlmTransport
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

public sealed class PiConversationalMemoryAgent : IAgent
{
    private const string AgentId = "pi-conversational-memory";
    private const string ExecutionProvider = "pi";
    private const string SupportedKind = "build-conversational-memory";
    private const string DefaultExecutionModel = "claude-haiku-4.5";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    private readonly string _workingDirectory;
    private readonly string _executionModel;
    private readonly IConversationalMemoryLlmTransport _transport;

    public PiConversationalMemoryAgent(string? workingDirectory = null, string? model = null, IConversationalMemoryLlmTransport? transport = null)
    {
        _workingDirectory = string.IsNullOrWhiteSpace(workingDirectory)
            ? Directory.GetCurrentDirectory()
            : workingDirectory;
        _executionModel = string.IsNullOrWhiteSpace(model)
            ? DefaultExecutionModel
            : model.Trim();
        _transport = transport ?? new PiJsonConversationalMemoryLlmTransport();

        Descriptor = new AgentDescriptor(
            id: AgentId,
            name: "Pi Conversational Memory Agent",
            role: "Produce a compact conversational continuity projection from recent RCK interactions.",
            executionModel: new AgentExecutionModel(ExecutionProvider, _executionModel),
            capabilities: new[] { SupportedKind });
    }

    public string Id => AgentId;

    public AgentDescriptor Descriptor { get; }

    public async Task<AgentTaskResult> ExecuteAsync(AgentTask task, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(task);
        cancellationToken.ThrowIfCancellationRequested();

        if (!string.Equals(task.Kind, SupportedKind, StringComparison.Ordinal))
        {
            return CreateFailure(task, $"PiConversationalMemoryAgent only accepts tasks with Kind='{SupportedKind}', received '{task.Kind}'.", task.Input ?? task.Goal);
        }

        if (string.IsNullOrWhiteSpace(task.Input))
        {
            return CreateFailure(task, "Conversational memory task input is missing JSON.", task.Goal);
        }

        RckConversationalMemoryInput input;
        try
        {
            input = JsonSerializer.Deserialize<RckConversationalMemoryInput>(task.Input, JsonOptions)
                ?? throw new JsonException("RckConversationalMemoryInput deserialized to null.");
        }
        catch (Exception ex) when (ex is JsonException or ArgumentException)
        {
            return CreateFailure(task, $"Invalid RckConversationalMemoryInput JSON: {ex.Message}", task.Input);
        }

        if (string.IsNullOrWhiteSpace(input.CurrentPrompt))
        {
            return CreateFailure(task, "RckConversationalMemoryInput.CurrentPrompt is required.", task.Input);
        }

        if (input.Limits is null)
        {
            return CreateFailure(task, "RckConversationalMemoryInput.Limits is required.", task.Input);
        }

        var llmPrompt = BuildPrompt(input);
        var llmResult = await _transport
            .AskAsync(_workingDirectory, llmPrompt, Descriptor.ExecutionModel.Model, cancellationToken)
            .ConfigureAwait(false);

        if (!llmResult.Success || string.IsNullOrWhiteSpace(llmResult.Answer))
        {
            return CreateFailure(
                task,
                llmResult.ErrorMessage ?? "rfs conversational-memory-llm: Pi JSON request failed.",
                input.CurrentPrompt,
                llmResult.Provider,
                llmResult.Model,
                llmPrompt);
        }

        if (!RckConversationalMemoryJsonCodec.TryParse(llmResult.Answer, out var conversationalMemory, out var parseError))
        {
            return CreateFailure(
                task,
                string.IsNullOrWhiteSpace(parseError)
                    ? "rfs conversational-memory-llm: invalid ConversationalMemory JSON from LLM."
                    : parseError,
                input.CurrentPrompt,
                llmResult.Provider,
                llmResult.Model,
                Preview(llmResult.Answer));
        }

        var outputJson = RckConversationalMemoryJsonCodec.Write(conversationalMemory!);
        var evidence = BuildEvidence(input, llmResult, llmPrompt, conversationalMemory!);

        return new AgentTaskResult(
            task.Id,
            AgentTaskStatus.Succeeded,
            Id,
            Descriptor.ExecutionModel,
            output: outputJson,
            summary: conversationalMemory!.Summary,
            evidence: evidence,
            warnings: conversationalMemory.Warnings,
            errors: Array.Empty<string>());
    }

    private static string BuildPrompt(RckConversationalMemoryInput input)
    {
        var builder = new StringBuilder();
        builder.AppendLine("Return only a single JSON object and nothing else.");
        builder.AppendLine("Do not use markdown fences.");
        builder.AppendLine("Do not add commentary, labels, or explanations.");
        builder.AppendLine("Summarize recent conversation continuity.");
        builder.AppendLine("Use only the provided recent interactions.");
        builder.AppendLine("Do not invent decisions.");
        builder.AppendLine("Do not infer repository structure beyond the provided input.");
        builder.AppendLine("Do not perform TraceSlice selection.");
        builder.AppendLine("Do not select anchors/states/deltas.");
        builder.AppendLine("Do not include file contents, diffs, stdout/stderr, raw JSONL, or tool logs.");
        builder.AppendLine("Preserve continuity useful for resolving references like 'eso', 'lo anterior', and 'esta idea'.");
        builder.AppendLine("Keep output compact.");
        builder.AppendLine("Respect schemaVersion = 1.");
        builder.AppendLine("Return type = rufus.conversational-memory.");
        builder.AppendLine("Do not add extra top-level keys.");
        builder.AppendLine();
        builder.AppendLine("Required exact output shape:");
        builder.AppendLine("{");
        builder.AppendLine("  \"type\": \"rufus.conversational-memory\",");
        builder.AppendLine("  \"schemaVersion\": 1,");
        builder.AppendLine("  \"summary\": \"...\",");
        builder.AppendLine("  \"activeTopic\": \"...\",");
        builder.AppendLine("  \"openQuestions\": [],");
        builder.AppendLine("  \"recentDecisions\": [],");
        builder.AppendLine("  \"continuityHints\": [],");
        builder.AppendLine("  \"warnings\": []");
        builder.AppendLine("}");
        builder.AppendLine("Do not include payloadCanonicalJson, selectedStateIds, selectedDeltaIds, selectedAnchorIds, diff --git, stdout, stderr, message_update, message_end, assistantMessageEvent, or .rfs/rck.");
        builder.AppendLine();
        builder.AppendLine("Input JSON:");
        builder.AppendLine(JsonSerializer.Serialize(input, JsonOptions));
        return builder.ToString();
    }

    private static IReadOnlyList<AgentEvidence> BuildEvidence(
        RckConversationalMemoryInput input,
        PiJsonAskResult llmResult,
        string llmPrompt,
        RckConversationalMemory conversationalMemory)
    {
        var evidence = new List<AgentEvidence>
        {
            new("agent", AgentId, "Pi Conversational Memory Agent"),
            new("execution-model", $"{llmResult.Provider ?? ExecutionProvider}/{llmResult.Model ?? DefaultExecutionModel}", $"provider={llmResult.Provider ?? ExecutionProvider}; model={llmResult.Model ?? DefaultExecutionModel}"),
            new("prompt", "current-prompt", input.CurrentPrompt),
            new("recent-interactions", input.RecentInteractions.Count.ToString(), $"interactions={input.RecentInteractions.Count}; max={input.Limits.MaxInteractions}"),
            new("prompt-to-send", "llm-prompt", llmPrompt),
            new("memory-summary", conversationalMemory.ActiveTopic, conversationalMemory.Summary),
        };

        return evidence;
    }

    private AgentTaskResult CreateFailure(
        AgentTask task,
        string errorMessage,
        string prompt,
        string? provider = null,
        string? model = null,
        string? outputPreview = null)
    {
        var evidence = new List<AgentEvidence>
        {
            new("agent", AgentId, "Pi Conversational Memory Agent"),
            new("execution-model", $"{Descriptor.ExecutionModel.Provider}/{Descriptor.ExecutionModel.Model}", $"provider={Descriptor.ExecutionModel.Provider}; model={Descriptor.ExecutionModel.Model}"),
            new("input", "current-prompt", prompt),
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
            Id,
            Descriptor.ExecutionModel,
            summary: "LLM conversational memory failed.",
            evidence: evidence,
            errors: new[] { errorMessage });
    }

    private static string Preview(string value)
    {
        var preview = value.Trim();
        if (preview.Length > 240)
        {
            preview = preview[..240] + "...";
        }

        return preview;
    }
}
