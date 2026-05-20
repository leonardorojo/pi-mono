using System.Text.Json;
using Rufus.RCK.Core.Hashing;

namespace Rufus.RCK.Core.Model;

public sealed record RckAnchor
{
    public RckAnchorId Id { get; }

    public RckStateId StateId { get; }

    public IReadOnlyList<RckAnchorId> ParentAnchorIds { get; }

    public RckAnchorMeta Meta { get; }

    private RckAnchor(RckAnchorId id, RckStateId stateId, IReadOnlyList<RckAnchorId> parentAnchorIds, RckAnchorMeta meta)
    {
        Id = id ?? throw new ArgumentNullException(nameof(id));
        StateId = stateId ?? throw new ArgumentNullException(nameof(stateId));
        ParentAnchorIds = parentAnchorIds;
        Meta = meta;
    }

    public static RckAnchor Create(
        RckStateId stateId,
        IEnumerable<RckAnchorId>? parentAnchorIds = null,
        RckAnchorMeta? meta = null)
    {
        ArgumentNullException.ThrowIfNull(stateId);

        var parentList = (parentAnchorIds ?? Array.Empty<RckAnchorId>()).ToArray();
        var canonical = BuildCanonicalAnchorInput(stateId, parentList, meta?.Label);
        var id = new RckAnchorId(RckHasher.HashJson(canonical));
        return new RckAnchor(id, stateId, parentList, meta ?? new RckAnchorMeta(DateTimeOffset.UtcNow));
    }

    internal static RckAnchor Rehydrate(RckAnchorId id, RckStateId stateId, IReadOnlyList<RckAnchorId> parentAnchorIds, RckAnchorMeta meta)
        => new(id, stateId, parentAnchorIds, meta);

    private static string BuildCanonicalAnchorInput(RckStateId stateId, IReadOnlyList<RckAnchorId> parentAnchorIds, string? label)
    {
        var projection = new
        {
            StateId = stateId.Value.Value,
            ParentAnchorIds = parentAnchorIds.Select(id => id.Value.Value).ToArray(),
            Label = label,
        };

        return JsonSerializer.Serialize(projection);
    }
}
