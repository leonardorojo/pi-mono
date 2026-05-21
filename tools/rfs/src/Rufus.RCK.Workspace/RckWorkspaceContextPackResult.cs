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

    public string? WorkspaceName { get; }

    public RckWorkspaceContextPackWorkspace? Workspace { get; }

    public string? HeadStateId { get; }

    public string? HeadShortId { get; }

    public DateTimeOffset GeneratedAtUtc { get; }

    public int StateCount { get; }

    public int DeltaCount { get; }

    public int AnchorCount { get; }

    public int ActiveChainLength { get; }

    public int OrphanStateCount { get; }

    public int OrphanDeltaCount { get; }

    public IReadOnlyList<RckWorkspaceContextPackActiveEntry> ActiveChain { get; }

    public IReadOnlyList<RckWorkspaceContextPackStateObject> States { get; }

    public IReadOnlyList<RckWorkspaceContextPackDeltaObject> Deltas { get; }

    public IReadOnlyList<RckWorkspaceContextPackAnchorObject> Anchors { get; }

    public IReadOnlyList<string> ActiveStateIds { get; }

    public IReadOnlyList<string> ActiveDeltaIds { get; }

    public IReadOnlyList<string> OrphanStateIds { get; }

    public IReadOnlyList<string> OrphanDeltaIds { get; }

    public IReadOnlyList<GitWorkspaceArtifactChange> ChangedArtifacts { get; }

    public IReadOnlyDictionary<string, IReadOnlyList<RckWorkspaceContextPackAnchorSummary>> AnchorsByStateId { get; }

    public IReadOnlyDictionary<string, IReadOnlyList<string>> DeltasByToStateId { get; }

    public IReadOnlyDictionary<string, IReadOnlyList<string>> DeltasByFromStateId { get; }

    private RckWorkspaceContextPackResult(
        bool success,
        string? errorMessage,
        string? repoRoot,
        string? workspaceName,
        RckWorkspaceContextPackWorkspace? workspace,
        string? headStateId,
        string? headShortId,
        DateTimeOffset generatedAtUtc,
        int stateCount,
        int deltaCount,
        int anchorCount,
        int activeChainLength,
        int orphanStateCount,
        int orphanDeltaCount,
        IReadOnlyList<RckWorkspaceContextPackActiveEntry> activeChain,
        IReadOnlyList<RckWorkspaceContextPackStateObject> states,
        IReadOnlyList<RckWorkspaceContextPackDeltaObject> deltas,
        IReadOnlyList<RckWorkspaceContextPackAnchorObject> anchors,
        IReadOnlyList<string> activeStateIds,
        IReadOnlyList<string> activeDeltaIds,
        IReadOnlyList<string> orphanStateIds,
        IReadOnlyList<string> orphanDeltaIds,
        IReadOnlyList<GitWorkspaceArtifactChange> changedArtifacts,
        IReadOnlyDictionary<string, IReadOnlyList<RckWorkspaceContextPackAnchorSummary>> anchorsByStateId,
        IReadOnlyDictionary<string, IReadOnlyList<string>> deltasByToStateId,
        IReadOnlyDictionary<string, IReadOnlyList<string>> deltasByFromStateId)
    {
        Success = success;
        ErrorMessage = errorMessage;
        RepoRoot = repoRoot;
        WorkspaceName = workspaceName;
        Workspace = workspace;
        HeadStateId = headStateId;
        HeadShortId = headShortId;
        GeneratedAtUtc = generatedAtUtc;
        StateCount = stateCount;
        DeltaCount = deltaCount;
        AnchorCount = anchorCount;
        ActiveChainLength = activeChainLength;
        OrphanStateCount = orphanStateCount;
        OrphanDeltaCount = orphanDeltaCount;
        ActiveChain = activeChain;
        States = states;
        Deltas = deltas;
        Anchors = anchors;
        ActiveStateIds = activeStateIds;
        ActiveDeltaIds = activeDeltaIds;
        OrphanStateIds = orphanStateIds;
        OrphanDeltaIds = orphanDeltaIds;
        ChangedArtifacts = changedArtifacts;
        AnchorsByStateId = anchorsByStateId;
        DeltasByToStateId = deltasByToStateId;
        DeltasByFromStateId = deltasByFromStateId;
    }

    public static RckWorkspaceContextPackResult Failure(string errorMessage)
        => new(
            success: false,
            errorMessage: errorMessage,
            repoRoot: null,
            workspaceName: null,
            workspace: null,
            headStateId: null,
            headShortId: null,
            generatedAtUtc: DateTimeOffset.UtcNow,
            stateCount: 0,
            deltaCount: 0,
            anchorCount: 0,
            activeChainLength: 0,
            orphanStateCount: 0,
            orphanDeltaCount: 0,
            activeChain: Array.Empty<RckWorkspaceContextPackActiveEntry>(),
            states: Array.Empty<RckWorkspaceContextPackStateObject>(),
            deltas: Array.Empty<RckWorkspaceContextPackDeltaObject>(),
            anchors: Array.Empty<RckWorkspaceContextPackAnchorObject>(),
            activeStateIds: Array.Empty<string>(),
            activeDeltaIds: Array.Empty<string>(),
            orphanStateIds: Array.Empty<string>(),
            orphanDeltaIds: Array.Empty<string>(),
            changedArtifacts: Array.Empty<GitWorkspaceArtifactChange>(),
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
        IReadOnlyDictionary<string, IReadOnlyList<string>> deltasByFromStateId,
        IReadOnlyList<GitWorkspaceArtifactChange> changedArtifacts)
        => new(
            success: true,
            errorMessage: null,
            repoRoot: repoRoot,
            workspaceName: GetWorkspaceName(repoRoot),
            workspace: workspace,
            headStateId: headStateId,
            headShortId: GetShortId(headStateId),
            generatedAtUtc: generatedAtUtc,
            stateCount: states.Count,
            deltaCount: deltas.Count,
            anchorCount: anchors.Count,
            activeChainLength: activeChain.Count,
            orphanStateCount: orphanStateIds.Count,
            orphanDeltaCount: orphanDeltaIds.Count,
            activeChain: activeChain,
            states: states,
            deltas: deltas,
            anchors: anchors,
            activeStateIds: activeStateIds,
            activeDeltaIds: activeDeltaIds,
            orphanStateIds: orphanStateIds,
            orphanDeltaIds: orphanDeltaIds,
            changedArtifacts: changedArtifacts,
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

        yield return "## Quick index";
        foreach (var line in BuildQuickIndexLines())
        {
            yield return line;
        }
        yield return string.Empty;

        yield return "## Purpose";
        yield return string.Empty;
        yield return "This document contains a complete RCK DAG export for a repository workspace.";
        yield return "It is intended to be pasted into an LLM so the LLM can reconstruct and reason about the cognitive trace.";
        yield return string.Empty;

        yield return "## Interpretation rules for the LLM";
        foreach (var line in BuildInterpretationRules())
        {
            yield return line;
        }
        yield return string.Empty;

        yield return "## JSON Schema";
        yield return "```json";
        foreach (var line in SerializeIndentedJson(BuildContextPackSchema()))
        {
            yield return line;
        }
        yield return "```";
        yield return string.Empty;

        yield return "## DAG metadata";
        yield return "```json";
        foreach (var line in SerializeIndentedJson(BuildDagMetadata()))
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
                     gitContext = new
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

    private IEnumerable<string> BuildQuickIndexLines()
    {
        yield return $"- Workspace: `{WorkspaceName ?? "(unknown)"}`";
        yield return $"  - Root: `{RepoRoot ?? "(unknown)"}`";
        yield return $"  - Branch: `{Workspace?.GitBranch ?? "(detached)"}`";
        yield return $"  - Commit: `{Workspace?.GitCommit ?? "(unknown)"}`";
        yield return $"  - Dirty: `{Workspace?.GitDirty ?? false}`";
        yield return $"  - HEAD state: `{HeadStateId ?? "(unknown)"}`";
        yield return $"  - HEAD short id: `{HeadShortId ?? "(unknown)"}`";
        yield return $"  - Counts: states {StateCount}, deltas {DeltaCount}, anchors {AnchorCount}, active chain {ActiveChainLength}, orphan states {OrphanStateCount}, orphan deltas {OrphanDeltaCount}";

        yield return $"  - Current changed artifacts ({ChangedArtifacts.Count}):";
        if (ChangedArtifacts.Count == 0)
        {
            yield return "    - (none)";
        }
        else
        {
            foreach (var artifact in ChangedArtifacts.Take(8))
            {
                yield return $"    - {FormatArtifactSummary(artifact)}";
            }

            if (ChangedArtifacts.Count > 8)
            {
                yield return $"    - … {ChangedArtifacts.Count - 8} more";
            }
        }

        var mainAnchors = ActiveChain.SelectMany(entry => entry.Anchors).ToArray();
        yield return $"  - Main anchors ({mainAnchors.Length}):";
        if (mainAnchors.Length == 0)
        {
            yield return "    - (none)";
        }
        else
        {
            foreach (var anchor in mainAnchors.Take(8))
            {
                var label = string.IsNullOrWhiteSpace(anchor.Label) ? anchor.Id : $"{anchor.Id} — {anchor.Label}";
                yield return $"    - {label}";
            }

            if (mainAnchors.Length > 8)
            {
                yield return $"    - … {mainAnchors.Length - 8} more";
            }
        }
    }

    private static IEnumerable<string> BuildInterpretationRules()
    {
        return new[]
        {
            "- RCK is a cognitive trace DAG.",
            "- State = cognitive snapshot.",
            "- Delta = transition between two States.",
            "- Anchor = semantic/material milestone pointing to a State.",
            "- HEAD points to the current active State.",
            "- The active chain is recovered by starting from HEAD and following Deltas backwards from `toStateId` to `fromStateId`.",
            "- Not every object is necessarily on the active chain.",
            "- Orphan States/Deltas/Anchors may exist during tests.",
            "- `workspace.changedArtifacts` is derived from Git status and excludes `.rfs/`, `bin/`, and `obj/`.",
            "- `.rfs/` is internal RCK workspace metadata; if it appears in tool observations or agent answers, do not treat it as a user artifact or a functional repo change.",
            "- Git stores actual file contents and diffs.",
            "- RCK only stores cognitive metadata, prompts, answers, tools, artifact paths, and Git context.",
            "- Artifact paths are references, not file contents.",
            "- Agent answers are recorded observations, not direct source-code evidence.",
            "- `decodedValueJson` is a direct parse of each delta op `valueJson`; if the decoded object contains `change`, `cause`, and `evidence`, treat them as siblings unless the raw JSON shows a different nesting.",
            "- Do not assume file contents unless provided separately.",
        };
    }

    private static object BuildContextPackSchema()
    {
        static Dictionary<string, object?> Dict(params (string Key, object? Value)[] entries)
        {
            var result = new Dictionary<string, object?>(StringComparer.Ordinal);
            foreach (var entry in entries)
            {
                result[entry.Key] = entry.Value;
            }

            return result;
        }

        static object Ref(string path) => Dict(("$ref", path));

        var jsonValueTypes = new[] { "object", "array", "string", "number", "boolean", "null" };
        var stringOrNull = new[] { "string", "null" };

        return Dict(
            ("$schema", "https://json-schema.org/draft/2020-12/schema"),
            ("$id", "urn:rfs:rck-dag-context-pack:v1"),
            ("title", "RCK DAG Context Pack v1"),
            ("type", "object"),
            ("additionalProperties", false),
            ("required", new[]
            {
                "schemaVersion",
                "type",
                "generatedAtUtc",
                "workspace",
                "headStateId",
                "headShortId",
                "counts",
                "activeChain",
                "states",
                "deltas",
                "anchors",
                "derivedRelationships",
            }),
            ("properties", Dict(
                ("schemaVersion", Dict(("type", "integer"), ("const", 1))),
                ("type", Dict(("type", "string"), ("const", "rck-dag-context-pack-v1"))),
                ("generatedAtUtc", Dict(("type", "string"), ("format", "date-time"))),
                ("workspace", Ref("#/$defs/workspace")),
                ("headStateId", Dict(("type", "string"))),
                ("headShortId", Dict(("type", "string"))),
                ("counts", Ref("#/$defs/counts")),
                ("activeChain", Dict(("type", "array"), ("items", Ref("#/$defs/activeEntry")))),
                ("states", Dict(("type", "array"), ("items", Ref("#/$defs/stateObject")))),
                ("deltas", Dict(("type", "array"), ("items", Ref("#/$defs/deltaObject")))),
                ("anchors", Dict(("type", "array"), ("items", Ref("#/$defs/anchorObject")))),
                ("derivedRelationships", Ref("#/$defs/derivedRelationships"))
            )),
            ("$defs", Dict(
                ("workspace", Dict(
                    ("type", "object"),
                    ("additionalProperties", false),
                    ("required", new[] { "name", "root", "gitContext", "changedArtifacts" }),
                    ("properties", Dict(
                        ("name", Dict(("type", "string"))),
                        ("root", Dict(("type", "string"))),
                        ("gitContext", Ref("#/$defs/gitContext")),
                        ("changedArtifacts", Dict(("type", "array"), ("items", Ref("#/$defs/artifact"))))
                    ))
                )),
                ("gitContext", Dict(
                    ("type", "object"),
                    ("additionalProperties", false),
                    ("required", new[] { "branch", "commit", "dirty" }),
                    ("properties", Dict(
                        ("branch", Dict(("type", stringOrNull))),
                        ("commit", Dict(("type", stringOrNull))),
                        ("dirty", Dict(("type", "boolean")))
                    ))
                )),
                ("counts", Dict(
                    ("type", "object"),
                    ("additionalProperties", false),
                    ("required", new[] { "states", "deltas", "anchors", "activeChainLength", "orphanStateCount", "orphanDeltaCount" }),
                    ("properties", Dict(
                        ("states", Dict(("type", "integer"), ("minimum", 0))),
                        ("deltas", Dict(("type", "integer"), ("minimum", 0))),
                        ("anchors", Dict(("type", "integer"), ("minimum", 0))),
                        ("activeChainLength", Dict(("type", "integer"), ("minimum", 0))),
                        ("orphanStateCount", Dict(("type", "integer"), ("minimum", 0))),
                        ("orphanDeltaCount", Dict(("type", "integer"), ("minimum", 0)))
                    ))
                )),
                ("activeEntry", Dict(
                    ("type", "object"),
                    ("additionalProperties", false),
                    ("required", new[] { "stateId", "anchors", "mode", "promptIsExcerpt", "gitContext", "artifacts" }),
                    ("properties", Dict(
                        ("stateId", Dict(("type", "string"))),
                        ("incomingDeltaId", Dict(("type", stringOrNull))),
                        ("anchors", Dict(("type", "array"), ("items", Ref("#/$defs/anchorSummary")))),
                        ("mode", Dict(("type", "string"))),
                        ("prompt", Dict(("type", stringOrNull))),
                        ("promptIsExcerpt", Dict(("type", "boolean"))),
                        ("answerSummary", Dict(("type", stringOrNull))),
                        ("gitContext", Ref("#/$defs/gitContext")),
                        ("artifacts", Dict(("type", "array"), ("items", Ref("#/$defs/artifact"))))
                    ))
                )),
                ("stateObject", Dict(
                    ("type", "object"),
                    ("additionalProperties", false),
                    ("required", new[] { "id", "payloadCanonicalJson", "payloadDecoded", "refs", "meta" }),
                    ("properties", Dict(
                        ("id", Dict(("type", "string"))),
                        ("payloadCanonicalJson", Dict(("type", "string"))),
                        ("payloadDecoded", Dict(
                            ("description", "Parsed JSON payload for the state; shape varies by state type."),
                            ("type", jsonValueTypes)
                        )),
                        ("refs", Dict(("type", "array"), ("items", Ref("#/$defs/refObject")))),
                        ("meta", Ref("#/$defs/meta"))
                    ))
                )),
                ("deltaObject", Dict(
                    ("type", "object"),
                    ("additionalProperties", false),
                    ("required", new[] { "id", "fromStateId", "toStateId", "ops", "refs", "evidenceRefs", "meta" }),
                    ("properties", Dict(
                        ("id", Dict(("type", "string"))),
                        ("fromStateId", Dict(("type", "string"))),
                        ("toStateId", Dict(("type", "string"))),
                        ("ops", Dict(("type", "array"), ("items", Ref("#/$defs/deltaOpObject")))),
                        ("refs", Dict(("type", "array"), ("items", Ref("#/$defs/refObject")))),
                        ("evidenceRefs", Dict(("type", "array"), ("items", Ref("#/$defs/evidenceRefObject")))),
                        ("meta", Ref("#/$defs/meta"))
                    ))
                )),
                ("deltaOpObject", Dict(
                    ("type", "object"),
                    ("additionalProperties", false),
                    ("required", new[] { "kind", "path" }),
                    ("properties", Dict(
                        ("kind", Dict(("type", "string"))),
                        ("path", Dict(("type", "string"))),
                        ("valueJson", Dict(("type", stringOrNull))),
                        ("decodedValueJson", Dict(
                            ("description", "Parsed JSON payload for the delta op valueJson; preserve the raw object shape when present."),
                            ("type", jsonValueTypes)
                        ))
                    ))
                )),
                ("anchorObject", Dict(
                    ("type", "object"),
                    ("additionalProperties", false),
                    ("required", new[] { "id", "stateId", "parentAnchorIds", "meta" }),
                    ("properties", Dict(
                        ("id", Dict(("type", "string"))),
                        ("stateId", Dict(("type", "string"))),
                        ("parentAnchorIds", Dict(("type", "array"), ("items", Dict(("type", "string"))))),
                        ("meta", Ref("#/$defs/meta"))
                    ))
                )),
                ("meta", Dict(
                    ("type", "object"),
                    ("additionalProperties", false),
                    ("required", new[] { "createdAtUtc" }),
                    ("properties", Dict(
                        ("createdAtUtc", Dict(("type", "string"), ("format", "date-time"))),
                        ("createdBy", Dict(("type", stringOrNull))),
                        ("label", Dict(("type", stringOrNull))),
                        ("reason", Dict(("type", stringOrNull)))
                    ))
                )),
                ("refObject", Dict(
                    ("type", "object"),
                    ("additionalProperties", false),
                    ("required", new[] { "id", "kind", "uri" }),
                    ("properties", Dict(
                        ("id", Dict(("type", "string"))),
                        ("kind", Dict(("type", "string"))),
                        ("uri", Dict(("type", "string"))),
                        ("hash", Dict(("type", stringOrNull))),
                        ("mediaType", Dict(("type", stringOrNull))),
                        ("meta", Dict(("oneOf", new object[] { Ref("#/$defs/meta"), Dict(("type", "null")) })))
                    ))
                )),
                ("evidenceRefObject", Dict(
                    ("type", "object"),
                    ("additionalProperties", false),
                    ("required", new[] { "id", "kind", "ref" }),
                    ("properties", Dict(
                        ("id", Dict(("type", "string"))),
                        ("kind", Dict(("type", "string"))),
                        ("ref", Ref("#/$defs/refObject")),
                        ("summary", Dict(("type", stringOrNull))),
                        ("hash", Dict(("type", stringOrNull)))
                    ))
                )),
                ("artifact", Dict(
                    ("type", "object"),
                    ("additionalProperties", false),
                    ("required", new[] { "kind", "path", "changeType", "gitStatus", "source" }),
                    ("properties", Dict(
                        ("kind", Dict(("type", "string"))),
                        ("path", Dict(("type", "string"))),
                        ("changeType", Dict(("type", "string"))),
                        ("gitStatus", Dict(("type", "string"))),
                        ("source", Dict(("type", "string")))
                    ))
                )),
                ("derivedRelationships", Dict(
                    ("type", "object"),
                    ("additionalProperties", false),
                    ("required", new[] { "activeStateIds", "activeDeltaIds", "orphanStateIds", "orphanDeltaIds", "anchorsByStateId", "deltasByToStateId", "deltasByFromStateId" }),
                    ("properties", Dict(
                        ("activeStateIds", Dict(("type", "array"), ("items", Dict(("type", "string"))))),
                        ("activeDeltaIds", Dict(("type", "array"), ("items", Dict(("type", "string"))))),
                        ("orphanStateIds", Dict(("type", "array"), ("items", Dict(("type", "string"))))),
                        ("orphanDeltaIds", Dict(("type", "array"), ("items", Dict(("type", "string"))))),
                        ("anchorsByStateId", Dict(("type", "object"), ("additionalProperties", Dict(("type", "array"), ("items", Ref("#/$defs/anchorSummary")))))),
                        ("deltasByToStateId", Dict(("type", "object"), ("additionalProperties", Dict(("type", "array"), ("items", Dict(("type", "string"))))))),
                        ("deltasByFromStateId", Dict(("type", "object"), ("additionalProperties", Dict(("type", "array"), ("items", Dict(("type", "string")))))))
                    ))
                )),
                ("anchorSummary", Dict(
                    ("type", "object"),
                    ("additionalProperties", false),
                    ("required", new[] { "id", "label" }),
                    ("properties", Dict(
                        ("id", Dict(("type", "string"))),
                        ("label", Dict(("type", stringOrNull)))
                    ))
                ))
            ))
        );
    }

    private object BuildDagMetadata()
    {
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["schemaVersion"] = 1,
            ["type"] = "rck-dag-context-pack-v1",
            ["generatedAtUtc"] = GeneratedAtUtc,
            ["workspace"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["name"] = WorkspaceName,
                ["root"] = Workspace?.Root,
                ["gitContext"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["branch"] = Workspace?.GitBranch,
                    ["commit"] = Workspace?.GitCommit,
                    ["dirty"] = Workspace?.GitDirty ?? false,
                },
                ["changedArtifacts"] = ChangedArtifacts,
            },
            ["headStateId"] = HeadStateId,
            ["headShortId"] = HeadShortId,
            ["counts"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["states"] = StateCount,
                ["deltas"] = DeltaCount,
                ["anchors"] = AnchorCount,
                ["activeChainLength"] = ActiveChainLength,
                ["orphanStateCount"] = OrphanStateCount,
                ["orphanDeltaCount"] = OrphanDeltaCount,
            },
        };
    }

    private static string FormatArtifactSummary(GitWorkspaceArtifactChange artifact)
        => $"{artifact.Path} [{artifact.ChangeType}, {artifact.GitStatus}, {artifact.Source}]";

    private static string GetWorkspaceName(string repoRoot)
    {
        var trimmedRoot = repoRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var workspaceName = Path.GetFileName(trimmedRoot);
        return string.IsNullOrWhiteSpace(workspaceName) ? trimmedRoot : workspaceName;
    }

    private static string? GetShortId(string id)
        => id.Length <= 7 ? id : id[..7];

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
