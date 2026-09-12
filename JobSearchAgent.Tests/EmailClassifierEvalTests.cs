using System.Text.Json;
using JobSearch.Data;
using JobSearchAgent.Agents;
using JobSearchAgent.Models;

namespace JobSearchAgent.Tests;

// Golden-dataset accuracy eval for EmailClassifier.
//
// HOW TO RUN (contract test: makes real, billed Claude API calls, excluded from the default
// `dotnet test` / CI run the same way as the rest of ContractTests.cs -- see ci.yml's
// `--filter "Category!=contract"`):
//
//   ANTHROPIC_API_KEY=sk-... dotnet test --filter "FullyQualifiedName~EmailClassifierEvalTests"
//
// HOW TO READ THE OUTPUT (visible with `dotnet test ... --logger "console;verbosity=detailed"`,
// or in the "Standard Output" section of a single test's result in most IDE test runners):
//
//   EmailClassifier golden-dataset accuracy (skill_version=3f2a9c1d4e5b6a7c):
//     application_confirmation  5/5   (100%)
//     follow_up_needed          5/5   (100%)
//     interview_invitation      5/6   (83%)
//     job_alert                 6/6   (100%)
//     not_relevant              8/8   (100%)
//     offer                     5/5   (100%)
//     recruiter_outreach        6/6   (100%)
//     rejection                 6/6   (100%)
//     scheduling_request        5/5   (100%)
//     TOTAL                     51/52 (98%)
//   Misclassifications:
//     [interview_invite_02] expected=interview_invitation actual=recruiter_outreach conf=0.62 -- ...
//
// `skill_version` is SkillLoader.Version(email_categories.md) at the moment the classifier was
// constructed -- note it in a PR/commit description alongside the score so a future regression
// can be attributed to the exact skill-file text that produced it (see ClaudeUsageLog.SkillVersion
// for the same idea applied to production usage rows).
//
// The dataset lives at Fixtures/email_classification_golden.json (hand-written, no real user
// data/PII) -- 52 examples across all 9 categories, weighted toward deliberately ambiguous cases
// (a rejection that sounds like recruiter_outreach, a job-alert digest with zero new matches, a
// bootcamp-marketing email disguised as an opportunity pitch, etc.) called out in each example's
// "notes" field. Add new tricky real-world cases there as they're discovered.
//
// The assertion is a floor, not a target: it exists to catch a prompt/skill-file change silently
// tanking accuracy, not to demand perfection from an LLM classifier on deliberately hard cases.
public class EmailClassifierEvalTests
{
    private const double AccuracyFloor = 0.75;

    private static string? ApiKey => Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");

    private record GoldenExample(
        string Id, string From, string Subject, string Body, string ExpectedCategory, string Notes);

    private static List<GoldenExample> LoadGoldenDataset()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "email_classification_golden.json");
        string json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<List<GoldenExample>>(json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
    }

    [Fact]
    [Trait("Category", "contract")]
    public async Task EmailClassifier_GoldenDataset_MeetsAccuracyFloor()
    {
        if (ApiKey is null) return;

        var dataset = LoadGoldenDataset();
        var classifier = new EmailClassifier(ApiKey);
        string skillVersion = SkillLoader.Version(SkillLoader.Load("email_categories.md"));

        var emails = dataset
            .Select(e => new RawEmail(e.Id, e.Id, e.From, e.Subject, e.Body, DateTimeOffset.UtcNow))
            .ToList();
        var results = await classifier.ClassifyBatchAsync(emails, userId: 1);

        var perCategoryTotal = new SortedDictionary<string, int>(StringComparer.Ordinal);
        var perCategoryCorrect = new Dictionary<string, int>();
        var misses = new List<string>();

        for (int i = 0; i < dataset.Count; i++)
        {
            var example = dataset[i];
            var actual = results[i].Classification.Category;

            perCategoryTotal[example.ExpectedCategory] = perCategoryTotal.GetValueOrDefault(example.ExpectedCategory) + 1;
            if (actual == example.ExpectedCategory)
            {
                perCategoryCorrect[example.ExpectedCategory] = perCategoryCorrect.GetValueOrDefault(example.ExpectedCategory) + 1;
            }
            else
            {
                misses.Add($"  [{example.Id}] expected={example.ExpectedCategory} actual={actual} " +
                    $"conf={results[i].Classification.Confidence:F2} -- {example.Notes}");
            }
        }

        int totalCorrect = perCategoryCorrect.Values.Sum();
        int totalCount = dataset.Count;
        double overallAccuracy = (double)totalCorrect / totalCount;

        Console.WriteLine($"EmailClassifier golden-dataset accuracy (skill_version={skillVersion}):");
        foreach (var (category, total) in perCategoryTotal)
        {
            int correct = perCategoryCorrect.GetValueOrDefault(category);
            Console.WriteLine($"  {category,-24} {correct}/{total} ({100.0 * correct / total:F0}%)");
        }
        Console.WriteLine($"  {"TOTAL",-24} {totalCorrect}/{totalCount} ({100.0 * overallAccuracy:F0}%)");

        if (misses.Count > 0)
        {
            Console.WriteLine("Misclassifications:");
            foreach (var miss in misses) Console.WriteLine(miss);
        }

        Assert.True(overallAccuracy >= AccuracyFloor,
            $"Golden-dataset accuracy {overallAccuracy:P0} fell below the {AccuracyFloor:P0} floor " +
            $"-- see misclassifications above.");
    }
}
