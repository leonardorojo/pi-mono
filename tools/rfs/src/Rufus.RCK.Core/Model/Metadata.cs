namespace Rufus.RCK.Core.Model;

/// <summary>
/// Shared operational metadata for RCK objects.
/// </summary>
public abstract record Metadata
{
    public DateTimeOffset CreatedAtUtc { get; }

    public string? CreatedBy { get; }

    public string? Label { get; }

    public string? Reason { get; }

    protected Metadata(DateTimeOffset createdAtUtc, string? createdBy = null, string? label = null, string? reason = null)
    {
        if (createdAtUtc == default)
        {
            throw new ArgumentException("CreatedAtUtc must be a valid UTC timestamp.", nameof(createdAtUtc));
        }

        CreatedAtUtc = createdAtUtc;
        CreatedBy = NormalizeOptional(createdBy, nameof(createdBy));
        Label = NormalizeOptional(label, nameof(label));
        Reason = NormalizeOptional(reason, nameof(reason));
    }

    private static string? NormalizeOptional(string? value, string paramName)
    {
        if (value is null)
        {
            return null;
        }

        return string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException($"{paramName} cannot be empty.", paramName)
            : value;
    }
}

public sealed record RckStateMeta : Metadata
{
    public RckStateMeta(DateTimeOffset createdAtUtc, string? createdBy = null, string? label = null, string? reason = null)
        : base(createdAtUtc, createdBy, label, reason)
    {
    }
}

public sealed record RckDeltaMeta : Metadata
{
    public RckDeltaMeta(DateTimeOffset createdAtUtc, string? createdBy = null, string? label = null, string? reason = null)
        : base(createdAtUtc, createdBy, label, reason)
    {
    }
}

public sealed record RckAnchorMeta : Metadata
{
    public RckAnchorMeta(DateTimeOffset createdAtUtc, string? createdBy = null, string? label = null, string? reason = null)
        : base(createdAtUtc, createdBy, label, reason)
    {
    }
}
