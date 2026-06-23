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
        var result = await evaluator.EvaluateAsync(SamplePosting, "https://example.com/job/1");

        Assert.False(string.IsNullOrEmpty(result.Company));
        Assert.False(string.IsNullOrEmpty(result.RoleTitle));
        Assert.Contains(result.Recommendation, new List<string> { "strong_match", "good_match", "weak_match", "discard" });
        Assert.Contains(result.SponsorshipVerdict, new List<string> { "pass", "discard" });
        Assert.Contains(result.LocationMatch, new List<string> { "preferred", "acceptable", "weak" });
        Assert.Contains(result.ExperienceMatch, new List<string> { "ideal", "acceptable", "excluded" });
        Assert.Contains(result.BackendMatch, new List<string> { "strong", "good", "acceptable", "excluded" });
        Assert.Contains(result.FrontendMatch, new List<string> { "strong", "good", "acceptable" });
        Assert.Contains(result.SalaryAssessment, new List<string> { "target", "acceptable", "flagged_low", "flagged_high", "missing" });
        Assert.NotNull(result.OrangeFlags);
        Assert.False(string.IsNullOrEmpty(result.Rationale));
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

        var result = await classifier.ClassifyAsync(email);

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

        var result = await agent.GenerateAsync(SamplePosting, evalJson);

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

        var result = await agent.GenerateAsync(SamplePosting, evalJson);

        Assert.False(string.IsNullOrWhiteSpace(result));
        Assert.True(result.Length >= 200, $"Expected ≥200 chars, got {result.Length}");
    }
}
