namespace Rufus.RCK.Semantic;

public sealed record RckSemanticNode
{
    public string Id { get; }
    public string AnchorId { get; }
    public string AnchorName { get; }
    public string StateId { get; }
    public string Summary { get; }
    public IReadOnlyList<string> Topics { get; }
    public DateTimeOffset CreatedAtUtc { get; }

    public RckSemanticNode(
        string id,
        string anchorId,
        string anchorName,
        string stateId,
        string summary,
        IReadOnlyList<string> topics,
        DateTimeOffset createdAtUtc)
    {
        Id = id ?? throw new ArgumentNullException(nameof(id));
        AnchorId = anchorId ?? throw new ArgumentNullException(nameof(anchorId));
        AnchorName = anchorName;
        StateId = stateId ?? throw new ArgumentNullException(nameof(stateId));
        Summary = summary ?? throw new ArgumentNullException(nameof(summary));
        Topics = topics ?? Array.Empty<string>();
        CreatedAtUtc = createdAtUtc;
    }
}
