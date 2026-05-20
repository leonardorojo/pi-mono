using Rufus.RCK.Core.Model;

namespace Rufus.RCK.Core.Storage;

public interface IRckAnchorStore
{
    Task SaveAsync(RckAnchor anchor, CancellationToken cancellationToken = default);

    Task<RckAnchor?> GetAsync(RckAnchorId id, CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(RckAnchorId id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RckAnchor>> GetByStateAsync(RckStateId stateId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RckAnchor>> ListAllAsync(CancellationToken cancellationToken = default);
}
