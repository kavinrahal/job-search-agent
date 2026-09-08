using System.Security.Cryptography;
using System.Text;

namespace JobSearch.Data;

public static class SkillLoader
{
    public static string Load(string filename)
    {
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (dir != null)
        {
            string path = Path.Combine(dir.FullName, "skills", filename);
            if (File.Exists(path)) return File.ReadAllText(path);
            dir = dir.Parent;
        }
        throw new FileNotFoundException($"skills/{filename} not found in any ancestor directory.");
    }

    // A short, stable identifier for a loaded skill file's content, computed once at load time
    // (skill files are read once per process lifetime — see Load above and each agent's
    // constructor) and threaded through to ClaudeUsageLog. This ties a historical usage-log row
    // back to the exact skill text that produced that call's prompt, so a bad output can be
    // checked against the skill-file text as it existed then vs. now.
    //
    // Content hash, not a git commit: this repo has no build-stamped version/commit identifier
    // available at runtime (no AssemblyInformationalVersion, no env var set by any Dockerfile or
    // CI workflow), so a running process can't reliably know its own git commit. Hashing what was
    // actually loaded is also more honest — it changes if and only if the prompt text changes,
    // independent of unrelated commits.
    public static string Version(string content)
    {
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(content));
        return Convert.ToHexStringLower(hash)[..16];
    }
}
