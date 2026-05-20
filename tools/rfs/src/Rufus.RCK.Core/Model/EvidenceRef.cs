using Rufus.RCK.Core.Hashing;

namespace Rufus.RCK.Core.Model;

public sealed record EvidenceRef
{
    public string Id { get; }

    public string Kind { get; }

    public RckRef Ref { get; }

    public string? Summary { get; }

    public RckHash? Hash { get; }

    public EvidenceRef(string id, string kind, RckRef @ref, string? summary = null, RckHash? hash = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(kind);
        Ref = @ref ?? throw new ArgumentNullException(nameof(@ref));

        if (summary is not null && string.IsNullOrWhiteSpace(summary))
        {
            throw new ArgumentException("Summary cannot be empty.", nameof(summary));
        }

        Id = id;
        Kind = kind;
        Summary = summary;
        Hash = hash;
    }
}
