using Rufus.RCK.Core.Model;

namespace Rufus.RCK.Core.Storage;

public sealed class InMemoryRckStateStore : IRckStateStore
{
    private sealed record StoredState(RckState State, string Fingerprint);

    private readonly object _gate = new();
    private readonly Dictionary<string, StoredState> _states = new(StringComparer.Ordinal);

    public Task SaveAsync(RckState state, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(state);
        cancellationToken.ThrowIfCancellationRequested();

        var key = state.Id.ToString();
        var fingerprint = RckStorageFingerprint.ForState(state);

        lock (_gate)
        {
            if (_states.TryGetValue(key, out var existing))
            {
                if (!string.Equals(existing.Fingerprint, fingerprint, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException($"State {key} already exists with different content.");
                }

                return Task.CompletedTask;
            }

            _states[key] = new StoredState(state, fingerprint);
            return Task.CompletedTask;
        }
    }

    public Task<RckState?> GetAsync(RckStateId id, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(id);
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            return Task.FromResult(_states.TryGetValue(id.ToString(), out var existing) ? existing.State : null);
        }
    }

    public Task<bool> ExistsAsync(RckStateId id, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(id);
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            return Task.FromResult(_states.ContainsKey(id.ToString()));
        }
    }

    public Task<IReadOnlyList<RckState>> ListAllAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            IReadOnlyList<RckState> result = _states.Values
                .Select(entry => entry.State)
                .OrderBy(state => state.Id.ToString(), StringComparer.Ordinal)
                .ToArray();
            return Task.FromResult(result);
        }
    }
}
