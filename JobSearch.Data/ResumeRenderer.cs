using System.Text;
using System.Text.Json;

namespace JobSearch.Data;

// (BackgroundData, UserResume) -> markdown, replacing "cv_base.md is a separately hand-maintained
// document" with a deterministic render over structured data. Output must stay within the
// markdown subset PdfRenderer.cs already parses (#/##/###, bullets, bold, ---) since that stays
// unchanged — and must match today's real cv_base.md conventions closely enough that migrated
// users see no visible difference (see BackgroundYamlParserTests and the Phase 1 plan's
// correctness bar).
public static class ResumeRenderer
{
    // Section keys that render deterministically straight from Background, no override needed —
    // confirmed by direct comparison against real data (see UserResume.cs / the Phase 1 plan).
    private static readonly Dictionary<string, Func<BackgroundData, UserResume, string?>> SectionBuilders = new()
    {
        ["experience"] = RenderExperience,
        ["education"] = RenderEducation,
        ["skills"] = RenderSkills,
        ["projects"] = RenderProjects,
        ["credentials"] = RenderCredentials,
        ["publications"] = RenderPublications,
        ["volunteering"] = RenderVolunteering,
    };

    // Default order/inclusion when SectionConfigJson is empty/missing — matches today's real
    // cv_base.md layout exactly. Credentials/Publications are new sections with nothing to match
    // against, so they default to excluded; a user (or the backfill) opts them in once populated.
    private static readonly List<SectionConfigEntry> DefaultSectionConfig =
    [
        new("experience", true),
        new("education", true),
        new("skills", true),
        new("projects", true),
        new("credentials", false),
        new("publications", false),
        new("volunteering", true),
    ];

    public static string Render(BackgroundData background, UserResume resume)
    {
        var sb = new StringBuilder();

        sb.Append("# ").Append(background.Personal.Name).Append('\n').Append('\n');
        sb.Append(ContactLine(background.Personal)).Append('\n').Append('\n');
        sb.Append("## Summary").Append('\n').Append('\n');
        sb.Append(string.IsNullOrWhiteSpace(resume.Summary)
            ? "[Fresh summary specific to this role; see tailoring instructions]"
            : resume.Summary).Append('\n');

        var sectionConfig = ParseOrDefault(resume.SectionConfigJson);
        foreach (var section in sectionConfig)
        {
            if (!section.Included) continue;
            if (!SectionBuilders.TryGetValue(section.SectionKey, out var build)) continue;
            var rendered = build(background, resume);
            if (string.IsNullOrWhiteSpace(rendered)) continue; // e.g. Credentials included but still empty
            sb.Append('\n').Append(rendered);
        }

        return sb.ToString().TrimEnd() + "\n";
    }

    private static List<SectionConfigEntry> ParseOrDefault(string json)
    {
        if (string.IsNullOrWhiteSpace(json) || json == "[]") return DefaultSectionConfig;
        var parsed = JsonSerializer.Deserialize<List<SectionConfigEntry>>(json);
        return parsed is { Count: > 0 } ? parsed : DefaultSectionConfig;
    }

    private static string ContactLine(PersonalInfo p)
    {
        var parts = new List<string> { p.Email, p.Phone, p.Location, p.Linkedin, p.Github };
        if (!string.IsNullOrWhiteSpace(p.PortfolioUrl)) parts.Add(p.PortfolioUrl);
        return string.Join(" | ", parts.Where(x => !string.IsNullOrWhiteSpace(x)));
    }

    // "2025-04" -> "Apr 2025"; null/blank end date -> "Present". Not a full date parser — this
    // is the one format background.yaml's dates actually use (confirmed against every entry in
    // the real fixture), so anything else falls back to the raw string rather than throwing.
    private static readonly string[] MonthAbbreviations =
        ["Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec"];

    private static string FormatMonthYear(string? yyyyMm)
    {
        if (string.IsNullOrWhiteSpace(yyyyMm)) return "Present";
        var parts = yyyyMm.Split('-');
        if (parts.Length != 2 || !int.TryParse(parts[0], out var year) || !int.TryParse(parts[1], out var month) || month is < 1 or > 12)
            return yyyyMm;
        return $"{MonthAbbreviations[month - 1]} {year}";
    }

    private static ExperienceOverride? OverrideFor(List<ExperienceOverride> overrides, int index) =>
        overrides.FirstOrDefault(o => o.ExperienceIndex == index);

    private static string? RenderExperience(BackgroundData background, UserResume resume)
    {
        if (background.Experience.Count == 0) return null;
        var overrides = JsonSerializer.Deserialize<List<ExperienceOverride>>(resume.ExperienceOverridesJson) ?? [];

        var sb = new StringBuilder("## Experience\n\n");
        var any = false;
        for (var i = 0; i < background.Experience.Count; i++)
        {
            var entry = background.Experience[i];
            var over = OverrideFor(overrides, i);
            if (over is { Included: false }) continue;

            any = true;
            sb.Append("### ").Append(entry.Role).Append(" – ").Append(entry.Company).Append('\n');
            sb.Append(entry.Location).Append(" | ")
              .Append(FormatMonthYear(entry.Dates.Start)).Append(" – ").Append(FormatMonthYear(entry.Dates.End))
              .Append('\n').Append('\n');

            var description = over?.CompanyDescriptionOverride ?? entry.CompanyDescription;
            if (!string.IsNullOrWhiteSpace(description))
                sb.Append(description.Trim()).Append('\n').Append('\n');

            RenderBulletList(sb, entry.Achievements, over?.Achievements ?? [], over?.ExtraAchievements ?? []);
            sb.Append('\n');
        }

        return any ? sb.ToString().TrimEnd() + "\n" : null;
    }

    // Shared by Experience achievements and Project highlights — same override shape (ItemOverride),
    // same rule: indexed override (text swap, exclude, or reorder) applied over the base list,
    // then extras (bullets with no Background source at all) appended last.
    //
    // Sort key is a (group, key) pair, not a single number — explicitly-ordered items always
    // sort as group 0 (by their Order value), untouched items as group 1 (by natural index).
    // A single shared "Order ?? naturalIndex" number looks simpler but is wrong: an untouched
    // item's natural index can numerically collide with another item's explicit Order (e.g. the
    // first untouched item is naturally at 0, and a moved item is also given Order=0), and a
    // stable sort resolves that tie by original list position — silently losing the "move to
    // front" the override asked for. The two-group split makes an explicit Order unconditionally
    // win, with natural order preserved as a stable tie-break within each group.
    private static void RenderBulletList(StringBuilder sb, List<string> baseItems, List<ItemOverride> overrides, List<string> extras)
    {
        var survivors = new List<(int group, int key, string text)>();
        for (var i = 0; i < baseItems.Count; i++)
        {
            var over = overrides.FirstOrDefault(x => x.Index == i);
            if (over is { Included: false }) continue;
            var group = over?.Order is not null ? 0 : 1;
            var key = over?.Order ?? i;
            survivors.Add((group, key, (over?.TextOverride ?? baseItems[i]).Trim()));
        }
        foreach (var (_, _, text) in survivors.OrderBy(s => s.group).ThenBy(s => s.key))
            sb.Append("- ").Append(text).Append('\n');
        foreach (var extra in extras)
            sb.Append("- ").Append(extra.Trim()).Append('\n');
    }

    private static string? RenderEducation(BackgroundData background, UserResume _)
    {
        if (background.Education.Count == 0) return null;
        var sb = new StringBuilder("## Education\n\n");
        foreach (var edu in background.Education)
        {
            sb.Append("**").Append(edu.Degree).Append("** – ").Append(edu.Institution).Append('\n');
            var meta = new List<string>();
            if (!string.IsNullOrWhiteSpace(edu.Location)) meta.Add(edu.Location);
            if (edu.GraduationYear is not null) meta.Add(edu.GraduationYear.Value.ToString());
            if (meta.Count > 0) sb.Append(string.Join(" | ", meta)).Append('\n');
            sb.Append('\n');
        }
        return sb.ToString().TrimEnd() + "\n";
    }

    private static string? RenderSkills(BackgroundData _, UserResume resume)
    {
        var sections = JsonSerializer.Deserialize<List<SkillsSectionEntry>>(resume.SkillsSectionJson) ?? [];
        if (sections.Count == 0) return null;
        var sb = new StringBuilder("## Skills\n\n");
        foreach (var s in sections)
            sb.Append("**").Append(s.Label).Append("** – ").Append(string.Join(", ", s.Items)).Append('\n');
        return sb.ToString().TrimEnd() + "\n";
    }

    private static string? RenderProjects(BackgroundData background, UserResume resume)
    {
        if (background.Projects.Count == 0) return null;
        var overrides = JsonSerializer.Deserialize<List<ProjectOverride>>(resume.ProjectOverridesJson) ?? [];

        var sb = new StringBuilder("## Projects\n\n");
        var any = false;
        for (var i = 0; i < background.Projects.Count; i++)
        {
            var proj = background.Projects[i];
            var over = overrides.FirstOrDefault(o => o.ProjectIndex == i);
            if (over is { Included: false }) continue;

            any = true;
            sb.Append("### ").Append(proj.Name).Append('\n').Append('\n');
            var description = over?.DescriptionOverride ?? proj.Description;
            if (!string.IsNullOrWhiteSpace(description))
                sb.Append(description.Trim()).Append('\n').Append('\n');

            RenderBulletList(sb, proj.Highlights, over?.Highlights ?? [], over?.ExtraHighlights ?? []);
            sb.Append('\n');
        }

        return any ? sb.ToString().TrimEnd() + "\n" : null;
    }

    private static string? RenderCredentials(BackgroundData background, UserResume _)
    {
        if (background.Credentials.Count == 0) return null;
        var sb = new StringBuilder("## Credentials\n\n");
        foreach (var c in background.Credentials)
        {
            sb.Append("**").Append(c.Name).Append("** – ").Append(c.Issuer).Append('\n');
            var meta = new List<string>();
            if (!string.IsNullOrWhiteSpace(c.Status)) meta.Add(c.Status);
            if (!string.IsNullOrWhiteSpace(c.IssuedDate)) meta.Add($"Issued {c.IssuedDate}");
            if (!string.IsNullOrWhiteSpace(c.ExpiryDate)) meta.Add($"Expires {c.ExpiryDate}");
            if (meta.Count > 0) sb.Append(string.Join(" | ", meta)).Append('\n');
            sb.Append('\n');
        }
        return sb.ToString().TrimEnd() + "\n";
    }

    private static string? RenderPublications(BackgroundData background, UserResume _)
    {
        if (background.Publications.Count == 0) return null;
        var sb = new StringBuilder("## Publications\n\n");
        foreach (var p in background.Publications)
        {
            var line = p.Title;
            var meta = new List<string>();
            if (!string.IsNullOrWhiteSpace(p.Venue)) meta.Add(p.Venue);
            if (!string.IsNullOrWhiteSpace(p.Date)) meta.Add(p.Date);
            if (meta.Count > 0) line += " — " + string.Join(", ", meta);
            sb.Append("- ").Append(line).Append('\n');
        }
        return sb.ToString().TrimEnd() + "\n";
    }

    private static string? RenderVolunteering(BackgroundData background, UserResume _)
    {
        if (background.Volunteering.Count == 0) return null;
        var sb = new StringBuilder("## Volunteering & Leadership\n\n");
        foreach (var v in background.Volunteering)
        {
            sb.Append("**").Append(v.Role).Append(", ").Append(v.Org).Append("** | ")
              .Append(FormatMonthYear(v.Dates.Start)).Append(" – ").Append(FormatMonthYear(v.Dates.End)).Append('\n');
            if (!string.IsNullOrWhiteSpace(v.Description))
                sb.Append(v.Description.Trim()).Append('\n');
            sb.Append('\n');
        }
        return sb.ToString().TrimEnd() + "\n";
    }
}
