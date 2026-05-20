using Rufus.RCK.Core.Hashing;

namespace Rufus.RCK.Core.Model;

public sealed record RckRef
{
    public string Id { get; }

    public string Kind { get; }

    public Uri Uri { get; }

    public RckHash? Hash { get; }

    public string? MediaType { get; }

    public Metadata? Meta { get; }

    public RckRef(string id, string kind, Uri uri, RckHash? hash = null, string? mediaType = null, Metadata? meta = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(kind);
        Uri = uri ?? throw new ArgumentNullException(nameof(uri));

        if (!Uri.IsAbsoluteUri)
        {
            throw new ArgumentException("Uri must be absolute.", nameof(uri));
        }

        if (mediaType is not null && string.IsNullOrWhiteSpace(mediaType))
        {
            throw new ArgumentException("MediaType cannot be empty.", nameof(mediaType));
        }

        Id = id;
        Kind = kind;
        Hash = hash;
        MediaType = mediaType;
        Meta = meta;
    }
}
