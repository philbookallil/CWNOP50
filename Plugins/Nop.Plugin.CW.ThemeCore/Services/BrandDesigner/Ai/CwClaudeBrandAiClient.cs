using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Nop.Plugin.CW.ThemeCore.Services.BrandDesigner.Ai;

/// <summary>
/// The Claude Messages API, called over REST like every other provider. Thinking is left to the model's default and no
/// sampling settings are sent: this API version refuses them.
/// </summary>
public class CwClaudeBrandAiClient : CwBrandAiClientBase
{
    #region Constants

    private const string API_VERSION = "2026-10-21";
    private const string FALLBACK_BETA = "server-side-fallback-2026-07-01";
    private const int MAX_TOKENS = 16000;

    /// <summary>The models that accept the refusal fallback.</summary>
    private static readonly string[] _fallbackModels = ["claude-sonnet-5-5", "claude-opus-5-5", "claude-fable-5-1"];

    #endregion

    #region Ctor

    public CwClaudeBrandAiClient(HttpClient httpClient) : base(httpClient)
    {
    }

    #endregion

    #region Utilities

    private static string ErrorType(string body)
    {
        try
        {
            return (string)JsonNode.Parse(body)?["error"]?["type"];
        }
        catch (Exception e) when (e is JsonException or InvalidOperationException)
        {
            return null;
        }
    }

    protected override (string ResourceKey, bool Retry) MapStatus(int status, string body)
    {
        var type = ErrorType(body);
        return (status, type) switch
        {
            (401, _) or (_, "authentication_error") => (BrandAiMessages.KeyRejected, false),
            (402, _) or (403, _) or (_, "billing_error") or (_, "permission_error") => (BrandAiMessages.AccountProblem, false),
            (400, _) or (_, "invalid_request_error") => (BrandAiMessages.InvalidRequest, false),
            (429, _) or (_, "rate_limit_error") => (BrandAiMessages.RateLimited, true),
            ( >= 500, _) or (_, "overloaded_error") => (BrandAiMessages.Unavailable, true),
            _ => (BrandAiMessages.Unavailable, false)
        };
    }

    private HttpRequestMessage CreateRequest(BrandAiConnection connection, JsonObject body, bool useFallback)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"{connection.BaseUrl.TrimEnd('/')}/v1/messages")
        {
            Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json")
        };
        request.Headers.Add("x-api-key", connection.ApiKey);
        request.Headers.Add("anthropic-version", API_VERSION);
        if (useFallback)
            request.Headers.Add("anthropic-beta", FALLBACK_BETA);

        return request;
    }

    #endregion

    #region Methods

    public override async Task<BrandAiAnswer> GenerateAsync(BrandAiConnection connection, BrandAiRequest request, CancellationToken cancellationToken = default)
    {
        var useFallback = _fallbackModels.Contains(connection.Model, StringComparer.OrdinalIgnoreCase);
        var body = new JsonObject
        {
            ["model"] = connection.Model,
            ["max_tokens"] = MAX_TOKENS,
            ["system"] = new JsonArray(new JsonObject
            {
                ["type"] = "text",
                ["text"] = BrandAiPrompt.System,
                ["cache_control"] = new JsonObject { ["type"] = "ephemeral" }
            }),
            ["messages"] = new JsonArray(new JsonObject { ["role"] = "user", ["content"] = BrandAiPrompt.UserMessage(request) }),
            ["output_config"] = new JsonObject
            {
                ["effort"] = connection.Effort,
                ["format"] = new JsonObject { ["type"] = "json_schema", ["schema"] = JsonNode.Parse(BrandAiPrompt.Schema) }
            }
        };
        if (useFallback)
            body["fallbacks"] = "default";

        var text = await SendAsync(() => CreateRequest(connection, body, useFallback), connection, cancellationToken);
        var response = JsonNode.Parse(text);

        switch ((string)response?["stop_reason"])
        {
            case "refusal":
                throw new BrandAiException(BrandAiMessages.Refused, $"category {response["stop_details"]?["category"]}");
            case "max_tokens":
                throw new BrandAiException(BrandAiMessages.CutOff);
        }

        var textBlock = response?["content"]?.AsArray().FirstOrDefault(block => (string)block?["type"] == "text");
        var answer = BrandAiPrompt.ParseAnswer((string)textBlock?["text"]);

        var usage = response?["usage"];
        answer.InputTokens = (int?)usage?["input_tokens"];
        answer.OutputTokens = (int?)usage?["output_tokens"];
        answer.CachedTokens = (int?)usage?["cache_read_input_tokens"];

        return answer;
    }

    public override async Task TestAsync(BrandAiConnection connection, CancellationToken cancellationToken = default)
    {
        var body = new JsonObject
        {
            ["model"] = connection.Model,
            ["max_tokens"] = 16,
            ["messages"] = new JsonArray(new JsonObject { ["role"] = "user", ["content"] = "Reply with OK." })
        };

        await SendAsync(() => CreateRequest(connection, body, false), connection, cancellationToken);
    }

    #endregion
}
