using System.Text.Json;
using Rufus.RCK.Core.Model;

namespace Rufus.RCK.Core.Storage;

public static class RckStorageFingerprint
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public static string ForState(RckState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        var projection = new
        {
            Payload = state.PayloadCanonicalJson.Trim(),
            Refs = state.Refs.Select(ToRefProjection).ToArray(),
        };

        return JsonSerializer.Serialize(projection, Options);
    }

    public static string ForDelta(RckDelta delta)
    {
        ArgumentNullException.ThrowIfNull(delta);

        var projection = new
        {
            FromStateId = delta.FromStateId.Value.Value,
            ToStateId = delta.ToStateId.Value.Value,
            Ops = delta.Ops.Select(op => new
            {
                op.Kind,
                op.Path,
                op.ValueJson,
            }).ToArray(),
            Refs = delta.Refs.Select(ToRefProjection).ToArray(),
            EvidenceRefs = delta.EvidenceRefs.Select(ToEvidenceProjection).ToArray(),
        };

        return JsonSerializer.Serialize(projection, Options);
    }

    public static string ForAnchor(RckAnchor anchor)
    {
        ArgumentNullException.ThrowIfNull(anchor);

        var projection = new
        {
            StateId = anchor.StateId.Value.Value,
            ParentAnchorIds = anchor.ParentAnchorIds.Select(id => id.Value.Value).ToArray(),
            Label = anchor.Meta.Label,
        };

        return JsonSerializer.Serialize(projection, Options);
    }

    private static object ToRefProjection(RckRef r) => new
    {
        r.Id,
        r.Kind,
        Uri = r.Uri.ToString(),
        Hash = r.Hash?.Value,
        r.MediaType,
    };

    private static object ToEvidenceProjection(EvidenceRef e) => new
    {
        e.Id,
        e.Kind,
        Ref = ToRefProjection(e.Ref),
        e.Summary,
        Hash = e.Hash?.Value,
    };
}
