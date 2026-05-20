using System.Security.Cryptography;
using System.Text;

namespace Rufus.RCK.Core.Hashing;

/// <summary>
/// Deterministic SHA-256 hashing helpers.
/// </summary>
public static class RckHasher
{
    public static RckHash Hash(string utf8Text)
    {
        ArgumentNullException.ThrowIfNull(utf8Text);
        return Hash(Encoding.UTF8.GetBytes(utf8Text));
    }

    public static RckHash Hash(byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        return new RckHash(ConvertToHex(SHA256.HashData(bytes)));
    }

    public static RckHash HashJson(string canonicalJson) => Hash(canonicalJson);

    private static string ConvertToHex(byte[] bytes) => Convert.ToHexString(bytes).ToLowerInvariant();
}
