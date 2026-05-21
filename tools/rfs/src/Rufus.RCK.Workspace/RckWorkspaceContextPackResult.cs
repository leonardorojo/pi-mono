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

        public string ToJson()
    {
        if (!Success)
        {
            return System.Text.Json.JsonSerializer.Serialize(new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["success"] = false,
                ["errorMessage"] = ErrorMessage,
            }, IndentedJsonOptions);
        }

        return System.Text.Json.JsonSerializer.Serialize(BuildContextPackDocument(), IndentedJsonOptions);
    }

    private object BuildContextPackDocument()
    {
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["schemaVersion"] = 1,
            ["type"] = "rck-dag-context-pack-v1",
            ["generatedAtUtc"] = GeneratedAtUtc,
            ["schema"] = BuildContextPackSchema(),
            ["interpretationRules"] = BuildInterpretationRules(),
            ["quickIndex"] = BuildQuickIndex(),
            ["workspace"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["name"] = WorkspaceName,
                ["root"] = RepoRoot,
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
            ["activeChain"] = ActiveChain,
            ["states"] = States,
            ["deltas"] = Deltas,
            ["anchors"] = Anchors,
            ["derivedRelationships"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["activeStateIds"] = ActiveStateIds,
                ["activeDeltaIds"] = ActiveDeltaIds,
                ["orphanStateIds"] = OrphanStateIds,
                ["orphanDeltaIds"] = OrphanDeltaIds,
                ["anchorsByStateId"] = AnchorsByStateId,
                ["deltasByToStateId"] = DeltasByToStateId,
                ["deltasByFromStateId"] = DeltasByFromStateId,
            },
            ["notes"] = BuildNotes(),
        };
    }

    private object BuildQuickIndex()
    {
        var mainAnchors = ActiveChain.SelectMany(entry => entry.Anchors).ToArray();
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["workspace"] = WorkspaceName,
            ["root"] = RepoRoot,
            ["branch"] = Workspace?.GitBranch,
            ["commit"] = Workspace?.GitCommit,
            ["dirty"] = Workspace?.GitDirty ?? false,
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
            ["currentChangedArtifacts"] = ChangedArtifacts,
            ["mainAnchors"] = mainAnchors,
        };
    }

    private static IEnumerable<string> BuildInterpretationRules()
    {
        return new[]
        {
            "RCK is a cognitive trace DAG.",
            "State = cognitive snapshot.",
            "Delta = transition between two States.",
            "Anchor = semantic/material milestone pointing to a State.",
            "HEAD points to the current active State.",
            "The active chain is recovered by starting from HEAD and following Deltas backwards from toStateId to fromStateId.",
            ".rfs/ is internal RCK workspace metadata.",
            ".rfs/ may appear in agent observations, but it should not be treated as user-facing repository source.",
            "The recorder excludes .rfs/ from changed artifact tracking.",
            "Git stores actual file contents and diffs.",
            "Artifact paths are references, not file contents.",
            "Agent answers are recorded observations, not direct source-code evidence.",
            "Do not assume file contents unless provided separately.",
        };
    }

    private static object BuildContextPackSchema()
    {
        const string schemaJson = """
        {
          "$schema": "https://json-schema.org/draft/2020-12/schema",
          "$id": "urn:rfs:rck-dag-context-pack:v1",
          "title": "RCK DAG Context Pack v1",
          "type": "object",
          "additionalProperties": false,
          "required": [
            "schemaVersion",
            "type",
            "generatedAtUtc",
            "schema",
            "interpretationRules",
            "quickIndex",
            "workspace",
            "headStateId",
            "headShortId",
            "counts",
            "activeChain",
            "states",
            "deltas",
            "anchors",
            "derivedRelationships",
            "notes"
          ],
          "properties": {
            "schemaVersion": { "type": "integer", "const": 1 },
            "type": { "type": "string", "const": "rck-dag-context-pack-v1" },
            "generatedAtUtc": { "type": "string", "format": "date-time" },
            "schema": {
              "type": "object",
              "additionalProperties": false,
              "required": ["$schema", "$id", "title", "type", "required", "properties", "$defs"],
              "properties": {
                "$schema": { "type": "string", "const": "https://json-schema.org/draft/2020-12/schema" },
                "$id": { "type": "string", "const": "urn:rfs:rck-dag-context-pack:v1" },
                "title": { "type": "string", "const": "RCK DAG Context Pack v1" },
                "type": { "type": "string", "const": "object" },
                "required": { "type": "array", "items": { "type": "string" } },
                "properties": { "type": "object" },
                "$defs": { "type": "object" }
              }
            },
            "interpretationRules": { "type": "array", "items": { "type": "string" } },
            "quickIndex": { "$ref": "#/$defs/quickIndex" },
            "workspace": { "$ref": "#/$defs/workspace" },
            "headStateId": { "type": "string" },
            "headShortId": { "type": "string" },
            "counts": { "$ref": "#/$defs/counts" },
            "activeChain": { "type": "array", "items": { "$ref": "#/$defs/activeEntry" } },
            "states": { "type": "array", "items": { "$ref": "#/$defs/stateObject" } },
            "deltas": { "type": "array", "items": { "$ref": "#/$defs/deltaObject" } },
            "anchors": { "type": "array", "items": { "$ref": "#/$defs/anchorObject" } },
            "derivedRelationships": { "$ref": "#/$defs/derivedRelationships" },
            "notes": { "$ref": "#/$defs/notes" }
          },
          "$defs": {
            "quickIndex": {
              "type": "object",
              "additionalProperties": false,
              "required": ["workspace", "root", "branch", "commit", "dirty", "headStateId", "headShortId", "counts", "currentChangedArtifacts", "mainAnchors"],
              "properties": {
                "workspace": { "type": "string" },
                "root": { "type": "string" },
                "branch": { "type": ["string", "null"] },
                "commit": { "type": ["string", "null"] },
                "dirty": { "type": "boolean" },
                "headStateId": { "type": "string" },
                "headShortId": { "type": "string" },
                "counts": { "$ref": "#/$defs/counts" },
                "currentChangedArtifacts": { "type": "array", "items": { "$ref": "#/$defs/artifact" } },
                "mainAnchors": { "type": "array", "items": { "$ref": "#/$defs/anchorSummary" } }
              }
            },
            "workspace": {
              "type": "object",
              "additionalProperties": false,
              "required": ["name", "root", "gitContext", "changedArtifacts"],
              "properties": {
                "name": { "type": "string" },
                "root": { "type": "string" },
                "gitContext": { "$ref": "#/$defs/gitContext" },
                "changedArtifacts": { "type": "array", "items": { "$ref": "#/$defs/artifact" } }
              }
            },
            "gitContext": {
              "type": "object",
              "additionalProperties": false,
              "required": ["branch", "commit", "dirty"],
              "properties": {
                "branch": { "type": ["string", "null"] },
                "commit": { "type": ["string", "null"] },
                "dirty": { "type": "boolean" }
              }
            },
            "counts": {
              "type": "object",
              "additionalProperties": false,
              "required": ["states", "deltas", "anchors", "activeChainLength", "orphanStateCount", "orphanDeltaCount"],
              "properties": {
                "states": { "type": "integer", "minimum": 0 },
                "deltas": { "type": "integer", "minimum": 0 },
                "anchors": { "type": "integer", "minimum": 0 },
                "activeChainLength": { "type": "integer", "minimum": 0 },
                "orphanStateCount": { "type": "integer", "minimum": 0 },
                "orphanDeltaCount": { "type": "integer", "minimum": 0 }
              }
            },
            "activeEntry": {
              "type": "object",
              "additionalProperties": false,
              "required": ["stateId", "anchors", "mode", "promptIsExcerpt", "gitContext", "artifacts"],
              "properties": {
                "stateId": { "type": "string" },
                "incomingDeltaId": { "type": ["string", "null"] },
                "anchors": { "type": "array", "items": { "$ref": "#/$defs/anchorSummary" } },
                "mode": { "type": "string" },
                "prompt": { "type": ["string", "null"] },
                "promptIsExcerpt": { "type": "boolean" },
                "answerSummary": { "type": ["string", "null"] },
                "gitContext": { "$ref": "#/$defs/gitContext" },
                "artifacts": { "type": "array", "items": { "$ref": "#/$defs/artifact" } }
              }
            },
            "stateObject": {
              "type": "object",
              "additionalProperties": false,
              "required": ["id", "payloadCanonicalJson", "payloadDecoded", "refs", "meta"],
              "properties": {
                "id": { "type": "string" },
                "payloadCanonicalJson": { "type": "string" },
                "payloadDecoded": { "$ref": "#/$defs/jsonValue" },
                "refs": { "type": "array", "items": { "$ref": "#/$defs/refObject" } },
                "meta": { "$ref": "#/$defs/meta" }
              }
            },
            "deltaObject": {
              "type": "object",
              "additionalProperties": false,
              "required": ["id", "fromStateId", "toStateId", "ops", "refs", "evidenceRefs", "meta"],
              "properties": {
                "id": { "type": "string" },
                "fromStateId": { "type": "string" },
                "toStateId": { "type": "string" },
                "ops": { "type": "array", "items": { "$ref": "#/$defs/deltaOpObject" } },
                "refs": { "type": "array", "items": { "$ref": "#/$defs/refObject" } },
                "evidenceRefs": { "type": "array", "items": { "$ref": "#/$defs/evidenceRefObject" } },
                "meta": { "$ref": "#/$defs/meta" }
              }
            },
            "deltaOpObject": {
              "type": "object",
              "additionalProperties": false,
              "required": ["kind", "path"],
              "properties": {
                "kind": { "type": "string" },
                "path": { "type": "string" },
                "valueJson": { "type": ["string", "null"] },
                "decodedValueJson": { "$ref": "#/$defs/jsonValue" }
              }
            },
            "anchorObject": {
              "type": "object",
              "additionalProperties": false,
              "required": ["id", "stateId", "parentAnchorIds", "meta"],
              "properties": {
                "id": { "type": "string" },
                "stateId": { "type": "string" },
                "parentAnchorIds": { "type": "array", "items": { "type": "string" } },
                "meta": { "$ref": "#/$defs/meta" }
              }
            },
            "meta": {
              "type": "object",
              "additionalProperties": false,
              "required": ["createdAtUtc"],
              "properties": {
                "createdAtUtc": { "type": "string", "format": "date-time" },
                "createdBy": { "type": ["string", "null"] },
                "label": { "type": ["string", "null"] },
                "reason": { "type": ["string", "null"] }
              }
            },
            "refObject": {
              "type": "object",
              "additionalProperties": false,
              "required": ["id", "kind", "uri"],
              "properties": {
                "id": { "type": "string" },
                "kind": { "type": "string" },
                "uri": { "type": "string" },
                "hash": { "type": ["string", "null"] },
                "mediaType": { "type": ["string", "null"] },
                "meta": { "oneOf": [ { "$ref": "#/$defs/meta" }, { "type": "null" } ] }
              }
            },
            "evidenceRefObject": {
              "type": "object",
              "additionalProperties": false,
              "required": ["id", "kind", "ref"],
              "properties": {
                "id": { "type": "string" },
                "kind": { "type": "string" },
                "ref": { "$ref": "#/$defs/refObject" },
                "summary": { "type": ["string", "null"] },
                "hash": { "type": ["string", "null"] }
              }
            },
            "artifact": {
              "type": "object",
              "additionalProperties": false,
              "required": ["kind", "path", "changeType", "gitStatus", "source"],
              "properties": {
                "kind": { "type": "string" },
                "path": { "type": "string" },
                "changeType": { "type": "string" },
                "gitStatus": { "type": "string" },
                "source": { "type": "string" }
              }
            },
            "derivedRelationships": {
              "type": "object",
              "additionalProperties": false,
              "required": ["activeStateIds", "activeDeltaIds", "orphanStateIds", "orphanDeltaIds", "anchorsByStateId", "deltasByToStateId", "deltasByFromStateId"],
              "properties": {
                "activeStateIds": { "type": "array", "items": { "type": "string" } },
                "activeDeltaIds": { "type": "array", "items": { "type": "string" } },
                "orphanStateIds": { "type": "array", "items": { "type": "string" } },
                "orphanDeltaIds": { "type": "array", "items": { "type": "string" } },
                "anchorsByStateId": { "type": "object", "additionalProperties": { "type": "array", "items": { "$ref": "#/$defs/anchorSummary" } } },
                "deltasByToStateId": { "type": "object", "additionalProperties": { "type": "array", "items": { "type": "string" } } },
                "deltasByFromStateId": { "type": "object", "additionalProperties": { "type": "array", "items": { "type": "string" } } }
              }
            },
            "anchorSummary": {
              "type": "object",
              "additionalProperties": false,
              "required": ["id", "label"],
              "properties": {
                "id": { "type": "string" },
                "label": { "type": ["string", "null"] }
              }
            },
            "jsonValue": {
              "description": "Any JSON value.",
              "type": ["object", "array", "string", "number", "boolean", "null"]
            },
            "notes": {
              "type": "array",
              "items": { "type": "string" }
            }
          }
        }
        """;

        return JsonDocument.Parse(schemaJson).RootElement.Clone();
    }

    private static object BuildNotes()
    {
        return new[]
        {
            "This is a full DAG export, not a repository content export.",
            "It does not include file contents.",
            "It does not include git diffs.",
            "It does not include artifact hashes yet.",
            "RckRef / EvidenceRef may be empty in the current implementation.",
            "The LLM should use this as trace context, not as source code evidence.",
        };
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
