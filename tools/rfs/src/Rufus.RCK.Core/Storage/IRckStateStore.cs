using Rufus.RCK.Core.Model;

namespace Rufus.RCK.Core.Storage;

public interface IRckStateStore
{
    Task SaveAsync(RckState state, CancellationToken cancellationToken = default);

    Task<RckState?> GetAsync(RckStateId id, CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(RckStateId id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RckState>> ListAllAsync(CancellationToken cancellationToken = default);
}
