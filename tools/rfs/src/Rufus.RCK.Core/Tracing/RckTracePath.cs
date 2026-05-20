using Rufus.RCK.Core.Model;

namespace Rufus.RCK.Core.Tracing;

public sealed record RckTracePath
{
    public RckStateId FromStateId { get; }

    public RckStateId ToStateId { get; }

    public IReadOnlyList<RckStateId> StateIds { get; }

    public IReadOnlyList<RckDeltaId> DeltaIds { get; }

    public RckTracePath(
        RckStateId fromStateId,
        RckStateId toStateId,
        IEnumerable<RckStateId> stateIds,
        IEnumerable<RckDeltaId> deltaIds)
    {
        ArgumentNullException.ThrowIfNull(fromStateId);
        ArgumentNullException.ThrowIfNull(toStateId);
        ArgumentNullException.ThrowIfNull(stateIds);
        ArgumentNullException.ThrowIfNull(deltaIds);

        FromStateId = fromStateId;
        ToStateId = toStateId;
        StateIds = stateIds.ToArray();
        DeltaIds = deltaIds.ToArray();
    }
}
