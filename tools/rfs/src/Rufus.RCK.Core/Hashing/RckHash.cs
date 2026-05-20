using System.Globalization;
using System.Text.RegularExpressions;

namespace Rufus.RCK.Core.Hashing;

/// <summary>
/// Immutable SHA-256 hex value object.
/// </summary>
public sealed record RckHash
{
    private static readonly Regex HexRegex = new("^[0-9a-fA-F]{64}$", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public string Value { get; }

    public RckHash(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        if (!HexRegex.IsMatch(value))
        {
            throw new ArgumentException("RckHash must be a 64-character hexadecimal SHA-256 value.", nameof(value));
        }

        Value = value.ToLowerInvariant();
    }

    public override string ToString() => Value;

    public static RckHash FromHex(string value) => new(value);
}
