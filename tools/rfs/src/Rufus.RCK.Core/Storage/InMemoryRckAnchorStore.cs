using Rufus.RCK.Core.Model;

namespace Rufus.RCK.Core.Storage;

public sealed class InMemoryRckAnchorStore : IRckAnchorStore
{
    private sealed record StoredAnchor(RckAnchor Anchor, string Fingerprint);

    private readonly object _gate = new();
    private readonly Dictionary<string, StoredAnchor> _anchors = new(StringComparer.Ordinal);

    public Task SaveAsync(RckAnchor anchor, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(anchor);
        cancellationToken.ThrowIfCancellationRequested();

        var key = anchor.Id.ToString();
        var fingerprint = RckStorageFingerprint.ForAnchor(anchor);

        lock (_gate)
        {
            if (_anchors.TryGetValue(key, out var existing))
            {
                if (!string.Equals(existing.Fingerprint, fingerprint, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException($"Anchor {key} already exists with different content.");
                }

                return Task.CompletedTask;
            }

            _anchors[key] = new StoredAnchor(anchor, fingerprint);
            return Task.CompletedTask;
        }
    }

    public Task<RckAnchor?> GetAsync(RckAnchorId id, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(id);
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            return Task.FromResult(_anchors.TryGetValue(id.ToString(), out var existing) ? existing.Anchor : null);
        }
    }

    public Task<bool> ExistsAsync(RckAnchorId id, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(id);
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            return Task.FromResult(_anchors.ContainsKey(id.ToString()));
        }
    }

    public Task<IReadOnlyList<RckAnchor>> GetByStateAsync(RckStateId stateId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stateId);
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            IReadOnlyList<RckAnchor> result = _anchors.Values
                .Select(entry => entry.Anchor)
                .Where(anchor => anchor.StateId == stateId)
                .ToArray();
            return Task.FromResult(result);
        }
    }

    public Task<IReadOnlyList<RckAnchor>> ListAllAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            IReadOnlyList<RckAnchor> result = _anchors.Values
                .Select(entry => entry.Anchor)
                .OrderBy(anchor => anchor.Id.ToString(), StringComparer.Ordinal)
                .ToArray();
            return Task.FromResult(result);
        }
    }
}
