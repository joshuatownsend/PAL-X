using System.Security.Cryptography;

namespace Pal.Packs.Signing;

public sealed class PackVerifier
{
    public sealed record VerificationResult(bool IsValid, string? KeyId, string? FailureReason);

    public VerificationResult Verify(string packDirectory, IReadOnlyList<RSA> trustedKeys)
    {
        string packYaml = Path.Combine(packDirectory, "pack.yaml");
        string sigPath = packYaml + ".sig";

        if (!File.Exists(sigPath))
            return new VerificationResult(false, null, "MissingSignature");

        string sidecar = File.ReadAllText(sigPath).Trim();
        var (keyId, signatureBase64) = ParseSidecar(sigPath, sidecar);

        byte[] signature;
        try
        {
            signature = Convert.FromBase64String(signatureBase64);
        }
        catch (FormatException)
        {
            return new VerificationResult(false, keyId, "MalformedSignature");
        }

        byte[] packBytes = File.ReadAllBytes(packYaml);

        foreach (var key in trustedKeys)
        {
            if (key.VerifyData(packBytes, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pss))
                return new VerificationResult(true, keyId, null);
        }

        return new VerificationResult(false, keyId,
            trustedKeys.Count == 0 ? "NoTrustedKeys" : "KeyNotTrusted");
    }

    private static (string? keyId, string signatureBase64) ParseSidecar(string path, string sidecar)
    {
        var lines = sidecar.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length < 2)
            throw new PackSignatureException($"Sidecar '{path}' is malformed: expected at least 2 lines.");

        string? keyId = null;
        string header = lines[0];
        if (header.StartsWith("# pal-pack-signature/v1", StringComparison.Ordinal))
        {
            int kIdx = header.IndexOf("keyid=", StringComparison.Ordinal);
            if (kIdx >= 0)
            {
                string rest = header[(kIdx + 6)..].Trim();
                int spaceIdx = rest.IndexOf(' ');
                keyId = spaceIdx >= 0 ? rest[..spaceIdx] : rest;
            }
        }

        return (keyId, lines[1]);
    }
}
