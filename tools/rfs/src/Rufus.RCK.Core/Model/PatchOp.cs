namespace Rufus.RCK.Core.Model;

public enum PatchOpKind
{
    Add = 0,
    Remove = 1,
    Replace = 2,
}

public sealed record PatchOp
{
    public PatchOpKind Kind { get; }

    public string Path { get; }

    public string? ValueJson { get; }

    public PatchOp(PatchOpKind kind, string path, string? valueJson = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (valueJson is not null && valueJson.Length == 0)
        {
            throw new ArgumentException("ValueJson cannot be empty when provided.", nameof(valueJson));
        }

        Kind = kind;
        Path = path;
        ValueJson = valueJson;
    }
}
