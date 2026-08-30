using AdminDashboard.Api.Services;

namespace AdminDashboard.Api.Tests;

public class AdminSecretVerifierTests
{
    [Fact]
    public void MatchingSecret_IsValid()
    {
        Assert.True(AdminSecretVerifier.IsValid("hunter2", "hunter2"));
    }

    [Fact]
    public void MismatchedSecret_IsInvalid()
    {
        Assert.False(AdminSecretVerifier.IsValid("wrong", "hunter2"));
    }

    [Fact]
    public void NoConfiguredSecret_RejectsEverything()
    {
        // No secret configured means the portal cannot be trusted at all — reject rather
        // than fall open, same stance as SentryWebhookVerifier in JobSearch.Data.
        Assert.False(AdminSecretVerifier.IsValid("anything", null));
        Assert.False(AdminSecretVerifier.IsValid("anything", ""));
    }

    [Fact]
    public void EmptySubmission_IsInvalid()
    {
        Assert.False(AdminSecretVerifier.IsValid(null, "hunter2"));
        Assert.False(AdminSecretVerifier.IsValid("", "hunter2"));
    }

    [Fact]
    public void DifferentLengthSecret_IsInvalid()
    {
        Assert.False(AdminSecretVerifier.IsValid("short", "a-much-longer-secret-value"));
    }
}
