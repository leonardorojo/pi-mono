using System.Text.Json;

namespace Rufus.RCK.Workspace;

public static class RckTraceSliceContextPackBuilder
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public static RckTraceSliceContextPackBuildResult Build(string prompt, string? startingDirectory = null, int maxStates = 5)
    {
        var contextPackResult = RckWorkspaceContextPackReader.Read(startingDirectory);
        if (!contextPackResult.Success)
        {
            return RckTraceSliceContextPackBuildResult.Failure(
                contextPackResult.ErrorMessage ?? "rfs context-pack --trace-slice: failed to read RCK workspace state.");
        }

        var traceSliceResult = RckTraceSliceBuilder.Build(prompt, startingDirectory, maxStates);
        if (!traceSliceResult.Success || string.IsNullOrWhiteSpace(traceSliceResult.Json))
        {
            return RckTraceSliceContextPackBuildResult.Failure(
                traceSliceResult.ErrorMessage ?? "rfs context-pack --trace-slice: failed to build TraceSlice.");
        }

        try
        {
            using var document = JsonDocument.Parse(traceSliceResult.Json);
            var traceSlice = document.RootElement.Clone();

            if (!TryGetObjectProperty(traceSlice, "selection", out var selection))
            {
                return RckTraceSliceContextPackBuildResult.Failure(
                    "rfs context-pack --trace-slice: trace slice is missing selection.");
            }

            var stateIds = ReadRequiredStringArray(selection, "stateIds");
            var deltaIds = ReadRequiredStringArray(selection, "deltaIds");
            var anchorIds = ReadRequiredStringArray(selection, "anchorIds");

            if (!TryGetObjectProperty(traceSlice, "materializationPolicy", out var materializationPolicyElement))
            {
                return RckTraceSliceContextPackBuildResult.Failure(
                    "rfs context-pack --trace-slice: trace slice is missing materializationPolicy.");
            }

            var materializationPolicy = new RckTraceSliceContextPackMaterializationPolicy(
                IncludeStatePayloads: ReadRequiredBoolean(materializationPolicyElement, "includeStatePayloads"),
                IncludeDeltaDecodedOps: ReadRequiredBoolean(materializationPolicyElement, "includeDeltaDecodedOps"),
                IncludeArtifactContents: ReadRequiredBoolean(materializationPolicyElement, "includeArtifactContents"),
                IncludeGitDiffs: ReadRequiredBoolean(materializationPolicyElement, "includeGitDiffs"),
                IncludeStdoutStderr: ReadRequiredBoolean(materializationPolicyElement, "includeStdoutStderr"),
                IncludeJsonl: ReadRequiredBoolean(materializationPolicyElement, "includeJsonl"));

            var artifacts = ReadArtifacts(traceSlice);
            var notes = ReadOptionalStringArray(traceSlice, "notes");
            var exclusions = ReadOptionalStringArray(traceSlice, "exclusions");

            var stateLookup = contextPackResult.States.ToDictionary(state => state.Id, StringComparer.Ordinal);
            var deltaLookup = contextPackResult.Deltas.ToDictionary(delta => delta.Id, StringComparer.Ordinal);
            var anchorLookup = contextPackResult.Anchors.ToDictionary(anchor => anchor.Id, StringComparer.Ordinal);
            var stateIdSet = stateIds.ToHashSet(StringComparer.Ordinal);

            var states = stateIds
                .Where(stateLookup.ContainsKey)
                .Select(stateId => stateLookup[stateId])
                .ToArray();

            var deltas = deltaIds
                .Where(deltaLookup.ContainsKey)
                .Select(deltaId => deltaLookup[deltaId])
                .ToArray();

            var anchors = anchorIds
                .Where(anchorLookup.ContainsKey)
                .Select(anchorId => anchorLookup[anchorId])
                .ToArray();

            var activeChain = contextPackResult.ActiveChain
                .Where(entry => stateIdSet.Contains(entry.StateId))
                .ToArray();

            var output = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["schemaVersion"] = 1,
                ["type"] = "rck-dag-context-pack-v1",
                ["scope"] = "trace-slice",
                ["generatedAtUtc"] = DateTimeOffset.UtcNow,
                ["traceSlice"] = traceSlice,
                ["workspace"] = BuildWorkspace(contextPackResult),
                ["headStateId"] = contextPackResult.HeadStateId,
                ["headShortId"] = contextPackResult.HeadShortId,
                ["counts"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["states"] = states.Length,
                    ["deltas"] = deltas.Length,
                    ["anchors"] = anchors.Length,
                },
                ["activeChain"] = activeChain,
                ["states"] = states,
                ["deltas"] = deltas,
                ["anchors"] = anchors,
                ["artifacts"] = artifacts,
                ["materializationPolicy"] = materializationPolicy,
                ["notes"] = notes,
                ["exclusions"] = exclusions,
            };

            return RckTraceSliceContextPackBuildResult.SuccessResult(JsonSerializer.Serialize(output, JsonOptions));
        }
        catch (Exception ex)
        {
            return RckTraceSliceContextPackBuildResult.Failure(
                $"rfs context-pack --trace-slice: failed to project trace-slice context pack: {ex.Message}");
        }
    }

    private static object BuildWorkspace(RckWorkspaceContextPackResult contextPackResult)
    {
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["name"] = contextPackResult.WorkspaceName,
            ["root"] = contextPackResult.RepoRoot,
            ["gitContext"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["branch"] = contextPackResult.Workspace?.GitBranch,
                ["commit"] = contextPackResult.Workspace?.GitCommit,
                ["dirty"] = contextPackResult.Workspace?.GitDirty ?? false,
            },
        };
    }

    private static IReadOnlyList<string> ReadRequiredStringArray(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidDataException($"Missing array property '{propertyName}'.");
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

    private static IReadOnlyList<string> ReadOptionalStringArray(JsonElement element, string propertyName)
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

    private static bool ReadRequiredBoolean(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property)
            || (property.ValueKind != JsonValueKind.True && property.ValueKind != JsonValueKind.False))
        {
            throw new InvalidDataException($"Missing boolean property '{propertyName}'.");
        }

        return property.GetBoolean();
    }

    private static IReadOnlyList<RckTraceSliceContextPackArtifact> ReadArtifacts(JsonElement traceSlice)
    {
        if (!traceSlice.TryGetProperty("artifacts", out var artifactsElement) || artifactsElement.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<RckTraceSliceContextPackArtifact>();
        }

        var artifacts = new List<RckTraceSliceContextPackArtifact>();
        foreach (var artifactElement in artifactsElement.EnumerateArray())
        {
            if (artifactElement.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var path = ReadOptionalString(artifactElement, "path");
            if (string.IsNullOrWhiteSpace(path))
            {
                continue;
            }

            artifacts.Add(new RckTraceSliceContextPackArtifact(
                Path: path,
                ChangeType: ReadOptionalString(artifactElement, "changeType") ?? "unknown",
                Source: ReadOptionalString(artifactElement, "source") ?? "unknown",
                IncludeMode: ReadOptionalString(artifactElement, "includeMode") ?? "metadata-only"));
        }

        return artifacts;
    }

    private static string? ReadOptionalString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        return property.GetString();
    }

    private static bool TryGetObjectProperty(JsonElement element, string propertyName, out JsonElement property)
    {
        if (element.TryGetProperty(propertyName, out property) && property.ValueKind == JsonValueKind.Object)
        {
            return true;
        }

        property = default;
        return false;
    }
}

public sealed record RckTraceSliceContextPackMaterializationPolicy(
    bool IncludeStatePayloads,
    bool IncludeDeltaDecodedOps,
    bool IncludeArtifactContents,
    bool IncludeGitDiffs,
    bool IncludeStdoutStderr,
    bool IncludeJsonl);

public sealed record RckTraceSliceContextPackArtifact(
    string Path,
    string ChangeType,
    string Source,
    string IncludeMode);

public sealed record RckTraceSliceContextPackBuildResult(bool Success, string? ErrorMessage, string? Json)
{
    public static RckTraceSliceContextPackBuildResult SuccessResult(string json) => new(true, null, json);

    public static RckTraceSliceContextPackBuildResult Failure(string errorMessage) => new(false, errorMessage, null);
}
