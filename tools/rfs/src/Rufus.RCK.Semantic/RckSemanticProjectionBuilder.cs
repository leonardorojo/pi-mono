using System.Security.Cryptography;
using System.Text;

namespace Rufus.RCK.Semantic;

/// <summary>
/// Minimal input record for building a semantic projection from anchors.
/// Decoupled from RCK Core types so the builder is testable in memory.
/// </summary>
public sealed record RckSemanticAnchorInput
{
    public string AnchorId { get; }
    public string AnchorLabel { get; }
    public string StateId { get; }
    public DateTimeOffset CreatedAtUtc { get; }

    public RckSemanticAnchorInput(string anchorId, string anchorLabel, string stateId, DateTimeOffset createdAtUtc)
    {
        AnchorId = anchorId ?? throw new ArgumentNullException(nameof(anchorId));
        AnchorLabel = anchorLabel ?? throw new ArgumentNullException(nameof(anchorLabel));
        StateId = stateId ?? throw new ArgumentNullException(nameof(stateId));
        CreatedAtUtc = createdAtUtc;
    }
}

/// <summary>
/// Builds an <see cref="RckSemanticProjection"/> from ordered anchor inputs.
/// Pure in-memory — no filesystem, no LLM, no RCK storage writes.
/// </summary>
public static class RckSemanticProjectionBuilder
{
    /// <summary>
    /// Build a semantic projection from a sequence of anchor inputs.
    /// Anchors are ordered by CreatedAtUtc ascending.
    /// One SemanticNode per anchor. One SemanticDelta per consecutive pair.
    /// IDs are deterministic (SHA-256 derived from anchor ids).
    /// </summary>
    public static RckSemanticProjection BuildFromAnchors(IReadOnlyList<RckSemanticAnchorInput> anchors)
    {
        ArgumentNullException.ThrowIfNull(anchors);

        var ordered = anchors
            .OrderBy(a => a.CreatedAtUtc)
            .ThenBy(a => a.AnchorId, StringComparer.Ordinal)
            .ToArray();

        var nodes = new List<RckSemanticNode>(ordered.Length);
        var deltas = new List<RckSemanticDelta>(Math.Max(0, ordered.Length - 1));

        for (var i = 0; i < ordered.Length; i++)
        {
            var anchor = ordered[i];
            var nodeId = DeriveNodeId(anchor.AnchorId);
            var summary = string.IsNullOrWhiteSpace(anchor.AnchorLabel) ? anchor.AnchorId : anchor.AnchorLabel;
            var topics = NormalizeTopicTokens(anchor.AnchorLabel);

            var node = new RckSemanticNode(
                id: nodeId,
                anchorId: anchor.AnchorId,
                anchorName: anchor.AnchorLabel,
                stateId: anchor.StateId,
                summary: summary,
                topics: topics,
                createdAtUtc: anchor.CreatedAtUtc);

            nodes.Add(node);

            // Create delta between this node and the previous one
            if (i > 0)
            {
                var prevAnchor = ordered[i - 1];
                var prevNode = nodes[i - 1];
                var deltaId = DeriveDeltaId(prevAnchor.AnchorId, anchor.AnchorId);
                var deltaSummary = BuildDeltaSummary(prevAnchor.AnchorLabel, anchor.AnchorLabel);
                var sourceStateIds = new[] { prevAnchor.StateId, anchor.StateId };

                var delta = new RckSemanticDelta(
                    id: deltaId,
                    fromNodeId: prevNode.Id,
                    toNodeId: node.Id,
                    fromAnchorId: prevAnchor.AnchorId,
                    toAnchorId: anchor.AnchorId,
                    summary: deltaSummary,
                    sourceStateIds: sourceStateIds,
                    sourceDeltaIds: Array.Empty<string>(),
                    createdAtUtc: anchor.CreatedAtUtc);

                deltas.Add(delta);
            }
        }

        return RckSemanticProjection.Create(nodes, deltas);
    }

    /// <summary>
    /// Node ID is a deterministic SHA-256 hash derived from the anchor ID.
    /// Truncated to first 16 hex chars for readability.
    /// </summary>
    public static string DeriveNodeId(string anchorId)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes("rck-semantic-node:" + anchorId));
        return Convert.ToHexString(hash)[..16];
    }

    /// <summary>
    /// Delta ID is a deterministic SHA-256 hash derived from both anchor IDs.
    /// Truncated to first 16 hex chars for readability.
    /// </summary>
    public static string DeriveDeltaId(string fromAnchorId, string toAnchorId)
    {
        var input = $"rck-semantic-delta:{fromAnchorId}|{toAnchorId}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(hash)[..16];
    }

    private static string BuildDeltaSummary(string fromLabel, string toLabel)
    {
        var from = string.IsNullOrWhiteSpace(fromLabel) ? "unnamed" : fromLabel;
        var to = string.IsNullOrWhiteSpace(toLabel) ? "unnamed" : toLabel;
        return $"Changes between anchor '{from}' and anchor '{to}'";
    }

    private static IReadOnlyList<string> NormalizeTopicTokens(string? label)
    {
        if (string.IsNullOrWhiteSpace(label))
            return Array.Empty<string>();

        return label
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(token => new string(token.Where(c => char.IsLetterOrDigit(c)).ToArray()).ToLowerInvariant())
            .Where(t => t.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}
