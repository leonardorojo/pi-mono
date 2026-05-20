using System.Text.Json;
using Rufus.RCK.Core.Hashing;

namespace Rufus.RCK.Core.Model;

public sealed record RckDelta
{
    public RckDeltaId Id { get; }

    public RckStateId FromStateId { get; }

    public RckStateId ToStateId { get; }

    public IReadOnlyList<PatchOp> Ops { get; }

    public IReadOnlyList<RckRef> Refs { get; }

    public IReadOnlyList<EvidenceRef> EvidenceRefs { get; }

    public RckDeltaMeta Meta { get; }

    private RckDelta(
        RckDeltaId id,
        RckStateId fromStateId,
        RckStateId toStateId,
        IReadOnlyList<PatchOp> ops,
        IReadOnlyList<RckRef> refs,
        IReadOnlyList<EvidenceRef> evidenceRefs,
        RckDeltaMeta meta)
    {
        Id = id ?? throw new ArgumentNullException(nameof(id));
        FromStateId = fromStateId ?? throw new ArgumentNullException(nameof(fromStateId));
        ToStateId = toStateId ?? throw new ArgumentNullException(nameof(toStateId));
        Ops = ops;
        Refs = refs;
        EvidenceRefs = evidenceRefs;
        Meta = meta;
    }

    public static RckDelta Create(
        RckStateId fromStateId,
        RckStateId toStateId,
        IEnumerable<PatchOp>? ops = null,
        IEnumerable<RckRef>? refs = null,
        IEnumerable<EvidenceRef>? evidenceRefs = null,
        RckDeltaMeta? meta = null)
    {
        ArgumentNullException.ThrowIfNull(fromStateId);
        ArgumentNullException.ThrowIfNull(toStateId);

        var opList = (ops ?? Array.Empty<PatchOp>()).ToArray();
        var refList = (refs ?? Array.Empty<RckRef>()).ToArray();
        var evidenceList = (evidenceRefs ?? Array.Empty<EvidenceRef>()).ToArray();
        var canonical = BuildCanonicalDeltaInput(fromStateId, toStateId, opList, refList, evidenceList);
        var id = new RckDeltaId(RckHasher.HashJson(canonical));
        return new RckDelta(id, fromStateId, toStateId, opList, refList, evidenceList, meta ?? new RckDeltaMeta(DateTimeOffset.UtcNow));
    }

    internal static RckDelta Rehydrate(
        RckDeltaId id,
        RckStateId fromStateId,
        RckStateId toStateId,
        IReadOnlyList<PatchOp> ops,
        IReadOnlyList<RckRef> refs,
        IReadOnlyList<EvidenceRef> evidenceRefs,
        RckDeltaMeta meta)
        => new(id, fromStateId, toStateId, ops, refs, evidenceRefs, meta);

    private static string BuildCanonicalDeltaInput(
        RckStateId fromStateId,
        RckStateId toStateId,
        IReadOnlyList<PatchOp> ops,
        IReadOnlyList<RckRef> refs,
        IReadOnlyList<EvidenceRef> evidenceRefs)
    {
        var projection = new
        {
            FromStateId = fromStateId.Value.Value,
            ToStateId = toStateId.Value.Value,
            Ops = ops.Select(op => new
            {
                op.Kind,
                op.Path,
                op.ValueJson,
            }).ToArray(),
            Refs = refs.Select(r => new
            {
                r.Id,
                r.Kind,
                Uri = r.Uri.ToString(),
                Hash = r.Hash?.Value,
                r.MediaType,
            }).ToArray(),
            EvidenceRefs = evidenceRefs.Select(e => new
            {
                e.Id,
                e.Kind,
                Ref = new
                {
                    e.Ref.Id,
                    e.Ref.Kind,
                    Uri = e.Ref.Uri.ToString(),
                    Hash = e.Ref.Hash?.Value,
                    e.Ref.MediaType,
                },
                e.Summary,
                Hash = e.Hash?.Value,
            }).ToArray(),
        };

        return JsonSerializer.Serialize(projection);
    }
}
