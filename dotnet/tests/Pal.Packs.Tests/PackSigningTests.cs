using System.Security.Cryptography;
using System.Text;
using Pal.Packs.Signing;
using Xunit;

namespace Pal.Packs.Tests;

public class PackSigningTests : IDisposable
{
    private readonly string _tempDir;

    public PackSigningTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"pal-sign-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);

        // Write a minimal valid pack.yaml for testing
        File.WriteAllText(Path.Combine(_tempDir, "pack.yaml"), """
            schema_version: "pal.pack/v1"
            pack_id: test-signing-pack
            pack_name: Test Signing Pack
            version: 1.0.0
            rules: []
            """, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    public void Dispose() => Directory.Delete(_tempDir, recursive: true);

    // ── helpers ─────────────────────────────────────────────────────────────

    private static (string privKeyPath, RSA publicKey) MakeTestKeyPair(string dir)
    {
        using var rsa = RSA.Create(3072);
        string privPem = rsa.ExportPkcs8PrivateKeyPem();
        string privPath = Path.Combine(dir, "test.priv.pem");
        File.WriteAllText(privPath, privPem, new UTF8Encoding(false));

        var publicKey = RSA.Create();
        publicKey.ImportSubjectPublicKeyInfo(rsa.ExportSubjectPublicKeyInfo(), out _);
        return (privPath, publicKey);
    }

    // ── tests ───────────────────────────────────────────────────────────────

    [Fact]
    public void Sign_WritesSidecarFile()
    {
        var (privPath, pub) = MakeTestKeyPair(_tempDir);
        pub.Dispose();

        var signer = new PackSigner();
        signer.Sign(_tempDir, privPath);

        Assert.True(File.Exists(Path.Combine(_tempDir, "pack.yaml.sig")));
    }

    [Fact]
    public void Verify_RoundTrip_Succeeds()
    {
        var (privPath, pub) = MakeTestKeyPair(_tempDir);

        var signer = new PackSigner();
        signer.Sign(_tempDir, privPath);

        var verifier = new PackVerifier();
        var result = verifier.Verify(_tempDir, [pub]);

        pub.Dispose();
        Assert.True(result.IsValid);
        Assert.Null(result.FailureReason);
    }

    [Fact]
    public void Verify_TamperedPackYaml_Fails()
    {
        var (privPath, pub) = MakeTestKeyPair(_tempDir);

        var signer = new PackSigner();
        signer.Sign(_tempDir, privPath);

        // Tamper with the pack
        string packPath = Path.Combine(_tempDir, "pack.yaml");
        File.AppendAllText(packPath, "\n# tampered\n");

        var verifier = new PackVerifier();
        var result = verifier.Verify(_tempDir, [pub]);

        pub.Dispose();
        Assert.False(result.IsValid);
        Assert.Equal("InvalidSignature", result.FailureReason);
    }

    [Fact]
    public void Verify_UntrustedKey_Fails()
    {
        var (privPath, pub) = MakeTestKeyPair(_tempDir);
        pub.Dispose();

        var signer = new PackSigner();
        signer.Sign(_tempDir, privPath);

        // Verify with a different (untrusted) key
        using var untrustedKey = RSA.Create(3072);
        var verifier = new PackVerifier();
        var result = verifier.Verify(_tempDir, [untrustedKey]);

        Assert.False(result.IsValid);
        Assert.Equal("InvalidSignature", result.FailureReason);
    }

    [Fact]
    public void Verify_MissingSidecar_ReturnsMissingSignature()
    {
        using var anyKey = RSA.Create(3072);
        var verifier = new PackVerifier();
        var result = verifier.Verify(_tempDir, [anyKey]);

        Assert.False(result.IsValid);
        Assert.Equal("MissingSignature", result.FailureReason);
    }

    [Fact]
    public void Verify_NoTrustedKeys_ReturnsNoTrustedKeys()
    {
        var (privPath, pub) = MakeTestKeyPair(_tempDir);
        pub.Dispose();

        var signer = new PackSigner();
        signer.Sign(_tempDir, privPath);

        var verifier = new PackVerifier();
        var result = verifier.Verify(_tempDir, []);

        Assert.False(result.IsValid);
        Assert.Equal("NoTrustedKeys", result.FailureReason);
    }

    [Fact]
    public void PackLoader_RequiredSignature_Throws_WhenMissing()
    {
        var loader = new PackLoader();
        var ex = Assert.Throws<PackSignatureException>(
            () => loader.LoadFromDirectory(_tempDir, SignatureRequirement.Required, []));
        Assert.Contains("no pack.yaml.sig", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void PackLoader_RequiredSignature_Passes_WhenValid()
    {
        var (privPath, pub) = MakeTestKeyPair(_tempDir);

        var signer = new PackSigner();
        signer.Sign(_tempDir, privPath);

        var loader = new PackLoader();
        var pack = loader.LoadFromDirectory(_tempDir, SignatureRequirement.Required, [pub]);

        pub.Dispose();
        Assert.Equal("test-signing-pack", pack.PackId);
    }
}
