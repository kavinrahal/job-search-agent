using System.Text.Json;
using System.Text.RegularExpressions;
using Anthropic;
using Anthropic.Models.Messages;

namespace JobSearch.Data;

// A cheap gate in front of PostingEvaluator (see PostingEvaluator.cs), not a replacement for
// it. Real production data: 89.2% of postings that go through the full Sonnet evaluation get
// DisqualifierHit set — i.e. the expensive call was spent just to arrive at a hard,
// rule-checkable rejection. This runs on claude-haiku-4-5 and checks only the same four hard
// disqualifiers (see JobSearchAgent/skills/prefilter_posting.md), nothing else — no weak-match
// judgment, no salary/location/experience scoring. Scope is deliberately narrow: mixing in
// subjective judgment calls here would raise false-negative risk on real matches for no cost
// benefit, since those other dimensions aren't why postings get filtered.
public class PostingPreFilterAgent
{
    private readonly AnthropicClient _client;
    private const string HaikuModel = "claude-haiku-4-5";

    private readonly string _skillText;
    private readonly string _skillVersion;
    private readonly Tool _tool;
    private readonly ClaudeUsageLogger? _usageLogger;

    public PostingPreFilterAgent(string apiKey, ClaudeUsageLogger? usageLogger = null)
    {
        _client = new AnthropicClient { ApiKey = apiKey };
        _skillText = SkillLoader.Load("prefilter_posting.md");
        _skillVersion = SkillLoader.Version(_skillText);
        _usageLogger = usageLogger;

        _tool = new Tool
        {
            Name = "prefilter_posting",
            Description = "Check a job posting for the four hard disqualifiers only (sponsorship exclusion, PHP-primary backend, gambling as core business, solo-engineer role). Do not score anything else.",
            InputSchema = new InputSchema
            {
                Properties = new Dictionary<string, JsonElement>
                {
                    ["disqualifier_hit"] = PropEnum(
                        "Which hard disqualifier explicitly matched. Omit this field entirely if none matched — never the literal string \"null\".",
                        "sponsorship", "php_primary", "gambling", "solo_engineer"),
                    ["evidence"] = Prop(
                        "Exact quoted phrase from the posting that triggered the disqualifier. Required when disqualifier_hit is set. Omit this field entirely if there is none — never the literal string \"null\"."),
                },
                Required = [],
            },
            CacheControl = new CacheControlEphemeral(),
        };
    }

    protected PostingPreFilterAgent() { _client = null!; _skillText = ""; _skillVersion = ""; _tool = null!; _usageLogger = null; }

    // Per-call, not per-instance — see CvTailorAgent.BuildSystemPrompt for why.
    private string BuildSystemPrompt(UserProfile profile) => $"""
        {_skillText}

        --- JOB CRITERIA ---
        {profile.JobCriteria}
        """;

    // Words/frameworks whose literal presence is required for a php_primary hit to be trusted —
    // see the "Mandatory literal-text gate" in prefilter_posting.md's php_primary section.
    private static readonly string[] PhpKeywords = ["php", "laravel", "symfony", "wordpress", "codeigniter", "cakephp", "drupal"];

    public virtual async Task<PostingPreFilterResult> PreFilterAsync(UserProfile profile, string postingText, string? sourceUrl = null)
    {
        string userContent = sourceUrl is not null
            ? $"Source URL: {sourceUrl}\n\n{postingText}"
            : postingText;

        var result = await ClaudeToolCallRetry.CallAsync(
            _client,
            buildRequest: messages => new MessageCreateParams
            {
                Model = HaikuModel,
                MaxTokens = 256,
                // Fixed-category classification (one of four disqualifier ids, or none) — low,
                // consistent temperature instead of the API default (1.0). See ClaudeTemperature.
                // claude-haiku-4-5 predates the post-Opus-4.6 restriction that forces the default
                // temperature, so this is safe to set (same reasoning as EmailClassifier).
#pragma warning disable CS0618
                Temperature = ClaudeTemperature.Classification,
#pragma warning restore CS0618
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
                Messages = [.. messages],
            },
            initialMessages: [new() { Role = Role.User, Content = userContent }],
            toolName: _tool.Name,
            parse: ParseResult,
            missingToolUseMessage: "Pre-filter did not return a tool use block.",
            logLabel: nameof(PostingPreFilterAgent),
            onUsage: _usageLogger is null ? null : usage => _usageLogger.LogAsync(profile.UserId, ClaudeAgentName.PostingPreFilterAgent, HaikuModel, usage, _skillVersion));

        result = RejectUnsupportedPhpPrimary(result, postingText);
        result = RejectSponsorshipForCitizenOrPr(result, profile.JobCriteria);
        return result;
    }

    // Belt-and-suspenders on top of the skill file's own "mandatory literal-text gate": a live
    // eval against real production postings (see the eval suite referenced from the PR) showed
    // claude-haiku-4-5 repeatedly misusing php_primary as a stand-in for "backend isn't the
    // candidate's preferred language" (flagging Java/Python roles as php_primary) despite
    // multiple rounds of increasingly explicit prompt guardrails against exactly that pattern.
    // Prompt-only fixes plateaued, so this closes the gap deterministically in code: a
    // php_primary hit is only trusted if the posting text actually contains the literal word
    // "PHP" or a named PHP framework/CMS. This cannot introduce a false negative — it only ever
    // downgrades a php_primary hit to "no disqualifier", never the reverse.
    internal static PostingPreFilterResult RejectUnsupportedPhpPrimary(PostingPreFilterResult result, string postingText)
    {
        if (result.DisqualifierHit != "php_primary") return result;
        if (PhpKeywords.Any(kw => postingText.Contains(kw, StringComparison.OrdinalIgnoreCase))) return result;

        Console.Error.WriteLine($"[{nameof(PostingPreFilterAgent)}] Rejecting php_primary — posting text doesn't literally mention PHP or a PHP framework. evidence was: \"{result.Evidence}\"");
        return new PostingPreFilterResult { DisqualifierHit = null, Evidence = null };
    }

    // Matches `citizen_or_permanent_resident: true` (or `citizenOrPermanentResident: true`,
    // any surrounding whitespace/quoting) wherever it appears in the candidate's job criteria
    // text — the structural field this repo's real job_criteria.yaml and JobSearch.Web's
    // jobCriteriaYaml.ts both serialize the candidate's own citizen/PR status under.
    private static readonly Regex CitizenOrPrTrue = new(
        @"citizen[_ ]?or[_ ]?permanent[_ ]?resident\s*:\s*""?true""?",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // Same belt-and-suspenders reasoning as RejectUnsupportedPhpPrimary: a live eval run showed
    // the model still flagging `sponsorship` even when the candidate's own criteria explicitly
    // states citizen_or_permanent_resident: true — despite the skill file's explicit "this check
    // never applies" instruction for exactly that case. A citizen/PR candidate is never affected
    // by "no sponsorship" or "citizens only" language, so when the criteria unambiguously says
    // so, the flag is always wrong and can be safely discarded in code rather than trusted to
    // prompt-following alone. This cannot introduce a false negative on a candidate who actually
    // needs sponsorship — it only fires when the criteria explicitly says citizen_or_permanent_
    // resident: true.
    internal static PostingPreFilterResult RejectSponsorshipForCitizenOrPr(PostingPreFilterResult result, string jobCriteria)
    {
        if (result.DisqualifierHit != "sponsorship") return result;
        if (!CitizenOrPrTrue.IsMatch(jobCriteria)) return result;

        Console.Error.WriteLine($"[{nameof(PostingPreFilterAgent)}] Rejecting sponsorship — candidate's own criteria states citizen_or_permanent_resident: true. evidence was: \"{result.Evidence}\"");
        return new PostingPreFilterResult { DisqualifierHit = null, Evidence = null };
    }

    // The only four categories this agent is scoped to detect — see prefilter_posting.md.
    private static readonly HashSet<string> ValidDisqualifierIds = new(StringComparer.OrdinalIgnoreCase)
    {
        "sponsorship", "php_primary", "gambling", "solo_engineer",
    };

    // Split out from PreFilterAsync so the tool-input-to-DTO mapping (including the
    // literal-"null" normalization below) is unit-testable without a live API call — mirrors
    // PostingEvaluator.ParseEvaluation.
    internal static PostingPreFilterResult ParseResult(IReadOnlyDictionary<string, JsonElement> i)
    {
        string? disqualifierHit = i.TryGetValue("disqualifier_hit", out var dq) ? NullIfLiteralNull(dq.GetString()) : null;

        // Defensive, not just decorative: the tool schema's `enum` constraint on disqualifier_hit
        // is a strong hint to the model, not a hard guarantee — observed live, the model has
        // emitted values outside the four allowed ids (e.g. "senior_role") despite the schema.
        // A narrow prefilter that starts inventing its own disqualifier categories defeats the
        // entire point of keeping this scoped to four checks, so treat anything outside the
        // known set the same as "nothing found" rather than trusting it through.
        if (disqualifierHit is not null && !ValidDisqualifierIds.Contains(disqualifierHit))
        {
            Console.Error.WriteLine($"[{nameof(PostingPreFilterAgent)}] Ignoring out-of-scope disqualifier_hit \"{disqualifierHit}\" — not one of the four known ids.");
            disqualifierHit = null;
        }

        // Evidence for a disqualifier that got rejected above is meaningless on its own — keep
        // the two fields paired the same way the model is asked to produce them.
        string? evidence = null;
        if (disqualifierHit is not null && i.TryGetValue("evidence", out var ev))
            evidence = NullIfLiteralNull(ev.GetString());

        return new PostingPreFilterResult { DisqualifierHit = disqualifierHit, Evidence = evidence };
    }

    // The model occasionally emits the literal string "null" for an optional field instead of
    // omitting it or using a real JSON null — see the identical note on PostingEvaluator.
    private static string? NullIfLiteralNull(string? value) =>
        value is null || value.Trim().Equals("null", StringComparison.OrdinalIgnoreCase) ? null : value;

    private static JsonElement Prop(string description) =>
        JsonSerializer.SerializeToElement(new { type = "string", description });

    private static JsonElement PropEnum(string description, params string[] values) =>
        JsonSerializer.SerializeToElement(new { type = "string", description, @enum = values });
}

public class PostingPreFilterResult
{
    public string? DisqualifierHit { get; init; }
    public string? Evidence { get; init; }
}
