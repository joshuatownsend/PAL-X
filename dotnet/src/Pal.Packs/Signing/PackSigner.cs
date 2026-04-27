using System.Security.Cryptography;
using System.Text;

namespace Pal.Packs.Signing;

public sealed class PackSigner
{
    public void Sign(string packDirectory, string privateKeyPemPath)
    {
        string packYaml = Path.Combine(packDirectory, "pack.yaml");
        if (!File.Exists(packYaml))
            throw new PackSignatureException($"No pack.yaml found in '{packDirectory}'.");

        byte[] packBytes = File.ReadAllBytes(packYaml);

        using var rsa = RSA.Create();
        string pem = File.ReadAllText(privateKeyPemPath);
        try
        {
            rsa.ImportFromPem(pem);
        }
        catch (Exception ex)
        {
            throw new PackSignatureException(
                $"Could not load private key from '{privateKeyPemPath}': {ex.Message}", ex);
        }

        string signature = SignToBase64(packBytes, rsa);
        string keyId = ComputeKeyId(rsa);

        string sidecar = BuildSidecar(keyId, signature);
        string sigPath = packYaml + ".sig";
        File.WriteAllText(sigPath, sidecar, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    public string SignToBase64(byte[] packBytes, RSA privateKey) =>
        Convert.ToBase64String(privateKey.SignData(packBytes,
            HashAlgorithmName.SHA256, RSASignaturePadding.Pss));

    internal static string ComputeKeyId(RSA rsa)
    {
        byte[] pubKeyBytes = rsa.ExportSubjectPublicKeyInfo();
        byte[] hash = SHA256.HashData(pubKeyBytes);
        return Convert.ToHexString(hash)[..8].ToLowerInvariant();
    }

    private static string BuildSidecar(string keyId, string signature) =>
        $"# pal-pack-signature/v1 alg=rsa-pss-sha256 keyid={keyId}\n{signature}\n";
}
