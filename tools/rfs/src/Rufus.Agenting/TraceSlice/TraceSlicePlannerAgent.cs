using System.Text.Json;

namespace Rufus.Agenting.TraceSlice;

public sealed class TraceSlicePlannerAgent : IAgent
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public string Id => "trace-slice-planner";

    public AgentDescriptor Descriptor { get; }

    public TraceSlicePlannerAgent()
    {
        Descriptor = new AgentDescriptor(
            id: Id,
            name: "TraceSlice Planner Agent",
            role: "Builds a deterministic, anchor-aware TraceSliceProposal from controlled prompt/intent/DAG input.",
            executionModel: new AgentExecutionModel("mock", "deterministic-v1"),
            capabilities: new[] { "propose-trace-slice" });
    }

    public Task<AgentTaskResult> ExecuteAsync(AgentTask task, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(task);
        cancellationToken.ThrowIfCancellationRequested();

        if (!string.Equals(task.Kind, "propose-trace-slice", StringComparison.Ordinal))
        {
            return Task.FromResult(CreateFailure(task, $"TraceSlicePlannerAgent only accepts tasks with Kind='propose-trace-slice', received '{task.Kind}'."));
        }

        if (string.IsNullOrWhiteSpace(task.Input))
        {
            return Task.FromResult(CreateFailure(task, "TraceSlicePlannerAgent requires a controlled JSON task input."));
        }

        try
        {
            using var document = JsonDocument.Parse(task.Input);
            var root = document.RootElement;

            var prompt = GetRequiredString(root, "prompt");
            var intentElement = GetRequiredObject(root, "intent");
            var dagQuickIndexElement = GetRequiredObject(root, "dagQuickIndex");

            var intent = new TraceSliceProposalIntent(
                Kind: GetRequiredString(intentElement, "kind"),
                Summary: GetRequiredString(intentElement, "summary"),
                Source: GetRequiredString(intentElement, "source"));

            var headStateId = GetRequiredString(dagQuickIndexElement, "headStateId");
            var stateIds = ReadStringArray(dagQuickIndexElement, "recentStateIds")
                .Take(5)
                .ToArray();
            var deltaIds = ReadStringArray(dagQuickIndexElement, "recentDeltaIds")
                .Take(5)
                .ToArray();
            var selectedStateIds = stateIds.ToHashSet(StringComparer.Ordinal);
            var anchorIds = ReadAnchors(dagQuickIndexElement, selectedStateIds)
                .Select(anchor => anchor.Id)
                .Distinct(StringComparer.Ordinal)
                .ToArray();

            var rationale = new List<TraceSliceProposalRationale>();
            rationale.AddRange(stateIds.Select(stateId => new TraceSliceProposalRationale(
                Target: $"state:{stateId}",
                Reason: "Selected from active-chain-recent deterministic baseline.")));
            rationale.AddRange(deltaIds.Select(deltaId => new TraceSliceProposalRationale(
                Target: $"delta:{deltaId}",
                Reason: "Connects selected active-chain states in deterministic baseline.")));
            rationale.AddRange(anchorIds.Select(anchorId => new TraceSliceProposalRationale(
                Target: $"anchor:{anchorId}",
                Reason: "Cognitive milestone available in current RCK DAG.")));

            var warnings = new List<string>();
            if (stateIds.Length == 0)
            {
                warnings.Add("No recent active-chain states were available in the DAG quick index.");
            }

            var proposal = new TraceSliceProposal(
                Type: "rufus.trace-slice-proposal",
                SchemaVersion: 1,
                Prompt: new TraceSliceProposalPrompt(
                    Text: prompt,
                    IsExcerpt: false),
                Intent: intent,
                RequestedSelection: new TraceSliceProposalSelection(
                    StateIds: stateIds,
                    DeltaIds: deltaIds,
                    AnchorIds: anchorIds,
                    ArtifactRefs: Array.Empty<string>()),
                RequestedMaterializationPolicy: new TraceSliceProposalMaterializationPolicy(
                    IncludeStatePayloads: true,
                    IncludeDeltaDecodedOps: true,
                    IncludeArtifactContents: false,
                    IncludeGitDiffs: false,
                    IncludeStdoutStderr: false,
                    IncludeJsonl: false),
                Rationale: rationale,
                Confidence: 1.0,
                Warnings: warnings);

            var output = JsonSerializer.Serialize(proposal, JsonOptions);
            var summary = $"Deterministic trace slice proposal selected {stateIds.Length} state(s), {deltaIds.Length} delta(s), and {anchorIds.Length} anchor(s).";
            var evidence = new[]
            {
                new AgentEvidence("input", "task.input", "prompt + intent + dagQuickIndex"),
                new AgentEvidence("head-state", "dagQuickIndex.headStateId", headStateId),
                new AgentEvidence("selection", "active-chain-recent", $"states={stateIds.Length}; deltas={deltaIds.Length}; anchors={anchorIds.Length}"),
            };

            return Task.FromResult(new AgentTaskResult(
                task.Id,
                AgentTaskStatus.Succeeded,
                Id,
                Descriptor.ExecutionModel,
                output: output,
                summary: summary,
                evidence: evidence,
                warnings: warnings));
        }
        catch (Exception ex)
        {
            return Task.FromResult(CreateFailure(task, ex.Message));
        }
    }

    private static JsonElement GetRequiredObject(JsonElement element, string propertyName)
    {
        if (element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.Object)
        {
            return property;
        }

        throw new InvalidDataException($"Missing object property '{propertyName}'.");
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

    private static IReadOnlyList<string> ReadStringArray(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<string>();
        }

        var values = new List<string>();
        foreach (var item in property.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String)
            {
                continue;
            }

            var value = item.GetString();
            if (!string.IsNullOrWhiteSpace(value))
            {
                values.Add(value);
            }
        }

        return values;
    }

    private static IReadOnlyList<PlannerAnchorCandidate> ReadAnchors(JsonElement dagQuickIndexElement, IReadOnlySet<string> selectedStateIds)
    {
        if (!dagQuickIndexElement.TryGetProperty("anchors", out var property) || property.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<PlannerAnchorCandidate>();
        }

        var anchors = new List<PlannerAnchorCandidate>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in property.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var id = GetRequiredString(item, "id");
            if (!seen.Add(id))
            {
                continue;
            }

            var stateId = GetRequiredString(item, "stateId");
            var isRecentChain = ReadOptionalBoolean(item, "isRecentChain") || selectedStateIds.Contains(stateId);
            if (!isRecentChain)
            {
                continue;
            }

            anchors.Add(new PlannerAnchorCandidate(id, stateId));
        }

        return anchors;
    }

    private static bool ReadOptionalBoolean(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property)
            || (property.ValueKind != JsonValueKind.True && property.ValueKind != JsonValueKind.False))
        {
            return false;
        }

        return property.GetBoolean();
    }

    private AgentTaskResult CreateFailure(AgentTask task, string errorMessage)
    {
        var evidence = new[]
        {
            new AgentEvidence("input", "task.input", task.Input ?? task.Goal),
            new AgentEvidence("agent", Id, Descriptor.Name),
            new AgentEvidence("execution-model", "mock/deterministic-v1", "provider=mock; model=deterministic-v1"),
        };

        return new AgentTaskResult(
            task.Id,
            AgentTaskStatus.Failed,
            Id,
            Descriptor.ExecutionModel,
            summary: "TraceSlice proposal planning failed.",
            evidence: evidence,
            errors: new[] { errorMessage });
    }

    private sealed record PlannerAnchorCandidate(string Id, string StateId);
}
