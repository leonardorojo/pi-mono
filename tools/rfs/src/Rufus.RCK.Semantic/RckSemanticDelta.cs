namespace Rufus.RCK.Semantic;

public sealed record RckSemanticDelta
{
    public string Id { get; }
    public string FromNodeId { get; }
    public string ToNodeId { get; }
    public string FromAnchorId { get; }
    public string ToAnchorId { get; }
    public string Summary { get; }
    public IReadOnlyList<string> SourceStateIds { get; }
    public IReadOnlyList<string> SourceDeltaIds { get; }
    public DateTimeOffset CreatedAtUtc { get; }

    public RckSemanticDelta(
        string id,
        string fromNodeId,
        string toNodeId,
        string fromAnchorId,
        string toAnchorId,
        string summary,
        IReadOnlyList<string> sourceStateIds,
        IReadOnlyList<string> sourceDeltaIds,
        DateTimeOffset createdAtUtc)
    {
        Id = id ?? throw new ArgumentNullException(nameof(id));
        FromNodeId = fromNodeId ?? throw new ArgumentNullException(nameof(fromNodeId));
        ToNodeId = toNodeId ?? throw new ArgumentNullException(nameof(toNodeId));
        FromAnchorId = fromAnchorId ?? throw new ArgumentNullException(nameof(fromAnchorId));
        ToAnchorId = toAnchorId ?? throw new ArgumentNullException(nameof(toAnchorId));
        Summary = summary ?? throw new ArgumentNullException(nameof(summary));
        SourceStateIds = sourceStateIds ?? Array.Empty<string>();
        SourceDeltaIds = sourceDeltaIds ?? Array.Empty<string>();
        CreatedAtUtc = createdAtUtc;
    }
}
