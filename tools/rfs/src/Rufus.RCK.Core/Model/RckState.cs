using System.Text.Json;
using Rufus.RCK.Core.Hashing;

namespace Rufus.RCK.Core.Model;

public sealed record RckState
{
    public RckStateId Id { get; }

    public string PayloadCanonicalJson { get; }

    public IReadOnlyList<RckRef> Refs { get; }

    public RckStateMeta Meta { get; }

    private RckState(RckStateId id, string payloadCanonicalJson, IReadOnlyList<RckRef> refs, RckStateMeta meta)
    {
        Id = id ?? throw new ArgumentNullException(nameof(id));
        PayloadCanonicalJson = RckCanonicalJson.Canonicalize(payloadCanonicalJson);
        Refs = refs;
        Meta = meta;
    }

    public static RckState Create(string payloadJson, IEnumerable<RckRef>? refs = null, RckStateMeta? meta = null)
    {
        var canonicalPayload = RckCanonicalJson.Canonicalize(payloadJson);
        var refList = (refs ?? Array.Empty<RckRef>()).ToArray();
        var canonical = BuildCanonicalStateInput(canonicalPayload, refList);
        var id = new RckStateId(RckHasher.HashJson(canonical));
        return new RckState(id, canonicalPayload, refList, meta ?? new RckStateMeta(DateTimeOffset.UtcNow));
    }

    internal static RckState Rehydrate(RckStateId id, string payloadCanonicalJson, IReadOnlyList<RckRef> refs, RckStateMeta meta)
        => new(id, payloadCanonicalJson, refs, meta);

    private static string BuildCanonicalStateInput(string canonicalPayloadJson, IReadOnlyList<RckRef> refs)
    {
        var projection = new
        {
            Payload = canonicalPayloadJson,
            Refs = refs.Select(r => new
            {
                r.Id,
                r.Kind,
                Uri = r.Uri.ToString(),
                Hash = r.Hash?.Value,
                r.MediaType,
            }).ToArray(),
        };

        return JsonSerializer.Serialize(projection);
    }
}
