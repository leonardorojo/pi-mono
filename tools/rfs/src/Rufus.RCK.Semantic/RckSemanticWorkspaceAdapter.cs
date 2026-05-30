using Rufus.RCK.Workspace;

namespace Rufus.RCK.Semantic;

/// <summary>
/// Rebuilds or reads an <see cref="RckSemanticProjection"/> from a real RCK workspace.
/// Reads .rfs/rck (anchors) and writes .rfs/semantic/projection.json.
/// Never writes to .rfs/rck.
/// </summary>
public static class RckSemanticWorkspaceAdapter
{
    /// <summary>
    /// Rebuild the semantic projection from the current workspace and persist it.
    /// </summary>
    public static RckSemanticRebuildResult RebuildProjection(string? workspaceRoot = null)
    {
        workspaceRoot ??= Directory.GetCurrentDirectory();

        // 1. Read workspace context pack
        var contextPack = RckWorkspaceContextPackReader.Read(workspaceRoot);
        if (!contextPack.Success)
        {
            return RckSemanticRebuildResult.Failed(
                contextPack.ErrorMessage ?? "rfs semantic rebuild: failed to read RCK workspace.");
        }

        // 2. Map anchors to semantic inputs
        var anchorInputs = contextPack.Anchors
            .Select(anchor => new RckSemanticAnchorInput(
                anchorId: anchor.Id,
                anchorLabel: anchor.Meta.Label ?? anchor.Id,
                stateId: anchor.StateId,
                createdAtUtc: anchor.Meta.CreatedAtUtc))
            .ToArray();

        if (anchorInputs.Length == 0)
        {
            return RckSemanticRebuildResult.Failed(
                "rfs semantic rebuild: no anchors found in workspace. Create an anchor first (e.g. rfs anchor …).");
        }

        // 3. Build projection
        var projection = RckSemanticProjectionBuilder.BuildFromAnchors(anchorInputs);

        // 4. Determine output path
        var paths = new RckWorkspacePaths(contextPack.RepoRoot!);
        var semanticDir = Path.Combine(paths.WorkspaceDirectory, "semantic");
        var projectionPath = Path.Combine(semanticDir, "projection.json");

        // 5. Write
        RckSemanticProjectionJsonStore.Write(projectionPath, projection);

        return RckSemanticRebuildResult.Succeeded(
            nodeCount: projection.Nodes.Count,
            deltaCount: projection.Deltas.Count,
            outputPath: projectionPath);
    }

    /// <summary>
    /// Read the existing semantic projection from disk.
    /// </summary>
    public static RckSemanticProjection? TryReadProjection(string? workspaceRoot = null)
    {
        workspaceRoot ??= Directory.GetCurrentDirectory();

        var contextPack = RckWorkspaceContextPackReader.Read(workspaceRoot);
        if (!contextPack.Success)
            return null;

        var paths = new RckWorkspacePaths(contextPack.RepoRoot!);
        var projectionPath = Path.Combine(paths.WorkspaceDirectory, "semantic", "projection.json");

        if (!File.Exists(projectionPath))
            return null;

        try
        {
            return RckSemanticProjectionJsonStore.Read(projectionPath);
        }
        catch
        {
            return null;
        }
    }
}

public sealed class RckSemanticRebuildResult
{
    public bool Success { get; }
    public string? ErrorMessage { get; }
    public int NodeCount { get; }
    public int DeltaCount { get; }
    public string? OutputPath { get; }

    private RckSemanticRebuildResult(bool success, string? errorMessage, int nodeCount, int deltaCount, string? outputPath)
    {
        Success = success;
        ErrorMessage = errorMessage;
        NodeCount = nodeCount;
        DeltaCount = deltaCount;
        OutputPath = outputPath;
    }

    public static RckSemanticRebuildResult Succeeded(int nodeCount, int deltaCount, string outputPath)
        => new(true, null, nodeCount, deltaCount, outputPath);

    public static RckSemanticRebuildResult Failed(string errorMessage)
        => new(false, errorMessage, 0, 0, null);
}
