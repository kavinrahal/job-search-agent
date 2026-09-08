using JobSearch.Data;

namespace JobSearch.Api.Tests;

public class SkillLoaderTests
{
    // TC01 — Hashing the same content twice produces the same identifier — this is what makes
    // it usable as a stable "which skill-file text produced this call" marker in ClaudeUsageLog.
    [Fact]
    public void Version_SameContent_ReturnsSameHash()
    {
        string content = "You are a helpful assistant. Follow these rules exactly.";

        string first = SkillLoader.Version(content);
        string second = SkillLoader.Version(content);

        Assert.Equal(first, second);
    }

    // TC02 — Any change to the skill file's text, however small, must change the identifier —
    // otherwise a revised skill file would be indistinguishable from the one it replaced.
    [Fact]
    public void Version_DifferentContent_ReturnsDifferentHash()
    {
        string original = "You are a helpful assistant.";
        string edited = "You are a very helpful assistant.";

        Assert.NotEqual(SkillLoader.Version(original), SkillLoader.Version(edited));
    }

    // TC03 — Truncated to a fixed, storage-friendly length (first 16 hex chars of the SHA-256),
    // not the full 64-character digest.
    [Fact]
    public void Version_ReturnsSixteenHexCharacters()
    {
        string version = SkillLoader.Version("arbitrary skill text");

        Assert.Equal(16, version.Length);
        Assert.Matches("^[0-9a-f]{16}$", version);
    }
}
