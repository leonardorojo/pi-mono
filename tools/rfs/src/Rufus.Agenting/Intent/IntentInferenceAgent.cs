using System.Text.Json;
using System.Text.RegularExpressions;
using Rufus.Agenting;

namespace Rufus.Agenting.Intent;

public sealed class IntentInferenceAgent : IAgent
{
    private static readonly Regex QuotedPhraseRegex = new("[\"'`](?<value>[^\"'`]+)[\"'`]|(?<value>[A-Z][A-Za-z0-9_-]+)", RegexOptions.Compiled);

    public string Id => "intent-inference";

    public AgentDescriptor Descriptor { get; }

    public IntentInferenceAgent()
    {
        Descriptor = new AgentDescriptor(
            id: Id,
            name: "Intent Inference Agent",
            role: "Infers the operational intent from a user prompt.",
            executionModel: new AgentExecutionModel("mock", "deterministic-v1"),
            capabilities: new[] { "infer-intent" });
    }

    public Task<AgentTaskResult> ExecuteAsync(AgentTask task, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(task);
        cancellationToken.ThrowIfCancellationRequested();

        if (!string.Equals(task.Kind, "infer-intent", StringComparison.Ordinal))
        {
            return Task.FromResult(CreateFailure(task, $"IntentInferenceAgent only accepts tasks with Kind='infer-intent', received '{task.Kind}'."));
        }

        var source = string.IsNullOrWhiteSpace(task.Input) ? task.Goal : task.Input;
        var intent = InferIntent(source);
        var entities = ExtractEntities(source);
        var constraints = ExtractConstraints(source);
        var promptIntent = new PromptIntent(
            intent,
            $"Deterministic mock intent inference classified the prompt as '{intent}'.",
            entities,
            constraints);

        var output = JsonSerializer.Serialize(promptIntent);
        var evidence = new[]
        {
            new AgentEvidence("input", "task.input", source),
            new AgentEvidence("agent", Id, Descriptor.Name),
            new AgentEvidence("execution-model", "mock/deterministic-v1", $"provider={Descriptor.ExecutionModel.Provider}; model={Descriptor.ExecutionModel.Model}"),
        };

        return Task.FromResult(new AgentTaskResult(
            task.Id,
            AgentTaskStatus.Succeeded,
            Id,
            Descriptor.ExecutionModel,
            output: output,
            summary: promptIntent.Summary,
            evidence: evidence));
    }

    private static AgentTaskResult CreateFailure(AgentTask task, string errorMessage)
    {
        var evidence = new[]
        {
            new AgentEvidence("input", "task.input", task.Input ?? task.Goal),
            new AgentEvidence("agent", "intent-inference", "IntentInferenceAgent"),
            new AgentEvidence("execution-model", "mock/deterministic-v1", "provider=mock; model=deterministic-v1"),
        };

        return new AgentTaskResult(
            task.Id,
            AgentTaskStatus.Failed,
            "intent-inference",
            new AgentExecutionModel("mock", "deterministic-v1"),
            summary: "Task kind not supported.",
            evidence: evidence,
            errors: new[] { errorMessage });
    }

    private static string InferIntent(string source)
    {
        var lowerSource = source.ToLowerInvariant();

        if (lowerSource.Contains("trace slice", StringComparison.Ordinal) || lowerSource.Contains("traceslice", StringComparison.Ordinal))
        {
            return "build-trace-slice";
        }

        if (lowerSource.Contains("context pack", StringComparison.Ordinal) || lowerSource.Contains("contextpack", StringComparison.Ordinal))
        {
            return "build-context-pack";
        }

        if (lowerSource.Contains("diff", StringComparison.Ordinal))
        {
            return "inspect-diff";
        }

        if (lowerSource.Contains("evidence", StringComparison.Ordinal) || lowerSource.Contains("summar", StringComparison.Ordinal))
        {
            return "summarize-evidence";
        }

        if (lowerSource.Contains("intent", StringComparison.Ordinal))
        {
            return "infer-operational-intent";
        }

        return "general-operational-intent";
    }

    private static IReadOnlyList<string> ExtractEntities(string source)
    {
        var entities = new List<string>();

        foreach (Match match in QuotedPhraseRegex.Matches(source))
        {
            var value = match.Groups["value"].Value.Trim();
            if (!string.IsNullOrWhiteSpace(value) && !entities.Contains(value, StringComparer.OrdinalIgnoreCase))
            {
                entities.Add(value);
            }
        }

        return entities.ToArray();
    }

    private static IReadOnlyList<string> ExtractConstraints(string source)
    {
        var clauses = source
            .Split(new[] { '.', ';', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(clause =>
            {
                var lowerClause = clause.ToLowerInvariant();
                return lowerClause.Contains("must", StringComparison.Ordinal)
                    || lowerClause.Contains("without", StringComparison.Ordinal)
                    || lowerClause.Contains("only", StringComparison.Ordinal)
                    || lowerClause.Contains("no ", StringComparison.Ordinal)
                    || lowerClause.Contains("do not", StringComparison.Ordinal)
                    || lowerClause.Contains("don't", StringComparison.Ordinal)
                    || lowerClause.Contains("never", StringComparison.Ordinal);
            })
            .ToArray();

        return clauses;
    }
}
