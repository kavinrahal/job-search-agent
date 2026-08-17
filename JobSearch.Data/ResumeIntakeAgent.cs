using System.Text.Json;
using Anthropic;
using Anthropic.Models.Messages;

namespace JobSearch.Data;

public record ParsedResume(string Background, string CvBase);

public class ResumeIntakeAgent
{
    private readonly AnthropicClient _client;
    private const string OpusModel = "claude-opus-4-8";
    private readonly string _skillText;
    private readonly Tool _tool;
    private readonly ClaudeUsageLogger? _usageLogger;

    public ResumeIntakeAgent(string apiKey, ClaudeUsageLogger? usageLogger = null)
    {
        _client = new AnthropicClient { ApiKey = apiKey };
        _skillText = SkillLoader.Load("parse_resume_intake.md");
        _usageLogger = usageLogger;

        _tool = new Tool
        {
            Name = "submit_parsed_resume",
            Description = "Submit the parsed background data and base CV extracted from the resume.",
            InputSchema = new InputSchema
            {
                Properties = new Dictionary<string, JsonElement>
                {
                    ["background_yaml"]   = Prop("string", "YAML with personal/experience/education/skills/projects sections."),
                    ["cv_base_markdown"]  = Prop("string", "Base CV in Markdown, Summary section left as the placeholder."),
                },
                Required = ["background_yaml", "cv_base_markdown"],
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
        var response = await _client.Messages.Create(new MessageCreateParams
        {
            Model = OpusModel,
            // Double CvTailorAgent's budget (also 4000) since this has to reproduce the whole
            // resume twice over — once as background_yaml, once as cv_base_markdown — not just
            // once. Too low a cap here doesn't truncate gracefully: it silently drops a whole
            // required tool field, throwing a KeyNotFoundException on a dense resume.
            MaxTokens = 8000,
            System = new List<TextBlockParam>
            {
                new() { Text = _skillText, CacheControl = new CacheControlEphemeral() },
            },
            Tools = [_tool],
            ToolChoice = new ToolChoiceAny(),
            Messages = [new() { Role = Role.User, Content = content }],
        });

        if (_usageLogger is not null)
            await _usageLogger.LogAsync(userId, ClaudeAgentName.ResumeIntakeAgent, OpusModel, response.Usage);

        foreach (var block in response.Content)
        {
            if (block.TryPickToolUse(out ToolUseBlock? toolUse))
                return ExtractParsedResume(toolUse.Input);
        }

        throw new InvalidOperationException("Resume intake did not return a tool use block.");
    }

    // A missing required field (rather than a truncated-but-present one) is exactly what a
    // response cut off by MaxTokens looks like — the model runs out of budget partway through
    // and never starts the second field at all. Split out from ParseAsync so this failure mode
    // is unit-testable without a live API call.
    public static ParsedResume ExtractParsedResume(IReadOnlyDictionary<string, JsonElement> toolInput)
    {
        if (!toolInput.TryGetValue("background_yaml", out var backgroundEl) ||
            !toolInput.TryGetValue("cv_base_markdown", out var cvBaseEl))
            throw new InvalidOperationException(
                "Resume intake response was missing a required field — the resume is likely too " +
                "dense to fit the current token budget. Increase MaxTokens above if this recurs.");
        return new ParsedResume(backgroundEl.GetString() ?? "", cvBaseEl.GetString() ?? "");
    }

    private static JsonElement Prop(string type, string description) =>
        JsonSerializer.SerializeToElement(new { type, description });
}
