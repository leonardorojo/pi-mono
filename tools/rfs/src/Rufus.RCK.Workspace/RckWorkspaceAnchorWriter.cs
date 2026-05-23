using System.Text;
using System.Text.Json;
using Rufus.RCK.Core.Hashing;
using Rufus.RCK.Core.Model;

namespace Rufus.RCK.Workspace;

public sealed record RckAnchorCreateResult
{
    public bool Success { get; }

    public string? ErrorMessage { get; }

    public string? RepoRoot { get; }

    public RckWorkspacePaths? Paths { get; }

    public bool AnchorCreated { get; }

    public string? AnchorLabel { get; }

    public RckStateId? StateId { get; }

    public RckAnchorId? AnchorId { get; }

    private RckAnchorCreateResult(
        bool success,
        string? errorMessage,
        string? repoRoot,
        RckWorkspacePaths? paths,
        bool anchorCreated,
        string? anchorLabel,
        RckStateId? stateId,
        RckAnchorId? anchorId)
    {
        Success = success;
        ErrorMessage = errorMessage;
        RepoRoot = repoRoot;
        Paths = paths;
        AnchorCreated = anchorCreated;
        AnchorLabel = anchorLabel;
        StateId = stateId;
        AnchorId = anchorId;
    }

    public static RckAnchorCreateResult Failure(string errorMessage)
    {
        return new RckAnchorCreateResult(
            success: false,
            errorMessage: errorMessage,
            repoRoot: null,
            paths: null,
            anchorCreated: false,
            anchorLabel: null,
            stateId: null,
            anchorId: null);
    }

    public static RckAnchorCreateResult SuccessResult(
        string repoRoot,
        RckWorkspacePaths paths,
        bool anchorCreated,
        string anchorLabel,
        RckStateId stateId,
        RckAnchorId anchorId)
    {
        return new RckAnchorCreateResult(
            success: true,
            errorMessage: null,
            repoRoot: repoRoot,
            paths: paths,
            anchorCreated: anchorCreated,
            anchorLabel: anchorLabel,
            stateId: stateId,
            anchorId: anchorId);
    }

    public IEnumerable<string> FormatConsoleLines()
    {
        if (!Success)
        {
            if (!string.IsNullOrWhiteSpace(ErrorMessage))
            {
                yield return ErrorMessage;
            }

            yield break;
        }

        yield return AnchorCreated ? "Anchor created:" : "Anchor already existed:";
        yield return $"  name: {AnchorLabel}";
        yield return $"  state: {StateId}";
        yield return $"  id: {AnchorId}";
    }
}

public static class RckWorkspaceAnchorWriter
{
    private static readonly JsonSerializerOptions IndentedJsonOptions = new() { WriteIndented = true };
    private static readonly UTF8Encoding Utf8NoBom = new(false);

    public static RckAnchorCreateResult CreateExplicitAnchor(string label, string? startingDirectory = null)
    {
        var normalizedLabel = NormalizeLabel(label);
        if (normalizedLabel is null)
        {
            return RckAnchorCreateResult.Failure("/anchor requires a non-empty milestone name.");
        }

        var repoRoot = FindRepoRoot(startingDirectory ?? Directory.GetCurrentDirectory());
        if (repoRoot is null)
        {
            return RckAnchorCreateResult.Failure("/anchor: repository root not found.");
        }

        var paths = new RckWorkspacePaths(repoRoot);
        if (!File.Exists(paths.HeadPath))
        {
            return RckAnchorCreateResult.Failure("/anchor: RFS workspace is not initialized. Run rfs init first.");
        }

        var stateId = ReadHeadStateId(paths.HeadPath);
        if (stateId is null)
        {
            return RckAnchorCreateResult.Failure($"/anchor: invalid HEAD file at {paths.HeadPath}.");
        }

        var anchor = RckAnchor.Create(
            stateId,
            meta: new RckAnchorMeta(
                DateTimeOffset.UtcNow,
                createdBy: "rfs anchor",
                label: normalizedLabel,
                reason: "explicit user milestone anchor"));

        var anchorCreated = EnsureAnchor(paths, anchor);
        return RckAnchorCreateResult.SuccessResult(repoRoot, paths, anchorCreated, normalizedLabel, stateId, anchor.Id);
    }

    public static bool EnsureAnchor(RckWorkspacePaths paths, RckAnchor anchor)
    {
        var anchorPath = Path.Combine(paths.AnchorsDirectory, $"{anchor.Id}.json");
        Directory.CreateDirectory(paths.AnchorsDirectory);
        if (File.Exists(anchorPath))
        {
            return false;
        }

        File.WriteAllText(anchorPath, SerializeAnchorEnvelope(anchor), Utf8NoBom);
        return true;
    }

    private static RckStateId? ReadHeadStateId(string headPath)
    {
        var headText = File.ReadAllText(headPath).Trim();
        if (string.IsNullOrWhiteSpace(headText))
        {
            return null;
        }

        return new RckStateId(new RckHash(headText));
    }

    private static string? FindRepoRoot(string startingDirectory)
    {
        var current = new DirectoryInfo(startingDirectory);

        while (current is not null)
        {
            var workspaceDirectory = Path.Combine(current.FullName, ".rfs");
            if (Directory.Exists(workspaceDirectory))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        return null;
    }

    private static string? NormalizeLabel(string? label)
    {
        if (label is null)
        {
            return null;
        }

        var normalized = label.Trim();
        if (normalized.Length == 0)
        {
            return null;
        }

        if (normalized.Contains('\n') || normalized.Contains('\r'))
        {
            return null;
        }

        if (normalized.StartsWith('"') && normalized.EndsWith('"'))
        {
            normalized = normalized[1..^1].Trim();
        }

        return normalized.Length == 0 || normalized.Contains('\n') || normalized.Contains('\r')
            ? null
            : normalized;
    }

    private static string SerializeAnchorEnvelope(RckAnchor anchor)
    {
        var envelope = new
        {
            schemaVersion = 1,
            type = "rufus.rck.anchor",
            id = anchor.Id.ToString(),
            stateId = anchor.StateId.ToString(),
            parentAnchorIds = anchor.ParentAnchorIds.Select(parent => parent.ToString()).ToArray(),
            meta = new
            {
                createdAtUtc = anchor.Meta.CreatedAtUtc,
                anchor.Meta.CreatedBy,
                anchor.Meta.Label,
                anchor.Meta.Reason,
            },
        };

        return JsonSerializer.Serialize(envelope, IndentedJsonOptions);
    }
}
