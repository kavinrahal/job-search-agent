using System.Text.Json;
using Anthropic;
using Anthropic.Models.Messages;
using JobSearchAgent.Models;

namespace JobSearchAgent.Agents;

public class EmailClassification
{
    public bool IsJobRelated { get; init; }
    public string Category { get; init; } = "not_relevant";
    public double Confidence { get; init; }
    public string Company { get; init; } = "";
    public string RoleTitle { get; init; } = "";
}

public class EmailClassifier
{
    private readonly AnthropicClient _client;
    private const string HaikuModel = "claude-haiku-4-5";

    private readonly string _systemPrompt;
    private readonly Tool _tool;

    public EmailClassifier(string apiKey)
    {
        _client = new AnthropicClient { ApiKey = apiKey };

        string categoriesText = LoadSkill("email_categories.md");
        _systemPrompt = $"""
            You are an email classifier for a software engineer's active job search.
            Determine whether each email is job-search related and assign the appropriate category.
            Be conservative: only mark an email as job-related if it clearly relates to employment, applications, or recruitment.

            {categoriesText}
            """;

        _tool = new Tool
        {
            Name = "classify_email",
            Description = "Classify whether an email is job-search related and assign a category.",
            InputSchema = new InputSchema
            {
                Properties = new Dictionary<string, JsonElement>
                {
                    ["is_job_related"] = JsonSerializer.SerializeToElement(new
                    {
                        type = "boolean",
                        description = "True only if this email clearly relates to a job search or employment opportunity.",
                    }),
                    ["category"] = JsonSerializer.SerializeToElement(new
                    {
                        type = "string",
                        @enum = new[]
                        {
                            "application_confirmation", "rejection", "interview_invitation",
                            "recruiter_outreach", "scheduling_request", "offer",
                            "follow_up_needed", "job_alert", "not_relevant",
                        },
                    }),
                    ["confidence"] = JsonSerializer.SerializeToElement(new
                    {
                        type = "number",
                        description = "Confidence score 0.0–1.0.",
                    }),
                    ["company"] = JsonSerializer.SerializeToElement(new
                    {
                        type = "string",
                        description = "Company name if identifiable, else empty string.",
                    }),
                    ["role_title"] = JsonSerializer.SerializeToElement(new
                    {
                        type = "string",
                        description = "Job title if mentioned, else empty string.",
                    }),
                },
                Required = ["is_job_related", "category", "confidence", "company", "role_title"],
            },
            CacheControl = new CacheControlEphemeral(),
        };
    }

    public async Task<EmailClassification> ClassifyAsync(RawEmail email)
    {
        string body = email.BodyText.Length > 1500
            ? email.BodyText[..1500]
            : email.BodyText;

        string userContent = $"""
            From: {email.FromAddress}
            Subject: {email.Subject}

            {body}
            """;

        var response = await _client.Messages.Create(new MessageCreateParams
        {
            Model = HaikuModel,
            MaxTokens = 256,
            System = new List<TextBlockParam>
            {
                new()
                {
                    Text = _systemPrompt,
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
                var input = toolUse.Input;
                return new EmailClassification
                {
                    IsJobRelated = input["is_job_related"].GetBoolean(),
                    Category = input["category"].GetString() ?? "not_relevant",
                    Confidence = input["confidence"].GetDouble(),
                    Company = input["company"].GetString() ?? "",
                    RoleTitle = input["role_title"].GetString() ?? "",
                };
            }
        }

        throw new InvalidOperationException("Classifier did not return a tool use block.");
    }

    public async Task<List<(RawEmail Email, EmailClassification Classification)>> ClassifyBatchAsync(
        List<RawEmail> emails, int maxConcurrency = 15)
    {
        var semaphore = new SemaphoreSlim(maxConcurrency, maxConcurrency);
        int completed = 0;
        var results = new (RawEmail Email, EmailClassification Classification)?[emails.Count];

        await Task.WhenAll(emails.Select(async (email, i) =>
        {
            await semaphore.WaitAsync();
            try
            {
                results[i] = (email, await ClassifyAsync(email));
                int n = Interlocked.Increment(ref completed);
                if (n % 10 == 0 || n == emails.Count)
                    Console.WriteLine($"  classified {n}/{emails.Count}...");
            }
            finally
            {
                semaphore.Release();
            }
        }));

        return [.. results.Where(r => r.HasValue).Select(r => r!.Value)];
    }

    private static string LoadSkill(string filename)
    {
        // Walk up from the working directory to find the skills/ folder
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (dir != null)
        {
            string path = Path.Combine(dir.FullName, "skills", filename);
            if (File.Exists(path)) return File.ReadAllText(path);
            dir = dir.Parent;
        }
        throw new FileNotFoundException($"skills/{filename} not found in any ancestor directory.");
    }
}
