using Rufus.RCK.Core.Model;

namespace Rufus.RCK.Core.Storage;

public interface IRckDeltaStore
{
    Task SaveAsync(RckDelta delta, CancellationToken cancellationToken = default);

    Task<RckDelta?> GetAsync(RckDeltaId id, CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(RckDeltaId id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RckDelta>> GetFromStateAsync(RckStateId stateId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RckDelta>> GetToStateAsync(RckStateId stateId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RckDelta>> ListAllAsync(CancellationToken cancellationToken = default);
}
