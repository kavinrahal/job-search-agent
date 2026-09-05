using System.Text.Json;
using Anthropic;
using Anthropic.Models.Messages;

namespace JobSearch.Data;

public class PostingEvaluator
{
    private readonly AnthropicClient _client;
    private const string SonnetModel = "claude-sonnet-5";

    private readonly string _skillText;
    private readonly Tool _tool;
    private readonly ClaudeUsageLogger? _usageLogger;

    public PostingEvaluator(string apiKey, ClaudeUsageLogger? usageLogger = null)
    {
        _client = new AnthropicClient { ApiKey = apiKey };
        _skillText = SkillLoader.Load("evaluate_posting.md");
        _usageLogger = usageLogger;

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
                    ["source_url"]           = Prop("string", "Source URL. Omit this field entirely if there is none — never the literal string \"null\".", nullable: true),
                    ["recommendation"]       = PropEnum("Recommendation tier.",
                                                  "strong_match", "good_match", "weak_match", "discard"),
                    ["disqualifier_hit"]     = Prop("string", "Disqualifier id that triggered. Omit this field entirely if none triggered — never the literal string \"null\".", nullable: true),
                    ["sponsorship_verdict"]  = PropEnum("Sponsorship stance.", "pass", "discard"),
                    ["sponsorship_evidence"] = Prop("string", "Exact quoted phrase. Omit this field entirely if there is none — never the literal string \"null\".", nullable: true),
                    ["location_match"]       = PropEnum("Location fit.", "preferred", "acceptable", "weak", "missing"),
                    ["location_detail"]      = Prop("string", "City and arrangement."),
                    ["experience_match"]     = PropEnum("Experience fit.", "ideal", "acceptable", "excluded", "missing"),
                    ["experience_detail"]    = Prop("string", "Quoted years requirement."),
                    ["skill_matches"]        = PropSkillMatchArray(
                                                  "One entry per skill dimension defined in the candidate's job " +
                                                  "criteria (e.g. \"Backend stack\", \"Clinical specialty\") — not " +
                                                  "fixed fields, the dimensions come from the criteria itself."),
                    ["salary_assessment"]    = PropEnum("Salary fit.",
                                                  "target", "acceptable", "flagged_low", "flagged_high", "missing"),
                    ["salary_detail"]        = Prop("string", "Quoted salary figure or range. Omit this field entirely if not stated — never the literal string \"null\".", nullable: true),
                    ["company_assessment"]   = PropEnum("Company fit.",
                                                  "preferred", "acceptable", "weaker", "excluded", "missing"),
                    ["role_type_match"]      = PropEnum("Role type fit.",
                                                  "preferred", "acceptable", "weaker", "excluded", "missing"),
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

    protected PostingEvaluator() { _client = null!; _skillText = ""; _tool = null!; _usageLogger = null; }

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
            Model = SonnetModel,
            MaxTokens = 1024,
            // This is classification-shaped (fixed recommendation tier + enum fields) and would
            // otherwise get ClaudeTemperature.Classification, but claude-sonnet-5 is one of the
            // post-Opus-4.6 models that only accepts the API-default temperature of 1.0 — any
            // other value is rejected with a 400 (see the [Obsolete] note on
            // MessageCreateParams.Temperature). There is no lever here; leave unset.
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

        if (_usageLogger is not null)
            await _usageLogger.LogAsync(profile.UserId, ClaudeAgentName.PostingEvaluator, SonnetModel, response.Usage);

        foreach (var block in response.Content)
        {
            if (block.TryPickToolUse(out ToolUseBlock? toolUse))
                return ParseEvaluation(toolUse.Input, sourceUrl);
        }

        throw new InvalidOperationException("Evaluator did not return a tool use block.");
    }

    // Split out from EvaluateAsync so the tool-input-to-DTO mapping (including the literal-"null"
    // normalization below) is unit-testable without a live API call — see
    // PostingEvaluatorParsingTests.
    internal static PostingEvaluation ParseEvaluation(IReadOnlyDictionary<string, JsonElement> i, string? fallbackSourceUrl)
    {
        return new PostingEvaluation
        {
            Company             = i["company"].GetString() ?? "",
            RoleTitle           = i["role_title"].GetString() ?? "",
            SourceUrl           = i.TryGetValue("source_url", out var su) ? NullIfLiteralNull(su.GetString()) : fallbackSourceUrl,
            Recommendation      = i["recommendation"].GetString() ?? "",
            DisqualifierHit     = i.TryGetValue("disqualifier_hit", out var dq) ? NullIfLiteralNull(dq.GetString()) : null,
            SponsorshipVerdict  = i["sponsorship_verdict"].GetString() ?? "",
            SponsorshipEvidence = i.TryGetValue("sponsorship_evidence", out var se) ? NullIfLiteralNull(se.GetString()) : null,
            LocationMatch       = i["location_match"].GetString() ?? "",
            LocationDetail      = i["location_detail"].GetString() ?? "",
            ExperienceMatch     = i["experience_match"].GetString() ?? "",
            ExperienceDetail    = i["experience_detail"].GetString() ?? "",
            SkillMatches        = GetSkillMatches(i, "skill_matches"),
            SalaryAssessment    = i["salary_assessment"].GetString() ?? "",
            SalaryDetail        = i.TryGetValue("salary_detail", out var sd) ? NullIfLiteralNull(sd.GetString()) : null,
            CompanyAssessment   = i["company_assessment"].GetString() ?? "",
            RoleTypeMatch       = i["role_type_match"].GetString() ?? "",
            OrangeFlags         = GetStringArray(i, "orange_flags"),
            Rationale           = i["rationale"].GetString() ?? "",
        };
    }

    // The model occasionally emits the literal string "null" for an optional field instead of
    // omitting it or using a real JSON null (observed live: "Disqualifier: null" / "Salary: null"
    // rendering on discovery cards). Don't rely on prompt/schema tightening alone to prevent this
    // — normalize defensively at the boundary where the value leaves the LLM response.
    private static string? NullIfLiteralNull(string? value) =>
        value is null || value.Trim().Equals("null", StringComparison.OrdinalIgnoreCase) ? null : value;

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

    private static JsonElement Prop(string type, string description, bool nullable = false) =>
        nullable
            ? JsonSerializer.SerializeToElement(new { type = new[] { type, "null" }, description })
            : JsonSerializer.SerializeToElement(new { type, description });

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
                    match = new { type = "string", @enum = new[] { "strong", "good", "acceptable", "excluded", "missing" }, description = "Fit tier for this dimension." },
                    detail = new { type = "string", description = "Specific technologies/qualifications/etc. named in the posting for this dimension." },
                },
                required = new[] { "dimension", "match", "detail" },
            },
        });
}
