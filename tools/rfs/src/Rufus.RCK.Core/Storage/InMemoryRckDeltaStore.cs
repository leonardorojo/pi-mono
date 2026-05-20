using Rufus.RCK.Core.Model;

namespace Rufus.RCK.Core.Storage;

public sealed class InMemoryRckDeltaStore : IRckDeltaStore
{
    private sealed record StoredDelta(RckDelta Delta, string Fingerprint);

    private readonly object _gate = new();
    private readonly Dictionary<string, StoredDelta> _deltas = new(StringComparer.Ordinal);

    public Task SaveAsync(RckDelta delta, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(delta);
        cancellationToken.ThrowIfCancellationRequested();

        var key = delta.Id.ToString();
        var fingerprint = RckStorageFingerprint.ForDelta(delta);

        lock (_gate)
        {
            if (_deltas.TryGetValue(key, out var existing))
            {
                if (!string.Equals(existing.Fingerprint, fingerprint, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException($"Delta {key} already exists with different content.");
                }

                return Task.CompletedTask;
            }

            _deltas[key] = new StoredDelta(delta, fingerprint);
            return Task.CompletedTask;
        }
    }

    public Task<RckDelta?> GetAsync(RckDeltaId id, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(id);
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            return Task.FromResult(_deltas.TryGetValue(id.ToString(), out var existing) ? existing.Delta : null);
        }
    }

    public Task<bool> ExistsAsync(RckDeltaId id, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(id);
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            return Task.FromResult(_deltas.ContainsKey(id.ToString()));
        }
    }

    public Task<IReadOnlyList<RckDelta>> GetFromStateAsync(RckStateId stateId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stateId);
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            IReadOnlyList<RckDelta> result = _deltas.Values
                .Select(entry => entry.Delta)
                .Where(delta => delta.FromStateId == stateId)
                .ToArray();
            return Task.FromResult(result);
        }
    }

    public Task<IReadOnlyList<RckDelta>> GetToStateAsync(RckStateId stateId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stateId);
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            IReadOnlyList<RckDelta> result = _deltas.Values
                .Select(entry => entry.Delta)
                .Where(delta => delta.ToStateId == stateId)
                .ToArray();
            return Task.FromResult(result);
        }
    }

    public Task<IReadOnlyList<RckDelta>> ListAllAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            IReadOnlyList<RckDelta> result = _deltas.Values
                .Select(entry => entry.Delta)
                .OrderBy(delta => delta.Id.ToString(), StringComparer.Ordinal)
                .ToArray();
            return Task.FromResult(result);
        }
    }
}
