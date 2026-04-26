using System.Security.Cryptography;
using System.Text;

namespace Pal.Api.Auth;

internal static class TokenHasher
{
    internal static string Hash(string rawToken)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawToken));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
