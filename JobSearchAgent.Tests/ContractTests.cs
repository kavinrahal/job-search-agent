using System.Text.Json;
using JobSearch.Data;
using JobSearchAgent.Agents;
using JobSearchAgent.Models;

namespace JobSearchAgent.Tests;

// Contract tests make real API calls. They are excluded from the default test run.
// Run explicitly with: dotnet test --filter "Category=contract"
// Requires: ANTHROPIC_API_KEY environment variable set.
public class ContractTests
{
    private static string? ApiKey =>
        Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");

    private const string SamplePosting = """
        Company: Acme Software
        Role: Software Engineer
        Location: Melbourne, VIC (hybrid)
        Salary: $120,000 – $140,000 AUD
        Experience: 3+ years C# or Java
        Description: Build backend services for our payments platform.
        Stack: C#, .NET 8, Azure, PostgreSQL, React.
        """;

    // =========================================================================
    // PostingEvaluator
    // =========================================================================

    // Verifies structural contract: all required fields present, recommendation is a valid enum.
    [Fact]
    [Trait("Category", "contract")]
    public async Task PostingEvaluator_ReturnsValidStructuredOutput()
    {
        if (ApiKey is null) return;

        var evaluator = new PostingEvaluator(ApiKey);
        var result = await evaluator.EvaluateAsync(Make.OwnerProfile(), SamplePosting, "https://example.com/job/1");

        Assert.False(string.IsNullOrEmpty(result.Company));
        Assert.False(string.IsNullOrEmpty(result.RoleTitle));
        Assert.Contains(result.Recommendation, new List<string> { "strong_match", "good_match", "weak_match", "discard" });
        Assert.Contains(result.SponsorshipVerdict, new List<string> { "pass", "discard" });
        Assert.Contains(result.LocationMatch, new List<string> { "preferred", "acceptable", "weak" });
        Assert.Contains(result.ExperienceMatch, new List<string> { "ideal", "acceptable", "excluded" });
        Assert.NotEmpty(result.SkillMatches);
        Assert.All(result.SkillMatches, s => Assert.Contains(s.Match, new List<string> { "strong", "good", "acceptable", "excluded" }));
        Assert.Contains(result.SalaryAssessment, new List<string> { "target", "acceptable", "flagged_low", "flagged_high", "missing" });
        Assert.NotNull(result.OrangeFlags);
        Assert.False(string.IsNullOrEmpty(result.Rationale));
    }

    // =========================================================================
    // PostingPreFilterAgent
    // =========================================================================

    // Verifies structural contract: a posting with an explicit sponsorship exclusion is flagged
    // with a valid disqualifier id and non-empty quoted evidence.
    [Fact]
    [Trait("Category", "contract")]
    public async Task PostingPreFilterAgent_ExplicitSponsorshipExclusion_FlagsDisqualifier()
    {
        if (ApiKey is null) return;

        const string posting = """
            Company: Acme Software
            Role: Software Engineer
            Location: Melbourne, VIC (hybrid)
            Description: Build backend services for our payments platform.
            Stack: C#, .NET 8, Azure, PostgreSQL, React.
            Note: We are unable to offer visa sponsorship for this role. Applicants must have full working rights in Australia.
            """;

        var agent = new PostingPreFilterAgent(ApiKey);
        var result = await agent.PreFilterAsync(Make.OwnerProfile(), posting, "https://example.com/job/2");

        Assert.Equal("sponsorship", result.DisqualifierHit);
        Assert.False(string.IsNullOrWhiteSpace(result.Evidence));
    }

    // Verifies structural contract: a clean posting with no disqualifier trigger comes back with
    // both fields unset, and any value that is set is a valid enum id.
    [Fact]
    [Trait("Category", "contract")]
    public async Task PostingPreFilterAgent_CleanPosting_ReturnsValidStructuredOutput()
    {
        if (ApiKey is null) return;

        var agent = new PostingPreFilterAgent(ApiKey);
        var result = await agent.PreFilterAsync(Make.OwnerProfile(), SamplePosting, "https://example.com/job/1");

        if (result.DisqualifierHit is not null)
        {
            Assert.Contains(result.DisqualifierHit, new List<string> { "sponsorship", "php_primary", "gambling", "solo_engineer" });
        }
        else
        {
            Assert.Null(result.Evidence);
        }
    }

    // =========================================================================
    // PostingPreFilterAgent — regression cases from the production eval suite
    //
    // These pin down specific failure modes a live eval run against real production postings
    // (see the PR description for the full methodology and numbers) found in claude-haiku-4-5's
    // behavior on this prompt, each fixed by either tightening prefilter_posting.md or — for the
    // php_primary case, where prompt tightening alone plateaued after several iterations — a
    // deterministic code-level guard in PostingPreFilterAgent.RejectUnsupportedPhpPrimary. Kept
    // as contract tests (real API calls) rather than unit tests because the failure mode is a
    // model behavior, not a parsing bug — a prompt or model regression here needs a live call to
    // catch. Make.OwnerProfile() loads the checked-in context/job_criteria.yaml fixture, whose
    // sponsorship.candidate_status is explicitly non-citizen/non-PR with no current work visa
    // (candidate_status.citizen_or_permanent_resident: false, has_current_work_visa: false) —
    // real signal is expected to still apply to this fixture.
    // =========================================================================

    // The exact real-world failure this regresses: the model repeatedly flagged php_primary on
    // Java/Python postings with reasoning like "the primary backend is Java, not C#/.NET" — a
    // stack-preference judgment call that belongs to the full evaluator, not this narrow check.
    [Theory]
    [Trait("Category", "contract")]
    [InlineData("Company: Acme\nRole: Core Java Developer\nDescription: Build backend services in Java and Spring Boot.")]
    [InlineData("Company: Acme\nRole: Fullstack Developer (Python/React)\nDescription: Backend in Python/Django, frontend in React.")]
    public async Task PostingPreFilterAgent_NonPhpBackend_NeverFlagsPhpPrimary(string posting)
    {
        if (ApiKey is null) return;

        var agent = new PostingPreFilterAgent(ApiKey);
        var result = await agent.PreFilterAsync(Make.OwnerProfile(), posting, "https://example.com/job/3");

        Assert.NotEqual("php_primary", result.DisqualifierHit);
    }

    // A posting whose backend genuinely is PHP must still be caught — the guard above must not
    // overcorrect into suppressing every php_primary hit.
    [Fact]
    [Trait("Category", "contract")]
    public async Task PostingPreFilterAgent_ActualPhpBackend_FlagsPhpPrimary()
    {
        if (ApiKey is null) return;

        const string posting = """
            Company: Acme Software
            Role: Backend Developer
            Description: Build and maintain our e-commerce platform.
            Stack: PHP 8, Laravel, MySQL.
            """;

        var agent = new PostingPreFilterAgent(ApiKey);
        var result = await agent.PreFilterAsync(Make.OwnerProfile(), posting, "https://example.com/job/4");

        Assert.Equal("php_primary", result.DisqualifierHit);
    }

    // Regresses a real miss: the model flagged "gambling" on a hedge fund / quantitative trading
    // firm ("systematic hedge fund that combines quantitative research... to trade FX and
    // futures markets") — financial risk-taking isn't gambling.
    [Fact]
    [Trait("Category", "contract")]
    public async Task PostingPreFilterAgent_HedgeFund_DoesNotFlagGambling()
    {
        if (ApiKey is null) return;

        const string posting = """
            Company: Acme Capital
            Role: Quantitative Software Engineer
            Description: Join our systematic hedge fund that combines quantitative research,
            machine learning, and sophisticated software engineering to trade FX and futures markets.
            Stack: C#, .NET, Python.
            """;

        var agent = new PostingPreFilterAgent(ApiKey);
        var result = await agent.PreFilterAsync(Make.OwnerProfile(), posting, "https://example.com/job/5");

        Assert.NotEqual("gambling", result.DisqualifierHit);
    }

    // A company whose core business genuinely is gambling must still be caught.
    [Fact]
    [Trait("Category", "contract")]
    public async Task PostingPreFilterAgent_OnlineCasino_FlagsGambling()
    {
        if (ApiKey is null) return;

        const string posting = """
            Company: Acme Bet
            Role: Backend Engineer
            Description: We operate a leading online casino and sports betting platform. Build
            the backend systems powering our real-money wagering products.
            Stack: C#, .NET, Azure.
            """;

        var agent = new PostingPreFilterAgent(ApiKey);
        var result = await agent.PreFilterAsync(Make.OwnerProfile(), posting, "https://example.com/job/6");

        Assert.Equal("gambling", result.DisqualifierHit);
    }

    // Regresses a real miss: the model flagged "solo_engineer" on a sales role ("this is a sales
    // role... not an engineering position") — using it as a catch-all for "wrong role type"
    // rather than the narrow "you'd be the only engineer" meaning.
    [Fact]
    [Trait("Category", "contract")]
    public async Task PostingPreFilterAgent_SalesRole_DoesNotFlagSoloEngineer()
    {
        if (ApiKey is null) return;

        const string posting = """
            Company: Acme Data
            Role: Sales Specialist Account Executive
            Description: Drive new business for our data platform. Own the full sales cycle from
            prospecting through close, working closely with our solutions engineering team.
            """;

        var agent = new PostingPreFilterAgent(ApiKey);
        var result = await agent.PreFilterAsync(Make.OwnerProfile(), posting, "https://example.com/job/7");

        Assert.NotEqual("solo_engineer", result.DisqualifierHit);
    }

    // A posting that genuinely states the candidate would be the only engineer must still be
    // caught.
    [Fact]
    [Trait("Category", "contract")]
    public async Task PostingPreFilterAgent_ExplicitSoloEngineerLanguage_FlagsSoloEngineer()
    {
        if (ApiKey is null) return;

        const string posting = """
            Company: Acme Startup
            Role: Founding Software Engineer
            Description: You will be our sole engineer — there is no other technical person at
            the company today, and you'll own the entire stack end to end.
            Stack: C#, .NET, React.
            """;

        var agent = new PostingPreFilterAgent(ApiKey);
        var result = await agent.PreFilterAsync(Make.OwnerProfile(), posting, "https://example.com/job/8");

        Assert.Equal("solo_engineer", result.DisqualifierHit);
    }

    // Sponsorship must be gated on the candidate's own citizen/PR status, not the posting alone.
    // A citizen/PR candidate is never affected by "no sponsorship" or "citizens only" language.
    [Fact]
    [Trait("Category", "contract")]
    public async Task PostingPreFilterAgent_SponsorshipExclusion_CitizenCandidate_DoesNotFlag()
    {
        if (ApiKey is null) return;

        var profile = Make.OwnerProfile();
        profile.JobCriteria = profile.JobCriteria.Replace(
            "citizen_or_permanent_resident: false",
            "citizen_or_permanent_resident: true");

        const string posting = """
            Company: Acme Software
            Role: Software Engineer
            Description: Build backend services.
            Note: No visa sponsorship offered. Must have full working rights in Australia.
            """;

        var agent = new PostingPreFilterAgent(ApiKey);
        var result = await agent.PreFilterAsync(profile, posting, "https://example.com/job/9");

        Assert.Null(result.DisqualifierHit);
    }

    // =========================================================================
    // EmailClassifier
    // =========================================================================

    // Verifies structural contract: IsJobRelated is set, Category is valid enum, Confidence in [0,1].
    [Fact]
    [Trait("Category", "contract")]
    public async Task EmailClassifier_ReturnsValidStructuredOutput()
    {
        if (ApiKey is null) return;

        var classifier = new EmailClassifier(ApiKey);
        var email = new RawEmail(
            "msg-contract-1", "thread-1",
            "noreply@seek.com.au",
            "Your application to Acme Software",
            "Thank you for applying for the Software Engineer role at Acme Software. We will be in touch.",
            DateTimeOffset.UtcNow);

        var result = await classifier.ClassifyAsync(email, userId: 1);

        Assert.Contains(result.Category, new List<string>
        {
            "application_confirmation", "rejection", "interview_invitation",
            "recruiter_outreach", "scheduling_request", "offer",
            "follow_up_needed", "job_alert", "not_relevant",
        });
        Assert.InRange(result.Confidence, 0.0, 1.0);
    }

    // =========================================================================
    // CoverLetterAgent
    // =========================================================================

    // Verifies structural contract: non-empty string output of reasonable length.
    [Fact]
    [Trait("Category", "contract")]
    public async Task CoverLetterAgent_ReturnsNonEmptyText()
    {
        if (ApiKey is null) return;

        var agent = new CoverLetterAgent(ApiKey);
        var evalJson = JsonSerializer.Serialize(new
        {
            recommendation = "good_match",
            company = "Acme Software",
            role_title = "Software Engineer",
            backend_match = "strong",
            rationale = "Strong C# match, payments domain relevant.",
        });

        var result = await agent.GenerateAsync(Make.OwnerProfile(), SamplePosting, evalJson);

        Assert.False(string.IsNullOrWhiteSpace(result));
        Assert.True(result.Length >= 100, $"Expected ≥100 chars, got {result.Length}");
    }

    // =========================================================================
    // CvTailorAgent
    // =========================================================================

    // Verifies structural contract: non-empty string output of reasonable length.
    [Fact]
    [Trait("Category", "contract")]
    public async Task CvTailorAgent_ReturnsNonEmptyText()
    {
        if (ApiKey is null) return;

        var agent = new CvTailorAgent(ApiKey);
        var evalJson = JsonSerializer.Serialize(new
        {
            recommendation = "good_match",
            backend_match = "strong",
            company_assessment = "preferred",
            role_type_match = "preferred",
        });

        var result = await agent.GenerateAsync(Make.OwnerProfile(), Make.OwnerResume(), SamplePosting, evalJson);

        Assert.False(string.IsNullOrWhiteSpace(result));
        Assert.True(result.Length >= 200, $"Expected ≥200 chars, got {result.Length}");
    }

    // Verifies structural contract: a prior draft + feedback turn produces changed, non-empty output.
    [Fact]
    [Trait("Category", "contract")]
    public async Task CvTailorAgent_ReviseAsync_ProducesChangedOutput()
    {
        if (ApiKey is null) return;

        var agent = new CvTailorAgent(ApiKey);
        var evalJson = JsonSerializer.Serialize(new { recommendation = "good_match", backend_match = "strong" });
        var profile = Make.OwnerProfile();
        var resume = Make.OwnerResume();
        var original = await agent.GenerateAsync(profile, resume, SamplePosting, evalJson);

        var history = new List<AgentThreadTurn>
        {
            new("user", CvTailorAgent.BuildInitialUserContent(SamplePosting, evalJson)),
            new("assistant", original),
            new("user", "Please revise the previous draft with this feedback: mention Docker experience in the summary."),
        };
        var revised = await agent.ReviseAsync(profile, resume, history);

        Assert.False(string.IsNullOrWhiteSpace(revised));
        Assert.NotEqual(original, revised);
    }

    // =========================================================================
    // AnswerAgent
    // =========================================================================

    // A vague question with no job context should trigger a clarifying question, not a guess.
    [Fact]
    [Trait("Category", "contract")]
    public async Task AnswerAgent_VagueQuestionNoContext_AsksFollowup()
    {
        if (ApiKey is null) return;

        var agent = new AnswerAgent(ApiKey);
        var history = new List<AgentThreadTurn>
        {
            new("user", AnswerAgent.BuildInitialUserContent("Why do you want to work here specifically?", null)),
        };

        var (mode, content) = await agent.RespondAsync(Make.OwnerProfile(), history);

        Assert.Equal("ask_followup", mode);
        Assert.False(string.IsNullOrWhiteSpace(content));
    }

    // A specific, well-grounded question should be answerable directly from background.yaml alone.
    [Fact]
    [Trait("Category", "contract")]
    public async Task AnswerAgent_SpecificQuestion_ReturnsFinalAnswer()
    {
        if (ApiKey is null) return;

        var agent = new AnswerAgent(ApiKey);
        var history = new List<AgentThreadTurn>
        {
            new("user", AnswerAgent.BuildInitialUserContent(
                "Describe a technically challenging feature you owned end to end.", null)),
        };

        var (mode, content) = await agent.RespondAsync(Make.OwnerProfile(), history);

        Assert.Equal("final_answer", mode);
        Assert.False(string.IsNullOrWhiteSpace(content));
    }

    // =========================================================================
    // ResumeSummaryAgent
    // =========================================================================

    // Verifies structural contract: non-empty summary text of reasonable length, obeying the
    // shared hard constraints (no colons, no em dashes).
    [Fact]
    [Trait("Category", "contract")]
    public async Task ResumeSummaryAgent_ReturnsNonEmptySummaryGroundedInBackground()
    {
        if (ApiKey is null) return;

        var agent = new ResumeSummaryAgent(ApiKey);
        var profile = Make.OwnerProfile();

        var summary = await agent.GenerateAsync(userId: 1, profile.Background, targetJobTitles: ["Software Engineer"]);

        Assert.False(string.IsNullOrWhiteSpace(summary));
        Assert.True(summary.Length >= 50, $"Expected >=50 chars, got {summary.Length}");
        Assert.DoesNotContain(':', summary);
        Assert.DoesNotContain('—', summary);
    }

    // Verifies the agent still produces a reasonable, non-empty summary with no target job
    // titles given at all (the "generic, role-agnostic" branch generate_resume_summary.md
    // describes).
    [Fact]
    [Trait("Category", "contract")]
    public async Task ResumeSummaryAgent_NoTargetJobTitles_StillReturnsNonEmptySummary()
    {
        if (ApiKey is null) return;

        var agent = new ResumeSummaryAgent(ApiKey);
        var profile = Make.OwnerProfile();

        var summary = await agent.GenerateAsync(userId: 1, profile.Background, targetJobTitles: []);

        Assert.False(string.IsNullOrWhiteSpace(summary));
    }

    // =========================================================================
    // ResumeIntakeAgent
    // =========================================================================

    private const string SampleResumeText = """
        Jordan Lee
        jordan.lee@example.com | Sydney, NSW | linkedin.com/in/jordanlee

        Experience

        Software Engineer, Acme Corp — Sydney, NSW — Jan 2022 to Present
        - Built and maintained backend services in C# and ASP.NET Core for an e-commerce platform.
        - Migrated a legacy monolith to a set of REST APIs, reducing deploy time by half.
        - Mentored two junior engineers.

        Junior Developer, Beta Pty Ltd — Melbourne, VIC — Jun 2020 to Dec 2021
        - Built internal tooling in Python and Django.

        Education

        Bachelor of Computer Science, University of Sydney — Graduated 2020

        Skills
        C#, ASP.NET Core, Python, Django, PostgreSQL, Docker, Git
        """;

    // Verifies structural contract: both outputs are non-empty, background_yaml contains the
    // expected top-level sections, cv_base_markdown leaves the Summary as the placeholder
    // (a later tailoring step fills it in, not this one).
    [Fact]
    [Trait("Category", "contract")]
    public async Task ResumeIntakeAgent_ParseFromTextAsync_ReturnsStructuredOutput()
    {
        if (ApiKey is null) return;

        var agent = new ResumeIntakeAgent(ApiKey);
        var result = await agent.ParseFromTextAsync(userId: 1, SampleResumeText);

        Assert.False(string.IsNullOrWhiteSpace(result.Background));
        Assert.Contains("personal:", result.Background);
        Assert.Contains("experience:", result.Background);
        Assert.Contains("education:", result.Background);
        Assert.Contains("skills:", result.Background);

        Assert.False(string.IsNullOrWhiteSpace(result.CvBase));
        Assert.Contains("[Fresh summary specific to this role; see tailoring instructions]", result.CvBase);
        Assert.Contains("Acme Corp", result.CvBase);
    }
}
