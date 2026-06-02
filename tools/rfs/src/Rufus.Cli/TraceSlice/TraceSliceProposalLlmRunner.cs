using System.Text;
using System.Text.Json;
using Rufus.Agenting;
using Rufus.Agenting.Intent;
using Rufus.Cli.Json;
using Rufus.Cli.PiIntegration;
using Rufus.Cli.Tui;
using Rufus.RCK.Workspace;

namespace Rufus.Cli.TraceSlice;

public static class TraceSliceProposalLlmRunner
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    public static async Task<TraceSliceProposalLlmPipelineResult> BuildProposalAsync(
        string prompt,
        string currentDirectory,
        CancellationToken cancellationToken = default)
    {
        var normalizedPrompt = prompt.Trim();
        if (string.IsNullOrWhiteSpace(normalizedPrompt))
        {
            return TraceSliceProposalLlmPipelineResult.Failure("rfs trace-slice-proposal-llm: missing prompt.");
        }

        var quickIndexResult = RckTraceSliceProposalInputBuilder.Build(currentDirectory, 5);
        if (!quickIndexResult.Success || quickIndexResult.DagQuickIndex is null)
        {
            return TraceSliceProposalLlmPipelineResult.Failure(
                quickIndexResult.ErrorMessage ?? "rfs trace-slice-proposal-llm: failed to read TraceSliceProposal input.");
        }

        var intentAgent = new IntentInferenceAgent();
        var intentTask = new AgentTask(
            id: $"intent-{Guid.NewGuid():N}",
            kind: "infer-intent",
            goal: "infer deterministic operational intent for a trace slice proposal",
            input: normalizedPrompt,
            expectedOutput: "PromptIntent JSON");

        var intentResult = await intentAgent.ExecuteAsync(intentTask, cancellationToken);
        if (intentResult.Status == AgentTaskStatus.Failed || string.IsNullOrWhiteSpace(intentResult.Output))
        {
            var firstError = intentResult.Errors.FirstOrDefault();
            return TraceSliceProposalLlmPipelineResult.Failure(firstError ?? "rfs trace-slice-proposal-llm: intent inference failed.");
        }

        if (!TryBuildTraceSliceProposalIntent(intentResult.Output, out var proposalIntent, out var intentErrorMessage))
        {
            return TraceSliceProposalLlmPipelineResult.Failure(intentErrorMessage ?? "rfs trace-slice-proposal-llm: failed to project intent result.");
        }

        var proposalInput = new TraceSliceProposalAgentInput(
            normalizedPrompt,
            proposalIntent,
            quickIndexResult.DagQuickIndex,
            new TraceSliceProposalAgentLimits(MaxStates: 5, MaxDeltas: 5),
            new[]
            {
                "Select only ids available in dagQuickIndex.",
                "Respect maxStates/maxDeltas.",
                "includeArtifactContents=false.",
                "includeGitDiffs=false.",
                "includeStdoutStderr=false.",
                "includeJsonl=false.",
                "Return JSON only.",
                "No markdown fences or extra prose.",
            });

        var proposalAgent = new PiTraceSliceProposalAgent(currentDirectory);
        var proposalTask = new AgentTask(
            id: $"trace-slice-proposal-{Guid.NewGuid():N}",
            kind: "propose-trace-slice",
            goal: "build an LLM-backed trace slice proposal",
            input: JsonSerializer.Serialize(proposalInput, JsonOptions),
            expectedOutput: "TraceSliceProposal JSON");

        var proposalResult = await proposalAgent.ExecuteAsync(proposalTask, cancellationToken).ConfigureAwait(false);
        if (proposalResult.Status == AgentTaskStatus.Failed || string.IsNullOrWhiteSpace(proposalResult.Output))
        {
            var firstError = proposalResult.Errors.FirstOrDefault();
            return TraceSliceProposalLlmPipelineResult.Failure(firstError ?? "rfs trace-slice-proposal-llm: trace slice proposal agent failed.");
        }

        return TraceSliceProposalLlmPipelineResult.SuccessResult(proposalResult.Output);
    }

    public static async Task<RckTraceSliceProposalValidationResult> BuildValidatedAsync(
        string prompt,
        string currentDirectory,
        CancellationToken cancellationToken = default)
    {
        var proposalResult = await BuildProposalAsync(prompt, currentDirectory, cancellationToken);
        if (!proposalResult.Success || string.IsNullOrWhiteSpace(proposalResult.ProposalJson))
        {
            return RckTraceSliceProposalValidationResult.Failure(
                proposalResult.ErrorMessage ?? "rfs trace-slice-validate-llm: failed to build TraceSliceProposal.");
        }

        return RckTraceSliceProposalValidator.Validate(proposalResult.ProposalJson, currentDirectory, maxStates: 5, maxDeltas: 5);
    }

    private static string BuildLlmPrompt(string requestJson)
    {
        var builder = new StringBuilder();
        builder.AppendLine("Return only a single JSON object and nothing else.");
        builder.AppendLine("Do not use markdown fences.");
        builder.AppendLine("Do not add commentary, labels, or explanations.");
        builder.AppendLine("Return type rufus.trace-slice-proposal.");
        builder.AppendLine("RFS will validate all IDs and materialization policy fields.");
        builder.AppendLine("Do not invent IDs.");
        builder.AppendLine("Do not request artifact contents.");
        builder.AppendLine("Do not request git diffs.");
        builder.AppendLine("Do not include stdout/stderr.");
        builder.AppendLine("Do not include JSONL.");
        builder.AppendLine();
        builder.AppendLine("Required exact output shape:");
        builder.AppendLine("{");
        builder.AppendLine("  \"type\": \"rufus.trace-slice-proposal\",");
        builder.AppendLine("  \"schemaVersion\": 1,");
        builder.AppendLine("  \"prompt\": { \"text\": \"...\", \"isExcerpt\": false },");
        builder.AppendLine("  \"intent\": { \"kind\": \"...\", \"summary\": \"...\", \"source\": \"intent-inference-agent\" },");
        builder.AppendLine("  \"requestedSelection\": { \"stateIds\": [], \"deltaIds\": [], \"anchorIds\": [], \"artifactRefs\": [] },");
        builder.AppendLine("  \"requestedMaterializationPolicy\": { \"includeStatePayloads\": true, \"includeDeltaDecodedOps\": true, \"includeArtifactContents\": false, \"includeGitDiffs\": false, \"includeStdoutStderr\": false, \"includeJsonl\": false },");
        builder.AppendLine("  \"rationale\": [],");
        builder.AppendLine("  \"confidence\": 0.0,");
        builder.AppendLine("  \"warnings\": []");
        builder.AppendLine("}");
        builder.AppendLine("Do not use the keys 'selection' or 'materializationPolicy'.");
        builder.AppendLine("Do not add extra top-level keys.");
        builder.AppendLine();
        builder.AppendLine("Request JSON:");
        builder.AppendLine(requestJson);
        return builder.ToString();
    }

    private static bool TryBuildTraceSliceProposalIntent(string intentOutputJson, out RckTraceSliceProposalIntentProjection intentProjection, out string? errorMessage)
    {
        intentProjection = default!;
        errorMessage = null;

        try
        {
            using var document = JsonDocument.Parse(LlmJsonOutputNormalizer.Normalize(intentOutputJson));
            var root = document.RootElement;
            intentProjection = new RckTraceSliceProposalIntentProjection(
                Kind: GetRequiredString(root, "Intent"),
                Summary: GetRequiredString(root, "Summary"),
                Source: "intent-inference-agent");
            return true;
        }
        catch (Exception ex)
        {
            errorMessage = $"rfs trace-slice-proposal-llm: failed to project intent result: {ex.Message}";
            return false;
        }
    }

    private static bool TryValidateTraceSliceProposalJson(string proposalJson, out string normalizedProposalJson, out string? errorMessage)
    {
        normalizedProposalJson = string.Empty;
        errorMessage = null;

        try
        {
            using var document = JsonDocument.Parse(LlmJsonOutputNormalizer.Normalize(proposalJson));
            var root = document.RootElement;

            if (!string.Equals(GetRequiredString(root, "type"), "rufus.trace-slice-proposal", StringComparison.Ordinal))
            {
                errorMessage = "rfs trace-slice-proposal-llm: expected type=rufus.trace-slice-proposal.";
                return false;
            }

            if (root.TryGetProperty("schemaVersion", out var schemaVersionElement))
            {
                if (schemaVersionElement.ValueKind != JsonValueKind.Number || schemaVersionElement.GetInt32() != 1)
                {
                    errorMessage = "rfs trace-slice-proposal-llm: expected schemaVersion=1.";
                    return false;
                }
            }
            else
            {
                errorMessage = "rfs trace-slice-proposal-llm: missing schemaVersion.";
                return false;
            }

            var promptElement = ValidateRequiredObject(root, "prompt");
            var intentElement = ValidateRequiredObject(root, "intent");
            var selectionElement = ValidateRequiredObject(root, "requestedSelection");
            var materializationPolicyElement = ValidateRequiredObject(root, "requestedMaterializationPolicy");
            ValidateRequiredArray(root, "rationale");
            ValidateRequiredArray(root, "warnings");

            _ = GetRequiredString(promptElement, "text");
            if (!promptElement.TryGetProperty("isExcerpt", out var isExcerptElement) || isExcerptElement.ValueKind != JsonValueKind.False)
            {
                errorMessage = "rfs trace-slice-proposal-llm: expected prompt.isExcerpt=false.";
                return false;
            }

            _ = GetRequiredString(intentElement, "kind");
            _ = GetRequiredString(intentElement, "summary");
            _ = GetRequiredString(intentElement, "source");

            ValidateRequiredArray(selectionElement, "stateIds");
            ValidateRequiredArray(selectionElement, "deltaIds");
            ValidateRequiredArray(selectionElement, "anchorIds");
            ValidateRequiredArray(selectionElement, "artifactRefs");

            foreach (var propertyName in new[] { "includeStatePayloads", "includeDeltaDecodedOps", "includeArtifactContents", "includeGitDiffs", "includeStdoutStderr", "includeJsonl" })
            {
                if (!materializationPolicyElement.TryGetProperty(propertyName, out var property) || (property.ValueKind != JsonValueKind.True && property.ValueKind != JsonValueKind.False))
                {
                    errorMessage = $"rfs trace-slice-proposal-llm: missing materialization policy field '{propertyName}'.";
                    return false;
                }
            }

            if (materializationPolicyElement.GetProperty("includeArtifactContents").ValueKind != JsonValueKind.False
                || materializationPolicyElement.GetProperty("includeGitDiffs").ValueKind != JsonValueKind.False
                || materializationPolicyElement.GetProperty("includeStdoutStderr").ValueKind != JsonValueKind.False
                || materializationPolicyElement.GetProperty("includeJsonl").ValueKind != JsonValueKind.False)
            {
                errorMessage = "rfs trace-slice-proposal-llm: expected restricted materialization policy flags to be false.";
                return false;
            }

            if (!TryValidateNoForbiddenContent(root, out errorMessage))
            {
                return false;
            }

            if (!root.TryGetProperty("confidence", out var confidenceElement) || confidenceElement.ValueKind != JsonValueKind.Number)
            {
                errorMessage = "rfs trace-slice-proposal-llm: missing confidence.";
                return false;
            }

            normalizedProposalJson = JsonSerializer.Serialize(root, JsonOptions);
            return true;
        }
        catch (JsonException ex)
        {
            errorMessage = $"rfs trace-slice-proposal-llm: invalid JSON from LLM: {ex.Message}";
            return false;
        }
        catch (Exception ex)
        {
            errorMessage = $"rfs trace-slice-proposal-llm: invalid proposal payload: {ex.Message}";
            return false;
        }
    }

    private static bool TryValidateNoForbiddenContent(JsonElement element, out string? errorMessage)
    {
        foreach (var stringValue in EnumerateStringValues(element))
        {
            if (ContainsForbiddenFragment(stringValue, out var fragment))
            {
                errorMessage = $"rfs trace-slice-proposal-llm: forbidden content detected in LLM output ('{fragment}').";
                return false;
            }
        }

        errorMessage = null;
        return true;
    }

    private static IEnumerable<string> EnumerateStringValues(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.String:
                yield return element.GetString() ?? string.Empty;
                yield break;

            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    foreach (var value in EnumerateStringValues(property.Value))
                    {
                        yield return value;
                    }
                }
                yield break;

            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                {
                    foreach (var value in EnumerateStringValues(item))
                    {
                        yield return value;
                    }
                }
                yield break;

            default:
                yield break;
        }
    }

    private static bool ContainsForbiddenFragment(string value, out string fragment)
    {
        foreach (var candidate in new[]
        {
            "diff --git",
            "message_update",
            "message_end",
            "assistantMessageEvent",
            "```",
            ".rfs/rck",
            "stdout",
            "stderr",
        })
        {
            if (value.Contains(candidate, StringComparison.OrdinalIgnoreCase))
            {
                fragment = candidate;
                return true;
            }
        }

        foreach (var candidate in new[]
        {
            "BEGIN PRIVATE KEY",
            "api key",
            "secret=",
            "token=",
            "password=",
        })
        {
            if (value.Contains(candidate, StringComparison.OrdinalIgnoreCase))
            {
                fragment = candidate;
                return true;
            }
        }

        fragment = string.Empty;
        return false;
    }

    private static JsonElement ValidateRequiredObject(JsonElement element, string propertyName)
    {
        if (element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.Object)
        {
            return property;
        }

        throw new InvalidDataException($"Missing object property '{propertyName}'.");
    }

    private static void ValidateRequiredArray(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidDataException($"Missing array property '{propertyName}'.");
        }
    }

    private static string GetRequiredString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.String)
        {
            throw new InvalidDataException($"Missing string property '{propertyName}'.");
        }

        var value = property.GetString();
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidDataException($"String property '{propertyName}' cannot be empty.");
        }

        return value;
    }
}

public sealed record TraceSliceProposalLlmPipelineResult(
    bool Success,
    string? ErrorMessage,
    string? ProposalJson)
{
    public static TraceSliceProposalLlmPipelineResult Failure(string errorMessage)
        => new(false, errorMessage, null);

    public static TraceSliceProposalLlmPipelineResult SuccessResult(string proposalJson)
        => new(true, null, proposalJson);
}
