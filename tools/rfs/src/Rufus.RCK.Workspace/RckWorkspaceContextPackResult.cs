using System.Text.Json;
using System.Text.Json.Serialization;

namespace Rufus.RCK.Workspace;

public sealed record RckWorkspaceContextPackResult
{
    private static readonly JsonSerializerOptions IndentedJsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public bool Success { get; }

    public string? ErrorMessage { get; }

    public string? RepoRoot { get; }

    public RckWorkspaceContextPackWorkspace? Workspace { get; }

    public string? HeadStateId { get; }

    public DateTimeOffset GeneratedAtUtc { get; }

    public int StateCount { get; }

    public int DeltaCount { get; }

    public int AnchorCount { get; }

    public int ActiveChainLength { get; }

    public IReadOnlyList<RckWorkspaceContextPackActiveEntry> ActiveChain { get; }

    public IReadOnlyList<RckWorkspaceContextPackStateObject> States { get; }

    public IReadOnlyList<RckWorkspaceContextPackDeltaObject> Deltas { get; }

    public IReadOnlyList<RckWorkspaceContextPackAnchorObject> Anchors { get; }

    public IReadOnlyList<string> ActiveStateIds { get; }

    public IReadOnlyList<string> ActiveDeltaIds { get; }

    public IReadOnlyList<string> OrphanStateIds { get; }

    public IReadOnlyList<string> OrphanDeltaIds { get; }

    public IReadOnlyDictionary<string, IReadOnlyList<RckWorkspaceContextPackAnchorSummary>> AnchorsByStateId { get; }

    public IReadOnlyDictionary<string, IReadOnlyList<string>> DeltasByToStateId { get; }

    public IReadOnlyDictionary<string, IReadOnlyList<string>> DeltasByFromStateId { get; }

    private RckWorkspaceContextPackResult(
        bool success,
        string? errorMessage,
        string? repoRoot,
        RckWorkspaceContextPackWorkspace? workspace,
        string? headStateId,
        DateTimeOffset generatedAtUtc,
        int stateCount,
        int deltaCount,
        int anchorCount,
        int activeChainLength,
        IReadOnlyList<RckWorkspaceContextPackActiveEntry> activeChain,
        IReadOnlyList<RckWorkspaceContextPackStateObject> states,
        IReadOnlyList<RckWorkspaceContextPackDeltaObject> deltas,
        IReadOnlyList<RckWorkspaceContextPackAnchorObject> anchors,
        IReadOnlyList<string> activeStateIds,
        IReadOnlyList<string> activeDeltaIds,
        IReadOnlyList<string> orphanStateIds,
        IReadOnlyList<string> orphanDeltaIds,
        IReadOnlyDictionary<string, IReadOnlyList<RckWorkspaceContextPackAnchorSummary>> anchorsByStateId,
        IReadOnlyDictionary<string, IReadOnlyList<string>> deltasByToStateId,
        IReadOnlyDictionary<string, IReadOnlyList<string>> deltasByFromStateId)
    {
        Success = success;
        ErrorMessage = errorMessage;
        RepoRoot = repoRoot;
        Workspace = workspace;
        HeadStateId = headStateId;
        GeneratedAtUtc = generatedAtUtc;
        StateCount = stateCount;
        DeltaCount = deltaCount;
        AnchorCount = anchorCount;
        ActiveChainLength = activeChainLength;
        ActiveChain = activeChain;
        States = states;
        Deltas = deltas;
        Anchors = anchors;
        ActiveStateIds = activeStateIds;
        ActiveDeltaIds = activeDeltaIds;
        OrphanStateIds = orphanStateIds;
        OrphanDeltaIds = orphanDeltaIds;
        AnchorsByStateId = anchorsByStateId;
        DeltasByToStateId = deltasByToStateId;
        DeltasByFromStateId = deltasByFromStateId;
    }

    public static RckWorkspaceContextPackResult Failure(string errorMessage)
        => new(
            success: false,
            errorMessage: errorMessage,
            repoRoot: null,
            workspace: null,
            headStateId: null,
            generatedAtUtc: DateTimeOffset.UtcNow,
            stateCount: 0,
            deltaCount: 0,
            anchorCount: 0,
            activeChainLength: 0,
            activeChain: Array.Empty<RckWorkspaceContextPackActiveEntry>(),
            states: Array.Empty<RckWorkspaceContextPackStateObject>(),
            deltas: Array.Empty<RckWorkspaceContextPackDeltaObject>(),
            anchors: Array.Empty<RckWorkspaceContextPackAnchorObject>(),
            activeStateIds: Array.Empty<string>(),
            activeDeltaIds: Array.Empty<string>(),
            orphanStateIds: Array.Empty<string>(),
            orphanDeltaIds: Array.Empty<string>(),
            anchorsByStateId: new SortedDictionary<string, IReadOnlyList<RckWorkspaceContextPackAnchorSummary>>(StringComparer.Ordinal),
            deltasByToStateId: new SortedDictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal),
            deltasByFromStateId: new SortedDictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal));

    public static RckWorkspaceContextPackResult Create(
        string repoRoot,
        RckWorkspaceContextPackWorkspace workspace,
        string headStateId,
        DateTimeOffset generatedAtUtc,
        IReadOnlyList<RckWorkspaceContextPackStateObject> states,
        IReadOnlyList<RckWorkspaceContextPackDeltaObject> deltas,
        IReadOnlyList<RckWorkspaceContextPackAnchorObject> anchors,
        IReadOnlyList<RckWorkspaceContextPackActiveEntry> activeChain,
        IReadOnlyList<string> activeStateIds,
        IReadOnlyList<string> activeDeltaIds,
        IReadOnlyList<string> orphanStateIds,
        IReadOnlyList<string> orphanDeltaIds,
        IReadOnlyDictionary<string, IReadOnlyList<RckWorkspaceContextPackAnchorSummary>> anchorsByStateId,
        IReadOnlyDictionary<string, IReadOnlyList<string>> deltasByToStateId,
        IReadOnlyDictionary<string, IReadOnlyList<string>> deltasByFromStateId)
        => new(
            success: true,
            errorMessage: null,
            repoRoot: repoRoot,
            workspace: workspace,
            headStateId: headStateId,
            generatedAtUtc: generatedAtUtc,
            stateCount: states.Count,
            deltaCount: deltas.Count,
            anchorCount: anchors.Count,
            activeChainLength: activeChain.Count,
            activeChain: activeChain,
            states: states,
            deltas: deltas,
            anchors: anchors,
            activeStateIds: activeStateIds,
            activeDeltaIds: activeDeltaIds,
            orphanStateIds: orphanStateIds,
            orphanDeltaIds: orphanDeltaIds,
            anchorsByStateId: anchorsByStateId,
            deltasByToStateId: deltasByToStateId,
            deltasByFromStateId: deltasByFromStateId);

    public IEnumerable<string> FormatMarkdownLines()
    {
        if (!Success)
        {
            if (!string.IsNullOrWhiteSpace(ErrorMessage))
            {
                yield return ErrorMessage;
            }

            yield break;
        }

        yield return "# RCK DAG Context Pack v1";
        yield return string.Empty;

        yield return "## Purpose";
        yield return "";
        yield return "This document contains a complete RCK DAG export for a repository workspace.";
        yield return "It is intended to be pasted into an LLM so the LLM can reconstruct and reason about the cognitive trace.";
        yield return string.Empty;

        yield return "## Interpretation rules for the LLM";
        foreach (var line in new[]
                 {
                     "- RCK is a cognitive trace DAG.",
                     "- State = cognitive snapshot.",
                     "- Delta = transition between two States.",
                     "- Anchor = semantic/material milestone pointing to a State.",
                     "- HEAD points to the current active State.",
                     "- The active chain is recovered by starting from HEAD and following Deltas backwards from `toStateId` to `fromStateId`.",
                     "- Not every object is necessarily on the active chain.",
                     "- Orphan States/Deltas/Anchors may exist during tests.",
                     "- Git stores actual file contents and diffs.",
                     "- RCK only stores cognitive metadata, prompts, answers, tools, artifact paths and Git context.",
                     "- Artifact paths are references, not file contents.",
                     "- Do not assume file contents unless provided separately.",
                 })
        {
            yield return line;
        }

        yield return string.Empty;

        yield return "## Schema";
        yield return "```json";
        foreach (var line in SerializeIndentedJson(new
                 {
                     schemaVersion = "number",
                     type = "string (rck-dag-context-pack-v1)",
                     generatedAtUtc = "string (UTC ISO-8601)",
                     workspace = new
                     {
                         root = "string",
                         git = new
                         {
                             branch = "string|null",
                             commit = "string|null",
                             dirty = "boolean",
                         },
                     },
                     headStateId = "string",
                     counts = new
                     {
                         states = "number",
                         deltas = "number",
                         anchors = "number",
                         activeChainLength = "number",
                     },
                     activeChain = "array<RckWorkspaceContextPackActiveEntry>",
                     objects = new
                     {
                         states = "array<RckWorkspaceContextPackStateObject>",
                         deltas = "array<RckWorkspaceContextPackDeltaObject>",
                         anchors = "array<RckWorkspaceContextPackAnchorObject>",
                     },
                     derivedRelationships = new
                     {
                         activeStateIds = "array<string>",
                         activeDeltaIds = "array<string>",
                         orphanStateIds = "array<string>",
                         orphanDeltaIds = "array<string>",
                         anchorsByStateId = "record<string, array<RckWorkspaceContextPackAnchorSummary>>",
                         deltasByToStateId = "record<string, array<string>>",
                         deltasByFromStateId = "record<string, array<string>>",
                     },
                     stateObject = new
                     {
                         id = "string",
                         payloadCanonicalJson = "string",
                         payloadDecoded = new
                         {
                             type = "string",
                             schemaVersion = "number",
                             interaction = new
                             {
                                 mode = "string|null",
                                 prompt = "string|null",
                                 answerSummary = "string|null",
                             },
                             git = new
                             {
                                 branch = "string|null",
                                 commit = "string|null",
                                 dirty = "boolean",
                             },
                             artifacts = "array<object>",
                         },
                         refs = "array<RckWorkspaceContextPackRefObject>",
                         meta = "RckWorkspaceContextPackMeta",
                     },
                     deltaObject = new
                     {
                         id = "string",
                         fromStateId = "string",
                         toStateId = "string",
                         ops = new[]
                         {
                             new
                             {
                                 kind = "string",
                                 path = "string",
                                 valueJson = "string|null",
                                 decodedValueJson = "object|null",
                             },
                         },
                         refs = "array<RckWorkspaceContextPackRefObject>",
                         evidenceRefs = "array<RckWorkspaceContextPackEvidenceRefObject>",
                         meta = "RckWorkspaceContextPackMeta",
                     },
                     anchorObject = new
                     {
                         id = "string",
                         stateId = "string",
                         parentAnchorIds = "array<string>",
                         meta = "RckWorkspaceContextPackMeta",
                     },
                 }))
        {
            yield return line;
        }
        yield return "```";
        yield return string.Empty;

        yield return "## DAG metadata";
        yield return "```json";
        foreach (var line in SerializeIndentedJson(new
                 {
                     schemaVersion = 1,
                     type = "rck-dag-context-pack-v1",
                     generatedAtUtc = GeneratedAtUtc,
                     workspace = new
                     {
                         root = Workspace?.Root,
                         git = new
                         {
                             branch = Workspace?.GitBranch,
                             commit = Workspace?.GitCommit,
                             dirty = Workspace?.GitDirty ?? false,
                         },
                     },
                     headStateId = HeadStateId,
                     counts = new
                     {
                         states = StateCount,
                         deltas = DeltaCount,
                         anchors = AnchorCount,
                         activeChainLength = ActiveChainLength,
                     },
                 }))
        {
            yield return line;
        }
        yield return "```";
        yield return string.Empty;

        yield return "## Active chain";
        yield return "Order: `HEAD → genesis` (walked backward from the current head state).";
        yield return "```json";
        foreach (var line in SerializeIndentedJson(ActiveChain.Select(entry => new
                 {
                     stateId = entry.StateId,
                     incomingDeltaId = entry.IncomingDeltaId,
                     anchors = entry.Anchors,
                     mode = entry.Mode,
                     prompt = entry.Prompt,
                     promptIsExcerpt = entry.PromptIsExcerpt,
                     answerSummary = entry.AnswerSummary,
                     git = new
                     {
                         branch = entry.GitBranch,
                         commit = entry.GitCommit,
                         dirty = entry.GitDirty,
                     },
                     artifacts = entry.Artifacts,
                 }).ToArray()))
        {
            yield return line;
        }
        yield return "```";
        yield return string.Empty;

        yield return "## Derived relationships";
        yield return "```json";
        foreach (var line in SerializeIndentedJson(new
                 {
                     headStateId = HeadStateId,
                     activeStateIds = ActiveStateIds,
                     activeDeltaIds = ActiveDeltaIds,
                     orphanStateIds = OrphanStateIds,
                     orphanDeltaIds = OrphanDeltaIds,
                     anchorsByStateId = AnchorsByStateId,
                     deltasByToStateId = DeltasByToStateId,
                     deltasByFromStateId = DeltasByFromStateId,
                 }))
        {
            yield return line;
        }
        yield return "```";
        yield return string.Empty;

        yield return "## Full objects";
        yield return string.Empty;

        yield return "### States";
        foreach (var state in States)
        {
            yield return $"#### State `{state.Id}`";
            yield return "```json";
            foreach (var line in SerializeIndentedJson(new
                     {
                         id = state.Id,
                         payloadCanonicalJson = state.PayloadCanonicalJson,
                         payloadDecoded = state.PayloadDecoded,
                         refs = state.Refs,
                         meta = state.Meta,
                     }))
            {
                yield return line;
            }
            yield return "```";
            yield return string.Empty;
        }

        yield return "### Deltas";
        foreach (var delta in Deltas)
        {
            yield return $"#### Delta `{delta.Id}`";
            yield return "```json";
            foreach (var line in SerializeIndentedJson(new
                     {
                         id = delta.Id,
                         fromStateId = delta.FromStateId,
                         toStateId = delta.ToStateId,
                         ops = delta.Ops,
                         refs = delta.Refs,
                         evidenceRefs = delta.EvidenceRefs,
                         meta = delta.Meta,
                     }))
            {
                yield return line;
            }
            yield return "```";
            yield return string.Empty;
        }

        yield return "### Anchors";
        foreach (var anchor in Anchors)
        {
            yield return $"#### Anchor `{anchor.Id}`";
            yield return "```json";
            foreach (var line in SerializeIndentedJson(new
                     {
                         id = anchor.Id,
                         stateId = anchor.StateId,
                         parentAnchorIds = anchor.ParentAnchorIds,
                         meta = anchor.Meta,
                     }))
            {
                yield return line;
            }
            yield return "```";
            yield return string.Empty;
        }

        yield return "## Notes / limitations";
        foreach (var line in new[]
                 {
                     "- This is a full DAG export, not a repository content export.",
                     "- It may be large.",
                     "- It does not include file contents.",
                     "- It does not include git diffs.",
                     "- It does not include artifact hashes yet.",
                     "- `RckRef` / `EvidenceRef` may be empty in the current implementation.",
                     "- The LLM should use this as trace context, not as source code evidence.",
                 })
        {
            yield return line;
        }
    }

    private static string NormalizePreview(string? value, out bool isExcerpt)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            isExcerpt = false;
            return string.Empty;
        }

        var normalized = string.Join(" ", value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        const int maxLength = 360;
        if (normalized.Length <= maxLength)
        {
            isExcerpt = false;
            return normalized;
        }

        isExcerpt = true;
        return normalized[..(maxLength - 1)] + "…";
    }

    private static IEnumerable<string> SerializeIndentedJson(object value)
    {
        var json = JsonSerializer.Serialize(value, IndentedJsonOptions);
        return json.Split('\n');
    }
}

public sealed record RckWorkspaceContextPackWorkspace(string Root, string? GitBranch, string? GitCommit, bool GitDirty);

public sealed record RckWorkspaceContextPackMeta(DateTimeOffset CreatedAtUtc, string? CreatedBy, string? Label, string? Reason);

public sealed record RckWorkspaceContextPackRefObject(
    string Id,
    string Kind,
    string Uri,
    string? Hash,
    string? MediaType,
    RckWorkspaceContextPackMeta? Meta);

public sealed record RckWorkspaceContextPackEvidenceRefObject(
    string Id,
    string Kind,
    RckWorkspaceContextPackRefObject Ref,
    string? Summary,
    string? Hash);

public sealed record RckWorkspaceContextPackStateObject(
    string Id,
    string PayloadCanonicalJson,
    JsonElement? PayloadDecoded,
    IReadOnlyList<RckWorkspaceContextPackRefObject> Refs,
    RckWorkspaceContextPackMeta Meta);

public sealed record RckWorkspaceContextPackDeltaOpObject(
    string Kind,
    string Path,
    string? ValueJson,
    JsonElement? DecodedValueJson);

public sealed record RckWorkspaceContextPackDeltaObject(
    string Id,
    string FromStateId,
    string ToStateId,
    IReadOnlyList<RckWorkspaceContextPackDeltaOpObject> Ops,
    IReadOnlyList<RckWorkspaceContextPackRefObject> Refs,
    IReadOnlyList<RckWorkspaceContextPackEvidenceRefObject> EvidenceRefs,
    RckWorkspaceContextPackMeta Meta);

public sealed record RckWorkspaceContextPackAnchorObject(
    string Id,
    string StateId,
    IReadOnlyList<string> ParentAnchorIds,
    RckWorkspaceContextPackMeta Meta);

public sealed record RckWorkspaceContextPackAnchorSummary(string Id, string? Label);

public sealed record RckWorkspaceContextPackActiveEntry(
    string StateId,
    string? IncomingDeltaId,
    IReadOnlyList<RckWorkspaceContextPackAnchorSummary> Anchors,
    string Mode,
    string? Prompt,
    bool PromptIsExcerpt,
    string? AnswerSummary,
    string? GitBranch,
    string? GitCommit,
    bool GitDirty,
    IReadOnlyList<GitWorkspaceArtifactChange> Artifacts);
