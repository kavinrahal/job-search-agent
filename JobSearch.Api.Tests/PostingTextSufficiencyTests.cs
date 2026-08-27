using JobSearch.Data;

namespace JobSearch.Api.Tests;

// Tests the guard GenerateArtifactAsync (Program.cs) runs on resolvedText before ever calling
// the CV/letter-writing agent. Placed ahead of both company extraction and WithCreditAsync in
// that method, so a text failing this check structurally never reaches the agent, never spends
// a credit, and never creates an AgentThread — those three don't need separate integration
// coverage here, they fall out of where the `if (!IsSufficient(...))` early-return sits.
public class PostingTextSufficiencyTests
{
    // TC01 — null (would only reach this check if ResolvePostingTextAsync's own `is null` guard
    // were ever bypassed), empty (a fetch that "succeeds" against a blank page), whitespace-only
    // (a page that's all layout, no text), and a short bot-block/login-wall message (exactly the
    // case this guard exists for: FetchAsync returned without throwing, so
    // ResolvePostingTextAsync's try/catch never saw it) are all rejected.
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   \n\t  ")]
    [InlineData("Please verify you are human to continue.")]
    public void IsSufficient_InsufficientText_ReturnsFalse(string? text)
    {
        Assert.False(PostingTextSufficiency.IsSufficient(text));
    }

    // TC02 — the boundary: one char under the floor is rejected, exactly at the floor is
    // accepted (inclusive), and padding around otherwise-sufficient content doesn't count
    // toward the floor (matches how a real fetch result would be padded by page chrome).
    [Fact]
    public void IsSufficient_LengthBoundary()
    {
        Assert.False(PostingTextSufficiency.IsSufficient(new string('a', PostingTextSufficiency.MinLength - 1)));
        Assert.True(PostingTextSufficiency.IsSufficient(new string('a', PostingTextSufficiency.MinLength)));

        var padding = new string(' ', 50);
        Assert.False(PostingTextSufficiency.IsSufficient(padding + new string('a', PostingTextSufficiency.MinLength - 1) + padding));
    }

    // TC03 — a realistically-sized job posting is accepted.
    [Fact]
    public void IsSufficient_RealisticPosting_ReturnsTrue()
    {
        var posting = "Senior Software Engineer at Acme Corp. " +
            "We are looking for an experienced engineer to join our platform team. " +
            "You will design, build, and maintain services that power our core product. " +
            "Requirements: 5+ years experience with C#, ASP.NET Core, and PostgreSQL. " +
            "Strong communication skills and a track record of shipping production systems.";

        Assert.True(PostingTextSufficiency.IsSufficient(posting));
    }
}
