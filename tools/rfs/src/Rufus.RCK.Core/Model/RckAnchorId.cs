using Rufus.RCK.Core.Hashing;

namespace Rufus.RCK.Core.Model;

public sealed record RckAnchorId
{
    public RckHash Value { get; }

    public RckAnchorId(RckHash value)
    {
        Value = value ?? throw new ArgumentNullException(nameof(value));
    }

    public override string ToString() => Value.ToString();
}
