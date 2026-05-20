using Rufus.RCK.Core.Model;
using Rufus.RCK.Core.Storage;

namespace Rufus.RCK.Core.Tracing;

public sealed class RckTraceNavigator
{
    private readonly IRckStateStore _stateStore;
    private readonly IRckDeltaStore _deltaStore;
    private readonly IRckAnchorStore _anchorStore;

    public RckTraceNavigator(IRckStateStore stateStore, IRckDeltaStore deltaStore, IRckAnchorStore anchorStore)
    {
        _stateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));
        _deltaStore = deltaStore ?? throw new ArgumentNullException(nameof(deltaStore));
        _anchorStore = anchorStore ?? throw new ArgumentNullException(nameof(anchorStore));
    }

    public async Task<IReadOnlyList<RckDelta>> GetOutgoingDeltasAsync(RckStateId stateId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stateId);
        cancellationToken.ThrowIfCancellationRequested();

        var deltas = await _deltaStore.GetFromStateAsync(stateId, cancellationToken).ConfigureAwait(false);
        return deltas.OrderBy(delta => delta.Id.ToString(), StringComparer.Ordinal).ToArray();
    }

    public async Task<IReadOnlyList<RckDelta>> GetIncomingDeltasAsync(RckStateId stateId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stateId);
        cancellationToken.ThrowIfCancellationRequested();

        var deltas = await _deltaStore.GetToStateAsync(stateId, cancellationToken).ConfigureAwait(false);
        return deltas.OrderBy(delta => delta.Id.ToString(), StringComparer.Ordinal).ToArray();
    }

    public async Task<IReadOnlyList<RckAnchor>> GetAnchorsForStateAsync(RckStateId stateId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stateId);
        cancellationToken.ThrowIfCancellationRequested();

        var anchors = await _anchorStore.GetByStateAsync(stateId, cancellationToken).ConfigureAwait(false);
        return anchors.OrderBy(anchor => anchor.Id.ToString(), StringComparer.Ordinal).ToArray();
    }

    public async Task<IReadOnlyList<RckStateId>> GetAncestorsAsync(RckStateId stateId, int maxDepth, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stateId);
        ValidateMaxDepth(maxDepth);
        cancellationToken.ThrowIfCancellationRequested();

        return await TraverseAsync(stateId, maxDepth, GetIncomingNeighborsAsync, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<RckStateId>> GetDescendantsAsync(RckStateId stateId, int maxDepth, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stateId);
        ValidateMaxDepth(maxDepth);
        cancellationToken.ThrowIfCancellationRequested();

        return await TraverseAsync(stateId, maxDepth, GetOutgoingNeighborsAsync, cancellationToken).ConfigureAwait(false);
    }

    public async Task<RckTracePath?> TryGetPathAsync(RckStateId fromStateId, RckStateId toStateId, int maxDepth, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(fromStateId);
        ArgumentNullException.ThrowIfNull(toStateId);
        ValidateMaxDepth(maxDepth);
        cancellationToken.ThrowIfCancellationRequested();

        if (fromStateId == toStateId)
        {
            return new RckTracePath(fromStateId, toStateId, [fromStateId], Array.Empty<RckDeltaId>());
        }

        var queue = new Queue<PathNode>();
        var visited = new HashSet<string>(StringComparer.Ordinal) { fromStateId.ToString() };
        queue.Enqueue(new PathNode(fromStateId, [fromStateId], Array.Empty<RckDeltaId>(), Depth: 0));

        while (queue.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var current = queue.Dequeue();
            if (current.Depth >= maxDepth)
            {
                continue;
            }

            var neighbors = await GetOutgoingNeighborsAsync(current.StateId, cancellationToken).ConfigureAwait(false);
            foreach (var neighbor in neighbors)
            {
                var neighborKey = neighbor.StateId.ToString();
                if (!visited.Add(neighborKey))
                {
                    continue;
                }

                var nextStateIds = current.StateIds.Concat([neighbor.StateId]).ToArray();
                var nextDeltaIds = current.DeltaIds.Concat([neighbor.DeltaId]).ToArray();
                if (neighbor.StateId == toStateId)
                {
                    return new RckTracePath(fromStateId, toStateId, nextStateIds, nextDeltaIds);
                }

                queue.Enqueue(new PathNode(neighbor.StateId, nextStateIds, nextDeltaIds, current.Depth + 1));
            }
        }

        return null;
    }

    public async Task<RckTraceValidationResult> ValidateAsync(
        IEnumerable<RckStateId> knownStateIds,
        IEnumerable<RckDeltaId> knownDeltaIds,
        IEnumerable<RckAnchorId> knownAnchorIds,
        CancellationToken cancellationToken = default)
    {
        _ = knownStateIds;
        _ = knownDeltaIds;
        _ = knownAnchorIds;

        cancellationToken.ThrowIfCancellationRequested();

        var issues = new List<RckTraceValidationIssue>();
        var states = (await _stateStore.ListAllAsync(cancellationToken).ConfigureAwait(false)).ToDictionary(state => state.Id.ToString(), StringComparer.Ordinal);
        var deltas = (await _deltaStore.ListAllAsync(cancellationToken).ConfigureAwait(false)).ToDictionary(delta => delta.Id.ToString(), StringComparer.Ordinal);
        var anchors = (await _anchorStore.ListAllAsync(cancellationToken).ConfigureAwait(false)).ToDictionary(anchor => anchor.Id.ToString(), StringComparer.Ordinal);

        foreach (var delta in deltas.Values.OrderBy(delta => delta.Id.ToString(), StringComparer.Ordinal))
        {
            if (!states.ContainsKey(delta.FromStateId.ToString()))
            {
                issues.Add(new RckTraceValidationIssue(
                    RckTraceValidationIssueCodes.MissingDeltaFromState,
                    $"Delta {delta.Id} references missing from-state {delta.FromStateId}.",
                    delta.Id.ToString()));
            }

            if (!states.ContainsKey(delta.ToStateId.ToString()))
            {
                issues.Add(new RckTraceValidationIssue(
                    RckTraceValidationIssueCodes.MissingDeltaToState,
                    $"Delta {delta.Id} references missing to-state {delta.ToStateId}.",
                    delta.Id.ToString()));
            }
        }

        foreach (var anchor in anchors.Values.OrderBy(anchor => anchor.Id.ToString(), StringComparer.Ordinal))
        {
            if (!states.ContainsKey(anchor.StateId.ToString()))
            {
                issues.Add(new RckTraceValidationIssue(
                    RckTraceValidationIssueCodes.MissingAnchorState,
                    $"Anchor {anchor.Id} references missing state {anchor.StateId}.",
                    anchor.Id.ToString()));
            }

            foreach (var parentId in anchor.ParentAnchorIds.OrderBy(id => id.ToString(), StringComparer.Ordinal))
            {
                if (!anchors.ContainsKey(parentId.ToString()))
                {
                    issues.Add(new RckTraceValidationIssue(
                        RckTraceValidationIssueCodes.MissingAnchorParent,
                        $"Anchor {anchor.Id} references missing parent anchor {parentId}.",
                        anchor.Id.ToString()));
                }
            }
        }

        if (HasCycle(states.Keys, BuildStateAdjacency(deltas.Values), StringComparer.Ordinal))
        {
            issues.Add(new RckTraceValidationIssue(
                RckTraceValidationIssueCodes.DeltaCycleDetected,
                "A delta cycle was detected in the state graph."));
        }

        if (HasCycle(anchors.Keys, BuildAnchorAdjacency(anchors.Values), StringComparer.Ordinal))
        {
            issues.Add(new RckTraceValidationIssue(
                RckTraceValidationIssueCodes.AnchorParentCycleDetected,
                "A cycle was detected in anchor parent lineage."));
        }

        return new RckTraceValidationResult(issues);
    }

    private async Task<IReadOnlyList<RckStateId>> TraverseAsync(
        RckStateId seed,
        int maxDepth,
        Func<RckStateId, CancellationToken, Task<IReadOnlyList<Neighbor>>> getNeighborsAsync,
        CancellationToken cancellationToken)
    {
        var result = new List<RckStateId>();
        var visited = new HashSet<string>(StringComparer.Ordinal) { seed.ToString() };
        var currentLevel = new List<RckStateId> { seed };

        for (var depth = 0; depth < maxDepth; depth++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var nextLevel = new List<Neighbor>();
            foreach (var stateId in currentLevel)
            {
                var neighbors = await getNeighborsAsync(stateId, cancellationToken).ConfigureAwait(false);
                nextLevel.AddRange(neighbors);
            }

            var orderedNextLevel = nextLevel
                .OrderBy(neighbor => neighbor.StateId.ToString(), StringComparer.Ordinal)
                .ThenBy(neighbor => neighbor.DeltaId.ToString(), StringComparer.Ordinal)
                .ToArray();

            var uniqueNextLevel = new List<RckStateId>();
            foreach (var neighbor in orderedNextLevel)
            {
                var key = neighbor.StateId.ToString();
                if (visited.Add(key))
                {
                    result.Add(neighbor.StateId);
                    uniqueNextLevel.Add(neighbor.StateId);
                }
            }

            if (uniqueNextLevel.Count == 0)
            {
                break;
            }

            currentLevel = uniqueNextLevel;
        }

        return result;
    }

    private async Task<IReadOnlyList<Neighbor>> GetOutgoingNeighborsAsync(RckStateId stateId, CancellationToken cancellationToken)
    {
        var deltas = await _deltaStore.GetFromStateAsync(stateId, cancellationToken).ConfigureAwait(false);
        return deltas
            .Select(delta => new Neighbor(delta.ToStateId, delta.Id))
            .OrderBy(neighbor => neighbor.StateId.ToString(), StringComparer.Ordinal)
            .ThenBy(neighbor => neighbor.DeltaId.ToString(), StringComparer.Ordinal)
            .ToArray();
    }

    private async Task<IReadOnlyList<Neighbor>> GetIncomingNeighborsAsync(RckStateId stateId, CancellationToken cancellationToken)
    {
        var deltas = await _deltaStore.GetToStateAsync(stateId, cancellationToken).ConfigureAwait(false);
        return deltas
            .Select(delta => new Neighbor(delta.FromStateId, delta.Id))
            .OrderBy(neighbor => neighbor.StateId.ToString(), StringComparer.Ordinal)
            .ThenBy(neighbor => neighbor.DeltaId.ToString(), StringComparer.Ordinal)
            .ToArray();
    }

    private static Dictionary<string, IReadOnlyList<string>> BuildStateAdjacency(IEnumerable<RckDelta> deltas)
    {
        var map = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var delta in deltas)
        {
            var from = delta.FromStateId.ToString();
            var to = delta.ToStateId.ToString();
            if (!map.TryGetValue(from, out var edges))
            {
                edges = new List<string>();
                map[from] = edges;
            }

            edges.Add(to);
        }

        return map.ToDictionary(
            pair => pair.Key,
            pair => (IReadOnlyList<string>)pair.Value.OrderBy(value => value, StringComparer.Ordinal).ToArray(),
            StringComparer.Ordinal);
    }

    private static Dictionary<string, IReadOnlyList<string>> BuildAnchorAdjacency(IEnumerable<RckAnchor> anchors)
    {
        var map = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var anchor in anchors)
        {
            var id = anchor.Id.ToString();
            if (!map.TryGetValue(id, out var edges))
            {
                edges = new List<string>();
                map[id] = edges;
            }

            edges.AddRange(anchor.ParentAnchorIds.Select(parent => parent.ToString()));
        }

        return map.ToDictionary(
            pair => pair.Key,
            pair => (IReadOnlyList<string>)pair.Value.OrderBy(value => value, StringComparer.Ordinal).ToArray(),
            StringComparer.Ordinal);
    }

    private static bool HasCycle(IEnumerable<string> nodes, IReadOnlyDictionary<string, IReadOnlyList<string>> adjacency, StringComparer comparer)
    {
        var color = new Dictionary<string, VisitColor>(comparer);
        foreach (var node in nodes.OrderBy(node => node, comparer))
        {
            if (DetectCycle(node, adjacency, color, comparer))
            {
                return true;
            }
        }

        return false;
    }

    private static bool DetectCycle(
        string node,
        IReadOnlyDictionary<string, IReadOnlyList<string>> adjacency,
        Dictionary<string, VisitColor> color,
        StringComparer comparer)
    {
        if (color.TryGetValue(node, out var existing))
        {
            return existing == VisitColor.Gray;
        }

        color[node] = VisitColor.Gray;
        if (adjacency.TryGetValue(node, out var edges))
        {
            foreach (var neighbor in edges.OrderBy(edge => edge, comparer))
            {
                if (DetectCycle(neighbor, adjacency, color, comparer))
                {
                    return true;
                }
            }
        }

        color[node] = VisitColor.Black;
        return false;
    }

    private static void ValidateMaxDepth(int maxDepth)
    {
        if (maxDepth < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxDepth));
        }
    }

    private readonly record struct PathNode(RckStateId StateId, IReadOnlyList<RckStateId> StateIds, IReadOnlyList<RckDeltaId> DeltaIds, int Depth);

    private readonly record struct Neighbor(RckStateId StateId, RckDeltaId DeltaId);

    private enum VisitColor
    {
        White,
        Gray,
        Black,
    }
}
