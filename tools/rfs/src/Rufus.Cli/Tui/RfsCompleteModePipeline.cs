using System.Text;
using System.Text.Json;
using Rufus.Agenting;
using Rufus.Agenting.Intent;
using Rufus.Agenting.TraceSlice;
using Rufus.RCK.Workspace;

namespace Rufus.Cli.Tui;

public static class RfsCompleteModePipeline
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public static async Task<RckTraceSliceProposalBuildResult> BuildProposalAsync(
        string prompt,
        string? currentDirectory = null,
        int maxRecentInteractions = 5,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(prompt))
        {
            return RckTraceSliceProposalBuildResult.Failure("rfs trace-slice-proposal: prompt is required.");
        }

        var normalizedPrompt = prompt.Trim();
        var quickIndexResult = RckTraceSliceProposalInputBuilder.Build(currentDirectory, maxRecentInteractions);
        if (!quickIndexResult.Success || quickIndexResult.DagQuickIndex is null)
        {
            return RckTraceSliceProposalBuildResult.Failure(
                quickIndexResult.ErrorMessage ?? "rfs trace-slice-proposal: failed to read TraceSliceProposal input.");
        }

        var intentAgent = new IntentInferenceAgent();
        var intentTask = new AgentTask(
            id: $"intent-{Guid.NewGuid():N}",
            kind: "infer-intent",
            goal: "infer deterministic operational intent for a trace slice proposal",
            input: normalizedPrompt,
            expectedOutput: "PromptIntent JSON");

        var intentResult = await intentAgent.ExecuteAsync(intentTask, cancellationToken).ConfigureAwait(false);
        if (intentResult.Status == AgentTaskStatus.Failed || string.IsNullOrWhiteSpace(intentResult.Output))
        {
            var firstError = intentResult.Errors.FirstOrDefault();
            return RckTraceSliceProposalBuildResult.Failure(firstError ?? "rfs trace-slice-proposal: intent inference failed.");
        }

        if (!TryBuildTraceSliceProposalIntent(intentResult.Output, out var proposalIntent, out var intentErrorMessage))
        {
            return RckTraceSliceProposalBuildResult.Failure(intentErrorMessage ?? "rfs trace-slice-proposal: failed to project intent result.");
        }

        var plannerInputJson = JsonSerializer.Serialize(new
        {
            prompt = normalizedPrompt,
            intent = proposalIntent,
            dagQuickIndex = quickIndexResult.DagQuickIndex,
        }, JsonOptions);

        var plannerAgent = new TraceSlicePlannerAgent();
        var plannerTask = new AgentTask(
            id: $"trace-slice-proposal-{Guid.NewGuid():N}",
            kind: "propose-trace-slice",
            goal: "build a deterministic anchor-aware trace slice proposal",
            input: plannerInputJson,
            expectedOutput: "TraceSliceProposal JSON");

        var plannerResult = await plannerAgent.ExecuteAsync(plannerTask, cancellationToken).ConfigureAwait(false);
        if (plannerResult.Status == AgentTaskStatus.Failed || string.IsNullOrWhiteSpace(plannerResult.Output))
        {
            var firstError = plannerResult.Errors.FirstOrDefault();
            return RckTraceSliceProposalBuildResult.Failure(firstError ?? "rfs trace-slice-proposal: trace slice planner failed.");
        }

        var proposalSummary = plannerResult.Summary ?? "TraceSlice proposal generated.";
        var warnings = MergeDistinct(intentResult.Warnings, plannerResult.Warnings);

        return RckTraceSliceProposalBuildResult.SuccessResult(
            proposalJson: plannerResult.Output,
            intentKind: proposalIntent.Kind,
            intentSummary: proposalIntent.Summary,
            proposalSummary: proposalSummary,
            proposalSource: plannerAgent.Id,
            warnings: warnings);
    }

    public static async Task<RckTraceSliceProposalValidationResult> BuildValidatedAsync(
        string prompt,
        string? currentDirectory = null,
        int maxRecentInteractions = 5,
        CancellationToken cancellationToken = default)
    {
        var proposalResult = await BuildProposalAsync(prompt, currentDirectory, maxRecentInteractions, cancellationToken).ConfigureAwait(false);
        if (!proposalResult.Success || string.IsNullOrWhiteSpace(proposalResult.ProposalJson))
        {
            return RckTraceSliceProposalValidationResult.Failure(
                proposalResult.ErrorMessage ?? "rfs trace-slice-validate: failed to build TraceSliceProposal.");
        }

        return RckTraceSliceProposalValidator.Validate(proposalResult.ProposalJson, currentDirectory, maxStates: 5, maxDeltas: 5);
    }

    public static async Task<RfsCompleteModeBuildResult> BuildAsync(
        string prompt,
        string? currentDirectory = null,
        int maxRecentInteractions = 5,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(prompt))
        {
            return RfsCompleteModeBuildResult.Failure("rfs complete mode requires a prompt.");
        }

        var proposalResult = await BuildProposalAsync(prompt, currentDirectory, maxRecentInteractions, cancellationToken).ConfigureAwait(false);
        if (!proposalResult.Success || string.IsNullOrWhiteSpace(proposalResult.ProposalJson))
        {
            return RfsCompleteModeBuildResult.Failure(
                proposalResult.ErrorMessage ?? "rfs complete mode: failed to build TraceSliceProposal.");
        }

        var validationResult = RckTraceSliceProposalValidator.Validate(proposalResult.ProposalJson, currentDirectory, maxStates: 5, maxDeltas: 5);
        if (!validationResult.Success || string.IsNullOrWhiteSpace(validationResult.Json))
        {
            return RfsCompleteModeBuildResult.Failure(
                validationResult.ErrorMessage ?? "rfs complete mode: failed to validate TraceSliceProposal.");
        }

        var contextPackResult = RckTraceSliceContextPackBuilder.BuildFromValidatedTraceSlice(validationResult.Json, currentDirectory);
        if (!contextPackResult.Success || string.IsNullOrWhiteSpace(contextPackResult.Json))
        {
            return RfsCompleteModeBuildResult.Failure(
                contextPackResult.ErrorMessage ?? "rfs complete mode: failed to build validated ContextPack.");
        }

        var summary = ParseCompleteSummary(
            proposalResult,
            validationResult.Json,
            contextPackResult.Json,
            normalizedPrompt: prompt.Trim());

        return summary;
    }

    private static bool TryBuildTraceSliceProposalIntent(
        string intentOutputJson,
        out RckTraceSliceProposalIntentProjection intentProjection,
        out string? errorMessage)
    {
        intentProjection = default!;
        errorMessage = null;

        try
        {
            using var document = JsonDocument.Parse(intentOutputJson);
            var root = document.RootElement;
            intentProjection = new RckTraceSliceProposalIntentProjection(
                Kind: GetRequiredString(root, "Intent"),
                Summary: GetRequiredString(root, "Summary"),
                Source: "intent-inference-agent");
            return true;
        }
        catch (Exception ex)
        {
            errorMessage = $"rfs trace-slice-proposal: failed to project intent result: {ex.Message}";
            return false;
        }
    }

    private static RfsCompleteModeBuildResult ParseCompleteSummary(
        RckTraceSliceProposalBuildResult proposalResult,
        string validatedTraceSliceJson,
        string contextPackJson,
        string normalizedPrompt)
    {
        using var traceSliceDocument = JsonDocument.Parse(validatedTraceSliceJson);
        var traceSliceRoot = traceSliceDocument.RootElement;

        var validationStatus = GetRequiredString(GetRequiredObject(traceSliceRoot, "validation"), "status");
        var selectionElement = GetRequiredObject(traceSliceRoot, "selection");
        var selectionStrategy = GetRequiredString(selectionElement, "strategy");
        var selectedStateIds = ReadStringArray(selectionElement, "stateIds");
        var selectedDeltaIds = ReadStringArray(selectionElement, "deltaIds");
        var selectedAnchorIds = ReadStringArray(selectionElement, "anchorIds");
        var materializationPolicySummary = BuildMaterializationPolicySummary(GetRequiredObject(traceSliceRoot, "materializationPolicy"));
        var warnings = MergeDistinct(proposalResult.Warnings, ReadStringArray(GetRequiredObject(traceSliceRoot, "validation"), "reasons"));
        var omissions = ReadStringArray(traceSliceRoot, "omissions");

        using var contextPackDocument = JsonDocument.Parse(contextPackJson);
        var contextPackRoot = contextPackDocument.RootElement;
        var contextPackScope = GetOptionalString(contextPackRoot, "scope") ?? "trace-slice-validated";
        var artifactRefCount = ReadArtifactCount(contextPackRoot);
        var contextSummary = BuildContextSummary(contextPackRoot, validationStatus, selectionStrategy, selectedStateIds.Count, selectedDeltaIds.Count, selectedAnchorIds.Count);

        var promptToSend = BuildMainLlmPrompt(normalizedPrompt, contextSummary, contextPackJson);
        var estimatedChars = promptToSend.Length;
        var estimatedTokens = EstimateTokens(estimatedChars);
        var truncated = false;

        if (estimatedChars > 32000)
        {
            warnings = MergeDistinct(warnings, new[] { "complete-mode prompt exceeds 32000 characters; no truncation applied." });
        }

        return RfsCompleteModeBuildResult.SuccessResult(
            promptToSend: promptToSend,
            intentKind: proposalResult.IntentKind,
            intentSummary: proposalResult.IntentSummary,
            proposalSummary: proposalResult.ProposalSummary,
            proposalSource: proposalResult.ProposalSource,
            validationStatus: validationStatus,
            traceSliceSelectionStrategy: selectionStrategy,
            contextPackScope: contextPackScope,
            selectedStateIds: selectedStateIds,
            selectedDeltaIds: selectedDeltaIds,
            selectedAnchorIds: selectedAnchorIds,
            artifactRefCount: artifactRefCount,
            materializationPolicySummary: materializationPolicySummary,
            estimatedChars: estimatedChars,
            estimatedTokens: estimatedTokens,
            truncated: truncated,
            warnings: warnings,
            omissions: omissions);
    }

    private static string BuildMainLlmPrompt(string normalizedPrompt, string contextSummary, string contextPackJson)
    {
        var builder = new StringBuilder();
        builder.AppendLine("You are assisting inside an RFS repository session.");
        builder.AppendLine("Use the following validated ContextPack. It was selected through the governed pipeline:");
        builder.AppendLine("Prompt → Intent → TraceSliceProposal → RFS Validation → TraceSlice → ContextPack.");
        builder.AppendLine("Do not assume file contents unless provided.");
        builder.AppendLine("Respect omissions and materialization policy.");
        builder.AppendLine();
        builder.AppendLine("[VALIDATED CONTEXTPACK SUMMARY]");
        builder.AppendLine(contextSummary);
        builder.AppendLine();
        builder.AppendLine("[VALIDATED CONTEXTPACK CONTENT]");
        builder.AppendLine(contextPackJson.Trim());
        builder.AppendLine();
        builder.AppendLine("[USER PROMPT]");
        builder.AppendLine(normalizedPrompt);
        return builder.ToString();
    }

    private static string BuildContextSummary(
        JsonElement contextPackRoot,
        string validationStatus,
        string selectionStrategy,
        int stateCount,
        int deltaCount,
        int anchorCount)
    {
        var selectedScope = GetOptionalString(contextPackRoot, "scope") ?? "trace-slice-validated";
        var artifactRefCount = ReadArtifactCount(contextPackRoot);
        var estimatedChars = EstimateContextChars(contextPackRoot);
        var estimatedTokens = EstimateTokens(estimatedChars);

        var builder = new StringBuilder();
        builder.AppendLine($"  selection: {selectionStrategy}");
        builder.AppendLine($"  validation: {validationStatus}");
        builder.AppendLine($"  scope: {selectedScope}");
        builder.AppendLine($"  states: {stateCount}");
        builder.AppendLine($"  deltas: {deltaCount}");
        builder.AppendLine($"  anchors: {anchorCount}");
        builder.AppendLine($"  artifact refs: {artifactRefCount}");
        builder.AppendLine($"  estimated chars: {estimatedChars}");
        builder.AppendLine($"  estimated tokens: {estimatedTokens}");
        return builder.ToString().TrimEnd();
    }

    private static int EstimateContextChars(JsonElement contextPackRoot)
    {
        return contextPackRoot.GetRawText().Length;
    }

    private static int EstimateTokens(int chars)
    {
        return (int)Math.Ceiling(chars / 4.0);
    }

    private static string BuildMaterializationPolicySummary(JsonElement materializationPolicyElement)
    {
        var parts = new List<string>();
        if (TryReadBool(materializationPolicyElement, "includeStatePayloads", out var includeStatePayloads))
        {
            parts.Add($"statePayloads={includeStatePayloads.ToString().ToLowerInvariant()}");
        }

        if (TryReadBool(materializationPolicyElement, "includeDeltaPayloads", out var includeDeltaPayloads))
        {
            parts.Add($"deltaPayloads={includeDeltaPayloads.ToString().ToLowerInvariant()}");
        }

        if (TryReadBool(materializationPolicyElement, "includeArtifactContents", out var includeArtifactContents))
        {
            parts.Add($"artifactContents={includeArtifactContents.ToString().ToLowerInvariant()}");
        }

        if (TryReadBool(materializationPolicyElement, "includeGitDiffs", out var includeGitDiffs))
        {
            parts.Add($"gitDiffs={includeGitDiffs.ToString().ToLowerInvariant()}");
        }

        if (TryReadBool(materializationPolicyElement, "includeStdoutStderr", out var includeStdoutStderr))
        {
            parts.Add($"stdoutStderr={includeStdoutStderr.ToString().ToLowerInvariant()}");
        }

        if (TryReadBool(materializationPolicyElement, "includeJsonl", out var includeJsonl))
        {
            parts.Add($"jsonl={includeJsonl.ToString().ToLowerInvariant()}");
        }

        return parts.Count == 0 ? "materialization policy unavailable" : string.Join("; ", parts);
    }

    private static int ReadArtifactCount(JsonElement contextPackRoot)
    {
        if (TryGetProperty(contextPackRoot, "artifacts", out var artifactsElement) && artifactsElement.ValueKind == JsonValueKind.Array)
        {
            return artifactsElement.GetArrayLength();
        }

        if (TryGetProperty(contextPackRoot, "changedArtifacts", out var changedArtifactsElement) && changedArtifactsElement.ValueKind == JsonValueKind.Array)
        {
            return changedArtifactsElement.GetArrayLength();
        }

        return 0;
    }

    private static IReadOnlyList<string> ReadStringArray(JsonElement root, string propertyName)
    {
        if (!TryGetProperty(root, propertyName, out var element) || element.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<string>();
        }

        var values = new List<string>();
        foreach (var item in element.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String)
            {
                var value = item.GetString();
                if (!string.IsNullOrWhiteSpace(value))
                {
                    values.Add(value);
                }
            }
        }

        return values;
    }

    private static string GetRequiredString(JsonElement root, string propertyName)
    {
        if (!TryGetProperty(root, propertyName, out var property) || property.ValueKind != JsonValueKind.String)
        {
            throw new InvalidDataException($"Missing required string property '{propertyName}'.");
        }

        return property.GetString() ?? throw new InvalidDataException($"Missing required string property '{propertyName}'.");
    }

    private static string? GetOptionalString(JsonElement root, string propertyName)
    {
        if (!TryGetProperty(root, propertyName, out var property) || property.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        return property.GetString();
    }

    private static bool TryReadBool(JsonElement root, string propertyName, out bool value)
    {
        value = default;
        if (!TryGetProperty(root, propertyName, out var property) || property.ValueKind != JsonValueKind.True && property.ValueKind != JsonValueKind.False)
        {
            return false;
        }

        value = property.GetBoolean();
        return true;
    }

    private static JsonElement GetRequiredObject(JsonElement root, string propertyName)
    {
        if (!TryGetProperty(root, propertyName, out var property) || property.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException($"Missing required object property '{propertyName}'.");
        }

        return property;
    }

    private static bool TryGetProperty(JsonElement root, string propertyName, out JsonElement property)
    {
        foreach (var candidate in root.EnumerateObject())
        {
            if (string.Equals(candidate.Name, propertyName, StringComparison.OrdinalIgnoreCase))
            {
                property = candidate.Value;
                return true;
            }
        }

        property = default;
        return false;
    }

    private static IReadOnlyList<string> MergeDistinct(IEnumerable<string>? first, IEnumerable<string>? second)
    {
        var values = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        void AddRange(IEnumerable<string>? source)
        {
            if (source is null)
            {
                return;
            }

            foreach (var item in source)
            {
                if (string.IsNullOrWhiteSpace(item) || !seen.Add(item))
                {
                    continue;
                }

                values.Add(item);
            }
        }

        AddRange(first);
        AddRange(second);
        return values;
    }
}

public sealed record RckTraceSliceProposalIntentProjection(string Kind, string Summary, string Source);

public sealed record RckTraceSliceProposalBuildResult
{
    public bool Success { get; }
    public string? ErrorMessage { get; }
    public string? ProposalJson { get; }
    public string? IntentKind { get; }
    public string? IntentSummary { get; }
    public string? ProposalSummary { get; }
    public string? ProposalSource { get; }
    public IReadOnlyList<string> Warnings { get; }

    private RckTraceSliceProposalBuildResult(
        bool success,
        string? errorMessage,
        string? proposalJson,
        string? intentKind,
        string? intentSummary,
        string? proposalSummary,
        string? proposalSource,
        IReadOnlyList<string> warnings)
    {
        Success = success;
        ErrorMessage = errorMessage;
        ProposalJson = proposalJson;
        IntentKind = intentKind;
        IntentSummary = intentSummary;
        ProposalSummary = proposalSummary;
        ProposalSource = proposalSource;
        Warnings = warnings;
    }

    public static RckTraceSliceProposalBuildResult Failure(string errorMessage)
        => new(false, errorMessage, null, null, null, null, null, Array.Empty<string>());

    public static RckTraceSliceProposalBuildResult SuccessResult(
        string proposalJson,
        string intentKind,
        string intentSummary,
        string proposalSummary,
        string proposalSource,
        IReadOnlyList<string> warnings)
        => new(true, null, proposalJson, intentKind, intentSummary, proposalSummary, proposalSource, warnings);
}

public sealed record RfsCompleteModeBuildResult
{
    public bool Success { get; }
    public string? ErrorMessage { get; }
    public string? PromptToSend { get; }
    public string? IntentKind { get; }
    public string? IntentSummary { get; }
    public string? ProposalSummary { get; }
    public string? ProposalSource { get; }
    public string? ValidationStatus { get; }
    public string? TraceSliceSelectionStrategy { get; }
    public string? ContextPackScope { get; }
    public IReadOnlyList<string> SelectedStateIds { get; }
    public IReadOnlyList<string> SelectedDeltaIds { get; }
    public IReadOnlyList<string> SelectedAnchorIds { get; }
    public int ArtifactRefCount { get; }
    public string? MaterializationPolicySummary { get; }
    public int EstimatedChars { get; }
    public int EstimatedTokens { get; }
    public bool Truncated { get; }
    public IReadOnlyList<string> Warnings { get; }
    public IReadOnlyList<string> Omissions { get; }

    private RfsCompleteModeBuildResult(
        bool success,
        string? errorMessage,
        string? promptToSend,
        string? intentKind,
        string? intentSummary,
        string? proposalSummary,
        string? proposalSource,
        string? validationStatus,
        string? traceSliceSelectionStrategy,
        string? contextPackScope,
        IReadOnlyList<string> selectedStateIds,
        IReadOnlyList<string> selectedDeltaIds,
        IReadOnlyList<string> selectedAnchorIds,
        int artifactRefCount,
        string? materializationPolicySummary,
        int estimatedChars,
        int estimatedTokens,
        bool truncated,
        IReadOnlyList<string> warnings,
        IReadOnlyList<string> omissions)
    {
        Success = success;
        ErrorMessage = errorMessage;
        PromptToSend = promptToSend;
        IntentKind = intentKind;
        IntentSummary = intentSummary;
        ProposalSummary = proposalSummary;
        ProposalSource = proposalSource;
        ValidationStatus = validationStatus;
        TraceSliceSelectionStrategy = traceSliceSelectionStrategy;
        ContextPackScope = contextPackScope;
        SelectedStateIds = selectedStateIds;
        SelectedDeltaIds = selectedDeltaIds;
        SelectedAnchorIds = selectedAnchorIds;
        ArtifactRefCount = artifactRefCount;
        MaterializationPolicySummary = materializationPolicySummary;
        EstimatedChars = estimatedChars;
        EstimatedTokens = estimatedTokens;
        Truncated = truncated;
        Warnings = warnings;
        Omissions = omissions;
    }

    public static RfsCompleteModeBuildResult Failure(string errorMessage)
        => new(
            success: false,
            errorMessage: errorMessage,
            promptToSend: null,
            intentKind: null,
            intentSummary: null,
            proposalSummary: null,
            proposalSource: null,
            validationStatus: null,
            traceSliceSelectionStrategy: null,
            contextPackScope: null,
            selectedStateIds: Array.Empty<string>(),
            selectedDeltaIds: Array.Empty<string>(),
            selectedAnchorIds: Array.Empty<string>(),
            artifactRefCount: 0,
            materializationPolicySummary: null,
            estimatedChars: 0,
            estimatedTokens: 0,
            truncated: false,
            warnings: Array.Empty<string>(),
            omissions: Array.Empty<string>());

    public static RfsCompleteModeBuildResult SuccessResult(
        string promptToSend,
        string? intentKind,
        string? intentSummary,
        string? proposalSummary,
        string? proposalSource,
        string? validationStatus,
        string? traceSliceSelectionStrategy,
        string? contextPackScope,
        IReadOnlyList<string> selectedStateIds,
        IReadOnlyList<string> selectedDeltaIds,
        IReadOnlyList<string> selectedAnchorIds,
        int artifactRefCount,
        string? materializationPolicySummary,
        int estimatedChars,
        int estimatedTokens,
        bool truncated,
        IReadOnlyList<string> warnings,
        IReadOnlyList<string> omissions)
        => new(
            success: true,
            errorMessage: null,
            promptToSend: promptToSend,
            intentKind: intentKind,
            intentSummary: intentSummary,
            proposalSummary: proposalSummary,
            proposalSource: proposalSource,
            validationStatus: validationStatus,
            traceSliceSelectionStrategy: traceSliceSelectionStrategy,
            contextPackScope: contextPackScope,
            selectedStateIds: selectedStateIds,
            selectedDeltaIds: selectedDeltaIds,
            selectedAnchorIds: selectedAnchorIds,
            artifactRefCount: artifactRefCount,
            materializationPolicySummary: materializationPolicySummary,
            estimatedChars: estimatedChars,
            estimatedTokens: estimatedTokens,
            truncated: truncated,
            warnings: warnings,
            omissions: omissions);
}
