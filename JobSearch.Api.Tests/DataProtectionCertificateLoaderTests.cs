using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using JobSearch.Data;

namespace JobSearch.Api.Tests;

public class DataProtectionCertificateLoaderTests
{
    private const string TestPassword = "test-password";

    // TC01 — Both vars present and valid: returns a usable certificate. This is the
    // staging-today / eventual production path once DP_CERT_PFX_B64 and DP_CERT_PFX_PASSWORD
    // are set.
    [Fact]
    public void TryLoad_BothVarsPresentAndValid_ReturnsCertificate()
    {
        var pfxBase64 = Convert.ToBase64String(CreateSelfSignedPfx(TestPassword));

        var cert = DataProtectionCertificateLoader.TryLoad(pfxBase64, TestPassword);

        Assert.NotNull(cert);
        Assert.True(cert!.HasPrivateKey);
    }

    // TC02 — Both vars absent: this is the state of local dev and production today. Must
    // return null, not throw, so the caller's AddDataProtection() chain stays on the plain
    // Postgres-persisted, unprotected path exactly as it's always worked.
    [Fact]
    public void TryLoad_BothVarsAbsent_ReturnsNull()
    {
        Assert.Null(DataProtectionCertificateLoader.TryLoad(null, null));
        Assert.Null(DataProtectionCertificateLoader.TryLoad("", ""));
    }

    // TC03 — Only the cert is set, password missing (or vice versa): still treated as "not
    // configured" rather than attempting a load with a missing piece.
    [Fact]
    public void TryLoad_OnlyPfxSet_ReturnsNull()
    {
        var pfxBase64 = Convert.ToBase64String(CreateSelfSignedPfx(TestPassword));

        Assert.Null(DataProtectionCertificateLoader.TryLoad(pfxBase64, null));
        Assert.Null(DataProtectionCertificateLoader.TryLoad(pfxBase64, ""));
    }

    [Fact]
    public void TryLoad_OnlyPasswordSet_ReturnsNull()
    {
        Assert.Null(DataProtectionCertificateLoader.TryLoad(null, TestPassword));
        Assert.Null(DataProtectionCertificateLoader.TryLoad("", TestPassword));
    }

    // TC04 — Invalid base64 with both vars "present": a deliberate choice, not an accident.
    // This combination only happens once someone has actually set the vars, so a malformed
    // value is a real setup error and should fail startup loudly rather than silently
    // degrading to "feature off" and leaving the key ring unexpectedly unprotected.
    [Fact]
    public void TryLoad_InvalidBase64_Throws()
    {
        Assert.Throws<FormatException>(() =>
            DataProtectionCertificateLoader.TryLoad("not-valid-base64!!!", TestPassword));
    }

    // TC05 — Valid base64 but not a real PKCS#12 blob (or wrong password): same "loud
    // failure" reasoning as TC04 — this is a cryptography-layer error, not a config-absence
    // one, so it isn't swallowed either.
    [Fact]
    public void TryLoad_ValidBase64ButNotAPfx_Throws()
    {
        var garbageBase64 = Convert.ToBase64String("this is not a pfx file"u8.ToArray());

        Assert.Throws<CryptographicException>(() =>
            DataProtectionCertificateLoader.TryLoad(garbageBase64, TestPassword));
    }

    [Fact]
    public void TryLoad_WrongPassword_Throws()
    {
        var pfxBase64 = Convert.ToBase64String(CreateSelfSignedPfx(TestPassword));

        Assert.Throws<CryptographicException>(() =>
            DataProtectionCertificateLoader.TryLoad(pfxBase64, "wrong-password"));
    }

    private static byte[] CreateSelfSignedPfx(string password)
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest(
            "CN=DataProtectionCertificateLoaderTests", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var cert = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
        return cert.Export(X509ContentType.Pfx, password);
    }
}
