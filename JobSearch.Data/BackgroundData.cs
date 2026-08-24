namespace JobSearch.Data;

// C# mirror of skills/context/background.yaml's shape, extended with the new optional sections
// from the resume-builder Phase 1 schema (Credentials, Publications, Volunteering, Personal.
// PortfolioUrl). Deserialized via BackgroundYamlParser (UnderscoredNamingConvention, so these
// PascalCase properties map onto the YAML's snake_case keys automatically).
//
// No typed Skills or Narrative property here, deliberately: confirmed by direct comparison
// against the real cv_base.md that Skills' nested shape doesn't deterministically map to any
// rendered output (the same shape gets merged into one line for one category and split into
// several for another, by one-time human/LLM judgment, not a rule a parser could apply), and
// Narrative is pure cover-letter guidance never touched by the CV renderer at all — neither has
// a renderer consumer, so neither is worth the parsing fragility. That fragility is real, not
// hypothetical: Skills isn't safe as Dictionary<string, object> (YamlDotNet's dynamic-object
// deserialization throws on this runtime — "Uninitialized Strings cannot be created" — see
// BackgroundYamlParserTests history), and a live production backfill run against real user data
// hit a second, independent case of the same root problem in Experience.Stack/Project.TechStack
// (removed below) — different users' independently LLM-generated Background YAML doesn't
// reliably share a dict-of-lists shape for fields the renderer never reads anyway. Every field
// removed here for this reason is confirmed to have zero consumer via grep before removal, not
// assumed. Callers that need this content as CV-tailoring prompt context keep using the raw
// Background YAML string directly, same as before any of this existed.
public class BackgroundData
{
    public string Version { get; set; } = "";
    public PersonalInfo Personal { get; set; } = new();
    public List<ExperienceEntry> Experience { get; set; } = [];
    public List<EducationEntry> Education { get; set; } = [];
    public List<ProjectEntry> Projects { get; set; } = [];
    public List<CredentialEntry> Credentials { get; set; } = [];
    public List<PublicationEntry> Publications { get; set; } = [];
    public List<VolunteeringEntry> Volunteering { get; set; } = [];
}

public class PersonalInfo
{
    public string Name { get; set; } = "";
    public string Handle { get; set; } = "";
    public string Email { get; set; } = "";
    public string Phone { get; set; } = "";
    public string Location { get; set; } = "";
    public string Linkedin { get; set; } = "";
    public string Github { get; set; } = "";
    public string? PortfolioUrl { get; set; }
}

public class DateRange
{
    public string Start { get; set; } = "";
    public string? End { get; set; }
}

public class ExperienceEntry
{
    public string Company { get; set; } = "";
    public string Role { get; set; } = "";
    public DateRange Dates { get; set; } = new();
    public string Location { get; set; } = "";
    public string EmploymentType { get; set; } = "";
    public string Domain { get; set; } = "";
    public string? CompanyDescription { get; set; }
    public List<string> Achievements { get; set; } = [];
    public string? Notes { get; set; }

    // Deliberately no typed Anchors/KeyDetails property: those are cover-letter narrative
    // guidance (write_cover_letter.md), never touched by the CV renderer, and their key_details
    // items use a "Label: text" plain-string convention that's genuinely ambiguous YAML — a
    // strict parser reads "- Feature area: real-time monitoring" as a single-key mapping, not a
    // string, and coercing that into List<string> throws (confirmed against the real fixture:
    // "Cannot dynamically create an instance of type 'System.String'"). Same call as Skills
    // above — not needed for rendering, so not worth fighting the ambiguity for. Anything that
    // needs anchor/narrative content keeps reading the raw Background YAML string, unchanged.
}

public class EducationEntry
{
    public string Institution { get; set; } = "";
    public string Degree { get; set; } = "";
    public string Location { get; set; } = "";
    public double? Gpa { get; set; }
    public string? GpaNotes { get; set; }
    public int GraduationYear { get; set; }
}

public class ProjectEntry
{
    public string Name { get; set; } = "";
    public string Status { get; set; } = "";
    public string Description { get; set; } = "";
    public List<string> Highlights { get; set; } = [];
    public string? Notes { get; set; }
}

// One shape for the three "gate section" credential types Phase 1 research found (healthcare
// licenses, legal bar admissions, trades certifications) rather than three near-identical lists.
public class CredentialEntry
{
    public string Kind { get; set; } = ""; // "license" | "bar_admission" | "certification"
    public string Name { get; set; } = "";
    public string Issuer { get; set; } = "";
    public string? IdOrNumber { get; set; }
    public string? IssuedDate { get; set; }
    public string? ExpiryDate { get; set; }
    public string? Status { get; set; }
}

public class PublicationEntry
{
    public string Title { get; set; } = "";
    public string? Venue { get; set; }
    public string? Date { get; set; }
    public string? Authors { get; set; }
    public string? Url { get; set; }
}

public class VolunteeringEntry
{
    public string Role { get; set; } = "";
    public string Org { get; set; } = "";
    public DateRange Dates { get; set; } = new();
    public string? Description { get; set; }
}
