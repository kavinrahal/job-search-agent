using System.Text;
using System.Text.RegularExpressions;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Gmail.v1;
using Google.Apis.Gmail.v1.Data;
using Google.Apis.Services;
using Google.Apis.Util.Store;
using JobSearchAgent.Models;

namespace JobSearchAgent.Integrations;

public class GmailClient
{
    private readonly GmailService _service;

    private GmailClient(GmailService service) => _service = service;

    public static async Task<GmailClient> CreateAsync(string credentialsPath, string tokenStorePath)
    {
        await using var stream = File.OpenRead(credentialsPath);
        var credential = await GoogleWebAuthorizationBroker.AuthorizeAsync(
            GoogleClientSecrets.FromStream(stream).Secrets,
            [GmailService.Scope.GmailReadonly],
            "user",
            CancellationToken.None,
            new FileDataStore(tokenStorePath, fullPath: true)
        );

        var service = new GmailService(new BaseClientService.Initializer
        {
            HttpClientInitializer = credential,
            ApplicationName = "JobSearchAgent",
        });

        return new GmailClient(service);
    }

    public async Task<List<RawEmail>> FetchEmailsSinceAsync(DateTimeOffset? since, DateTimeOffset? until = null)
    {
        string q = since.HasValue
            ? $"after:{since.Value.ToUnixTimeSeconds()}"
            : "newer_than:1d";

        if (until.HasValue)
            q += $" before:{until.Value.ToUnixTimeSeconds()}";

        var messageRefs = new List<Message>();
        string? pageToken = null;

        do
        {
            var listReq = _service.Users.Messages.List("me");
            listReq.Q = q;
            if (pageToken != null) listReq.PageToken = pageToken;

            var result = await listReq.ExecuteAsync();
            if (result.Messages != null) messageRefs.AddRange(result.Messages);
            pageToken = result.NextPageToken;
        } while (pageToken != null);

        var emails = new List<RawEmail>();
        foreach (var msgRef in messageRefs)
        {
            var getReq = _service.Users.Messages.Get("me", msgRef.Id);
            getReq.Format = UsersResource.MessagesResource.GetRequest.FormatEnum.Full;
            var msg = await getReq.ExecuteAsync();
            emails.Add(ParseMessage(msg));
        }

        return emails;
    }

    private static RawEmail ParseMessage(Message msg)
    {
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var h in msg.Payload.Headers)
            headers.TryAdd(h.Name, h.Value);

        var receivedAt = DateTimeOffset.FromUnixTimeMilliseconds(msg.InternalDate ?? 0);

        return new RawEmail(
            MessageId: msg.Id,
            ThreadId: msg.ThreadId,
            FromAddress: headers.GetValueOrDefault("from", ""),
            Subject: headers.GetValueOrDefault("subject", "(no subject)"),
            BodyText: ExtractBody(msg.Payload),
            ReceivedAt: receivedAt
        );
    }

    private static string DecodeBase64Url(string data)
    {
        // Gmail uses URL-safe base64; convert to standard and pad
        string standard = data.Replace('-', '+').Replace('_', '/');
        int padding = (4 - standard.Length % 4) % 4;
        standard += new string('=', padding);
        return Encoding.UTF8.GetString(Convert.FromBase64String(standard));
    }

    private static string StripHtml(string html)
    {
        string noTags = Regex.Replace(html, "<[^>]+>", " ");
        return Regex.Replace(noTags, @"\s+", " ").Trim();
    }

    private static string ExtractBody(MessagePart payload)
    {
        string mimeType = payload.MimeType ?? "";
        string bodyData = payload.Body?.Data ?? "";

        if (mimeType == "text/plain" && !string.IsNullOrEmpty(bodyData))
            return DecodeBase64Url(bodyData);

        if (payload.Parts != null)
        {
            // Prefer an immediate text/plain child before recursing
            foreach (var part in payload.Parts)
            {
                if (part.MimeType == "text/plain")
                {
                    string data = part.Body?.Data ?? "";
                    if (!string.IsNullOrEmpty(data)) return DecodeBase64Url(data);
                }
            }
            // Recurse into nested multipart nodes
            foreach (var part in payload.Parts)
            {
                string result = ExtractBody(part);
                if (!string.IsNullOrEmpty(result)) return result;
            }
        }

        if (mimeType == "text/html" && !string.IsNullOrEmpty(bodyData))
            return StripHtml(DecodeBase64Url(bodyData));

        return "";
    }
}
