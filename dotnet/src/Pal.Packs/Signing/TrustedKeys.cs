using System.Security.Cryptography;

namespace Pal.Packs.Signing;

public static class TrustedKeys
{
    // Empty in dev builds. Replace with a production RSA-3072 public key PEM before release.
    // Format: standard PEM BEGIN PUBLIC KEY / END PUBLIC KEY block (SPKI / SubjectPublicKeyInfo).
    public const string OfficialPublicKeyPem = "";

    public static RSA? LoadOfficialKey()
    {
        if (string.IsNullOrWhiteSpace(OfficialPublicKeyPem))
            return null;

        var rsa = RSA.Create();
        rsa.ImportFromPem(OfficialPublicKeyPem);
        return rsa;
    }

    public static RSA LoadFromPem(string pemPath)
    {
        string pem = File.ReadAllText(pemPath);
        var rsa = RSA.Create();
        try
        {
            rsa.ImportFromPem(pem);
            return rsa;
        }
        catch (Exception ex)
        {
            rsa.Dispose();
            throw new PackSignatureException($"Could not load RSA public key from '{pemPath}': {ex.Message}", ex);
        }
    }

    public static IReadOnlyList<RSA> DefaultTrusted(string? extraKeyPemPath = null)
    {
        var keys = new List<RSA>();

        var official = LoadOfficialKey();
        if (official is not null)
            keys.Add(official);

        if (!string.IsNullOrWhiteSpace(extraKeyPemPath))
            keys.Add(LoadFromPem(extraKeyPemPath));

        return keys;
    }
}
