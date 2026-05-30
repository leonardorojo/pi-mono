namespace Rufus.RCK.Semantic;

public sealed record RckSemanticProjection
{
    public int SchemaVersion { get; }
    public DateTimeOffset BuiltAtUtc { get; }
    public IReadOnlyList<RckSemanticNode> Nodes { get; }
    public IReadOnlyList<RckSemanticDelta> Deltas { get; }

    public RckSemanticProjection(
        int schemaVersion,
        DateTimeOffset builtAtUtc,
        IReadOnlyList<RckSemanticNode> nodes,
        IReadOnlyList<RckSemanticDelta> deltas)
    {
        SchemaVersion = schemaVersion;
        BuiltAtUtc = builtAtUtc;
        Nodes = nodes ?? Array.Empty<RckSemanticNode>();
        Deltas = deltas ?? Array.Empty<RckSemanticDelta>();
    }

    public static RckSemanticProjection Create(
        IReadOnlyList<RckSemanticNode> nodes,
        IReadOnlyList<RckSemanticDelta> deltas)
        => new(schemaVersion: 1, DateTimeOffset.UtcNow, nodes, deltas);
}
