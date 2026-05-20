using Rufus.RCK.Core.Hashing;

namespace Rufus.RCK.Core.Model;

public sealed record RckStateId
{
    public RckHash Value { get; }

    public RckStateId(RckHash value)
    {
        Value = value ?? throw new ArgumentNullException(nameof(value));
    }

    public override string ToString() => Value.ToString();
}
