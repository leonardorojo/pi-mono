using System.Text;
using System.Text.Json;
using Rufus.Agenting;
using Rufus.Agenting.Intent;
using Rufus.Cli.PiIntegration;
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

        var contextPackResult = RckWorkspaceContextPackReader.Read(currentDirectory);
        if (!contextPackResult.Success)
        {
            return TraceSliceProposalLlmPipelineResult.Failure(
                contextPackResult.ErrorMessage ?? "rfs trace-slice-proposal-llm: failed to read workspace context.");
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

        var requestJson = JsonSerializer.Serialize(new
        {
            prompt = new
            {
                text = normalizedPrompt,
                isExcerpt = false,
            },
            intent = proposalIntent,
            dagQuickIndex = quickIndexResult.DagQuickIndex,
            artifactMetadata = contextPackResult.ChangedArtifacts.Select(artifact => new
            {
                artifact.Kind,
                artifact.Path,
                artifact.ChangeType,
                artifact.GitStatus,
                artifact.Source,
            }),
            allowedPolicy = new
            {
                includeStatePayloads = true,
                includeDeltaDecodedOps = true,
                includeArtifactContents = false,
                includeGitDiffs = false,
                includeStdoutStderr = false,
                includeJsonl = false,
            },
            rules = new[]
            {
                "Return only JSON.",
                "Return type rufus.trace-slice-proposal.",
                "Do not invent IDs.",
                "Do not request artifact contents.",
                "Do not request git diffs.",
                "RFS will validate all IDs and policies.",
            },
        }, JsonOptions);

        var workspaceModel = RckWorkspaceModelConfigStore.TryReadDefaultModel(currentDirectory);
        var askResult = await PiJsonEventRunner.RunAskAsync(
            currentDirectory,
            BuildLlmPrompt(requestJson),
            workspaceModel,
            cancellationToken);

        if (!askResult.Success || string.IsNullOrWhiteSpace(askResult.Answer))
        {
            return TraceSliceProposalLlmPipelineResult.Failure(
                askResult.ErrorMessage ?? "rfs trace-slice-proposal-llm: Pi JSON request failed.");
        }

        if (!TryValidateTraceSliceProposalJson(askResult.Answer, out var normalizedProposalJson, out var validationError))
        {
            var preview = askResult.Answer.Trim();
            if (preview.Length > 240)
            {
                preview = preview[..240] + "...";
            }

            return TraceSliceProposalLlmPipelineResult.Failure(
                string.IsNullOrWhiteSpace(validationError)
                    ? $"rfs trace-slice-proposal-llm: invalid TraceSliceProposal JSON from LLM. Preview: {preview}"
                    : $"{validationError} Preview: {preview}");
        }

        return TraceSliceProposalLlmPipelineResult.SuccessResult(normalizedProposalJson);
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

    private static bool TryBuildTraceSliceProposalIntent(string intentOutputJson, out object intentProjection, out string? errorMessage)
    {
        intentProjection = null!;
        errorMessage = null;

        try
        {
            using var document = JsonDocument.Parse(intentOutputJson);
            var root = document.RootElement;
            intentProjection = new
            {
                kind = GetRequiredString(root, "Intent"),
                summary = GetRequiredString(root, "Summary"),
                source = "intent-inference-agent",
            };
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
            using var document = JsonDocument.Parse(proposalJson);
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
