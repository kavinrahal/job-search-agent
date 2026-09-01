using AdminDashboard.Api.Services;

namespace AdminDashboard.Api.Tests;

public class AdminCredentialVerifierTests
{
    [Fact]
    public void MatchingUsernameAndPassword_IsValid()
    {
        Assert.True(AdminCredentialVerifier.IsValid("admin", "hunter2", "admin", "hunter2"));
    }

    [Fact]
    public void WrongPassword_IsInvalid()
    {
        Assert.False(AdminCredentialVerifier.IsValid("admin", "wrong", "admin", "hunter2"));
    }

    [Fact]
    public void WrongUsername_IsInvalid()
    {
        Assert.False(AdminCredentialVerifier.IsValid("wrong", "hunter2", "admin", "hunter2"));
    }

    [Fact]
    public void NothingConfigured_RejectsEverything()
    {
        // Nothing configured means the endpoint cannot be trusted at all — reject rather than
        // fall open, same stance as SentryWebhookVerifier in JobSearch.Data.
        Assert.False(AdminCredentialVerifier.IsValid("admin", "hunter2", null, null));
        Assert.False(AdminCredentialVerifier.IsValid("admin", "hunter2", "", ""));
    }

    [Fact]
    public void EmptySubmission_IsInvalid()
    {
        Assert.False(AdminCredentialVerifier.IsValid(null, null, "admin", "hunter2"));
        Assert.False(AdminCredentialVerifier.IsValid("", "", "admin", "hunter2"));
        Assert.False(AdminCredentialVerifier.IsValid("admin", null, "admin", "hunter2"));
        Assert.False(AdminCredentialVerifier.IsValid(null, "hunter2", "admin", "hunter2"));
    }

    [Fact]
    public void DifferentLengthValues_AreInvalid()
    {
        Assert.False(AdminCredentialVerifier.IsValid("adm", "hunter2", "admin", "hunter2"));
        Assert.False(AdminCredentialVerifier.IsValid("admin", "short", "admin", "a-much-longer-password-value"));
    }
}
