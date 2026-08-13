using System.Text.Json;
using Anthropic;
using Anthropic.Models.Messages;

namespace JobSearch.Data;

public class PostingEvaluator
{
    private readonly AnthropicClient _client;
    private const string OpusModel = "claude-opus-4-8";

    private readonly string _skillText;
    private readonly Tool _tool;

    public PostingEvaluator(string apiKey)
    {
        _client = new AnthropicClient { ApiKey = apiKey };
        _skillText = SkillLoader.Load("evaluate_posting.md");

        _tool = new Tool
        {
            Name = "evaluate_posting",
            Description = "Evaluate a job posting and return a structured assessment.",
            InputSchema = new InputSchema
            {
                Properties = new Dictionary<string, JsonElement>
                {
                    ["company"]              = Prop("string", "Company name."),
                    ["role_title"]           = Prop("string", "Job title."),
                    ["source_url"]           = Prop("string", "Source URL, or null."),
                    ["recommendation"]       = PropEnum("Recommendation tier.",
                                                  "strong_match", "good_match", "weak_match", "discard"),
                    ["disqualifier_hit"]     = Prop("string", "Disqualifier id that triggered, or null."),
                    ["sponsorship_verdict"]  = PropEnum("Sponsorship stance.", "pass", "discard"),
                    ["sponsorship_evidence"] = Prop("string", "Exact quoted phrase, or null."),
                    ["location_match"]       = PropEnum("Location fit.", "preferred", "acceptable", "weak"),
                    ["location_detail"]      = Prop("string", "City and arrangement."),
                    ["experience_match"]     = PropEnum("Experience fit.", "ideal", "acceptable", "excluded"),
                    ["experience_detail"]    = Prop("string", "Quoted years requirement."),
                    ["skill_matches"]        = PropSkillMatchArray(
                                                  "One entry per skill dimension defined in the candidate's job " +
                                                  "criteria (e.g. \"Backend stack\", \"Clinical specialty\") — not " +
                                                  "fixed fields, the dimensions come from the criteria itself."),
                    ["salary_assessment"]    = PropEnum("Salary fit.",
                                                  "target", "acceptable", "flagged_low", "flagged_high", "missing"),
                    ["salary_detail"]        = Prop("string", "Quoted salary figure or range, or null."),
                    ["company_assessment"]   = PropEnum("Company fit.",
                                                  "preferred", "acceptable", "weaker", "excluded"),
                    ["role_type_match"]      = PropEnum("Role type fit.",
                                                  "preferred", "acceptable", "weaker", "excluded"),
                    ["orange_flags"]         = PropArray("Active orange flags. Empty array if none."),
                    ["rationale"]            = Prop("string", "2-3 sentences on key factors."),
                },
                Required = [
                    "company", "role_title", "recommendation", "sponsorship_verdict",
                    "location_match", "location_detail", "experience_match", "experience_detail",
                    "skill_matches", "salary_assessment", "company_assessment", "role_type_match",
                    "orange_flags", "rationale",
                ],
            },
            CacheControl = new CacheControlEphemeral(),
        };
    }

    protected PostingEvaluator() { _client = null!; _skillText = ""; _tool = null!; }

    // Per-call, not per-instance — see CvTailorAgent.BuildSystemPrompt for why.
    private string BuildSystemPrompt(UserProfile profile) => $"""
        {_skillText}

        --- JOB CRITERIA ---
        {profile.JobCriteria}
        """;

    public virtual async Task<PostingEvaluation> EvaluateAsync(UserProfile profile, string postingText, string? sourceUrl = null)
    {
        string userContent = sourceUrl is not null
            ? $"Source URL: {sourceUrl}\n\n{postingText}"
            : postingText;

        var response = await _client.Messages.Create(new MessageCreateParams
        {
            Model = OpusModel,
            MaxTokens = 1024,
            System = new List<TextBlockParam>
            {
                new()
                {
                    Text = BuildSystemPrompt(profile),
                    CacheControl = new CacheControlEphemeral(),
                },
            },
            Tools = [_tool],
            ToolChoice = new ToolChoiceAny(),
            Messages = [new() { Role = Role.User, Content = userContent }],
        });

        foreach (var block in response.Content)
        {
            if (block.TryPickToolUse(out ToolUseBlock? toolUse))
            {
                var i = toolUse.Input;
                return new PostingEvaluation
                {
                    Company             = i["company"].GetString() ?? "",
                    RoleTitle           = i["role_title"].GetString() ?? "",
                    SourceUrl           = i.TryGetValue("source_url", out var su) ? su.GetString() : sourceUrl,
                    Recommendation      = i["recommendation"].GetString() ?? "",
                    DisqualifierHit     = i.TryGetValue("disqualifier_hit", out var dq) ? dq.GetString() : null,
                    SponsorshipVerdict  = i["sponsorship_verdict"].GetString() ?? "",
                    SponsorshipEvidence = i.TryGetValue("sponsorship_evidence", out var se) ? se.GetString() : null,
                    LocationMatch       = i["location_match"].GetString() ?? "",
                    LocationDetail      = i["location_detail"].GetString() ?? "",
                    ExperienceMatch     = i["experience_match"].GetString() ?? "",
                    ExperienceDetail    = i["experience_detail"].GetString() ?? "",
                    SkillMatches        = GetSkillMatches(i, "skill_matches"),
                    SalaryAssessment    = i["salary_assessment"].GetString() ?? "",
                    SalaryDetail        = i.TryGetValue("salary_detail", out var sd) ? sd.GetString() : null,
                    CompanyAssessment   = i["company_assessment"].GetString() ?? "",
                    RoleTypeMatch       = i["role_type_match"].GetString() ?? "",
                    OrangeFlags         = GetStringArray(i, "orange_flags"),
                    Rationale           = i["rationale"].GetString() ?? "",
                };
            }
        }

        throw new InvalidOperationException("Evaluator did not return a tool use block.");
    }

    private static string[] GetStringArray(IReadOnlyDictionary<string, JsonElement> input, string key)
    {
        if (!input.TryGetValue(key, out var el) || el.ValueKind != JsonValueKind.Array)
            return [];
        return [.. el.EnumerateArray().Select(e => e.GetString() ?? "").Where(s => s.Length > 0)];
    }

    private static SkillMatch[] GetSkillMatches(IReadOnlyDictionary<string, JsonElement> input, string key)
    {
        if (!input.TryGetValue(key, out var el) || el.ValueKind != JsonValueKind.Array)
            return [];
        return [.. el.EnumerateArray().Select(e => new SkillMatch(
            e.TryGetProperty("dimension", out var d) ? d.GetString() ?? "" : "",
            e.TryGetProperty("match", out var m) ? m.GetString() ?? "" : "",
            e.TryGetProperty("detail", out var det) ? det.GetString() ?? "" : ""))];
    }

    private static JsonElement Prop(string type, string description) =>
        JsonSerializer.SerializeToElement(new { type, description });

    private static JsonElement PropEnum(string description, params string[] values) =>
        JsonSerializer.SerializeToElement(new { type = "string", description, @enum = values });

    private static JsonElement PropArray(string description) =>
        JsonSerializer.SerializeToElement(new
        {
            type = "array",
            description,
            items = new { type = "string" },
        });

    private static JsonElement PropSkillMatchArray(string description) =>
        JsonSerializer.SerializeToElement(new
        {
            type = "array",
            description,
            items = new
            {
                type = "object",
                properties = new
                {
                    dimension = new { type = "string", description = "Skill dimension name, as defined in the candidate's job criteria." },
                    match = new { type = "string", @enum = new[] { "strong", "good", "acceptable", "excluded" }, description = "Fit tier for this dimension." },
                    detail = new { type = "string", description = "Specific technologies/qualifications/etc. named in the posting for this dimension." },
                },
                required = new[] { "dimension", "match", "detail" },
            },
        });
}
