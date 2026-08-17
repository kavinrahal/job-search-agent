using System.Text.Json;
using Anthropic;
using Anthropic.Models.Messages;

namespace JobSearch.Data;

public record ParsedResume(string Background, string CvBase);

public class ResumeIntakeAgent
{
    private readonly AnthropicClient _client;
    private const string OpusModel = "claude-opus-4-8";
    private const int MaxTokens = 8000;
    private readonly string _skillText;
    private readonly Tool _backgroundTool;
    private readonly Tool _cvBaseTool;
    private readonly ClaudeUsageLogger? _usageLogger;

    public ResumeIntakeAgent(string apiKey, ClaudeUsageLogger? usageLogger = null)
    {
        _client = new AnthropicClient { ApiKey = apiKey };
        _skillText = SkillLoader.Load("parse_resume_intake.md");
        _usageLogger = usageLogger;

        // Two separate tools/calls rather than one combined one — a single call asking for
        // both background_yaml and cv_base_markdown at once means the two fields compete for
        // the same MaxTokens budget. For a dense resume, one field can consume most of it,
        // leaving the response cut off before the other field is even started — that failed in
        // production twice (missing field at 4000 tokens, then again at 8000, ~1 minute each
        // time). Splitting gives each field its own full budget with no competition, and
        // running them in parallel keeps latency roughly the same as the single-call version.
        _backgroundTool = new Tool
        {
            Name = "submit_background",
            Description = "Submit the parsed background data extracted from the resume.",
            InputSchema = new InputSchema
            {
                Properties = new Dictionary<string, JsonElement>
                {
                    ["background_yaml"] = Prop("string", "YAML with personal/experience/education/skills/projects sections."),
                },
                Required = ["background_yaml"],
            },
            CacheControl = new CacheControlEphemeral(),
        };

        _cvBaseTool = new Tool
        {
            Name = "submit_cv_base",
            Description = "Submit the base CV extracted from the resume.",
            InputSchema = new InputSchema
            {
                Properties = new Dictionary<string, JsonElement>
                {
                    ["cv_base_markdown"] = Prop("string", "Base CV in Markdown, Summary section left as the placeholder."),
                },
                Required = ["cv_base_markdown"],
            },
            CacheControl = new CacheControlEphemeral(),
        };
    }

    public Task<ParsedResume> ParseFromTextAsync(int userId, string resumeText) =>
        ParseAsync(userId, [new TextBlockParam { Text = resumeText }]);

    public Task<ParsedResume> ParseFromPdfAsync(int userId, byte[] pdfBytes) =>
        ParseAsync(userId, [
            new TextBlockParam { Text = "Resume attached as a PDF." },
            new DocumentBlockParam(new Base64PdfSource(Convert.ToBase64String(pdfBytes))),
        ]);

    private async Task<ParsedResume> ParseAsync(int userId, List<ContentBlockParam> content)
    {
        var backgroundTask = ExtractFieldAsync(userId, content, _backgroundTool, "background_yaml");
        var cvBaseTask = ExtractFieldAsync(userId, content, _cvBaseTool, "cv_base_markdown");
        await Task.WhenAll(backgroundTask, cvBaseTask);
        return new ParsedResume(backgroundTask.Result, cvBaseTask.Result);
    }

    private async Task<string> ExtractFieldAsync(int userId, List<ContentBlockParam> content, Tool tool, string fieldName)
    {
        var response = await _client.Messages.Create(new MessageCreateParams
        {
            Model = OpusModel,
            MaxTokens = MaxTokens,
            System = new List<TextBlockParam>
            {
                new() { Text = _skillText, CacheControl = new CacheControlEphemeral() },
            },
            Tools = [tool],
            ToolChoice = new ToolChoiceAny(),
            Messages = [new() { Role = Role.User, Content = content }],
        });

        if (_usageLogger is not null)
            await _usageLogger.LogAsync(userId, ClaudeAgentName.ResumeIntakeAgent, OpusModel, response.Usage);

        foreach (var block in response.Content)
        {
            if (block.TryPickToolUse(out ToolUseBlock? toolUse))
                return ExtractField(toolUse.Input, fieldName);
        }
        throw new InvalidOperationException($"Resume intake did not return a tool use block for \"{fieldName}\".");
    }

    // Split out from ExtractFieldAsync so this failure mode is unit-testable without a live
    // API call. A missing field (rather than a truncated-but-present one) is exactly what a
    // response cut off by MaxTokens looks like.
    public static string ExtractField(IReadOnlyDictionary<string, JsonElement> toolInput, string fieldName)
    {
        if (toolInput.TryGetValue(fieldName, out var el))
            return el.GetString() ?? "";
        throw new InvalidOperationException(
            $"Resume intake response did not include \"{fieldName}\" — the resume is likely too " +
            "dense to fit the current token budget. Increase MaxTokens above if this recurs.");
    }

    private static JsonElement Prop(string type, string description) =>
        JsonSerializer.SerializeToElement(new { type, description });
}
