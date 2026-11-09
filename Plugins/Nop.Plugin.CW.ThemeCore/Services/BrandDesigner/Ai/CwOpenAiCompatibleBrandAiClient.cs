using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Nodes;

namespace Nop.Plugin.CW.ThemeCore.Services.BrandDesigner.Ai;

/// <summary>
/// The OpenAI-style chat request, which most providers accept. Enforcement of the schema varies by provider, so it is
/// also sent as text, and the answer is judged like any other.
/// </summary>
public class CwOpenAiCompatibleBrandAiClient : CwBrandAiClientBase
{
    #region Ctor

    public CwOpenAiCompatibleBrandAiClient(HttpClient httpClient) : base(httpClient)
    {
    }

    #endregion

    #region Utilities

    protected override (string ResourceKey, bool Retry) MapStatus(int status, string body) => status switch
    {
        401 or 403 => (BrandAiMessages.KeyRejected, false),
        400 or 404 => (BrandAiMessages.ModelRejected, false),
        429 => (BrandAiMessages.FreeLimit, true),
        >= 500 => (BrandAiMessages.Unavailable, true),
        _ => (BrandAiMessages.Unavailable, false)
    };

    protected override (string ResourceKey, bool Retry) MapConnectionFailure(HttpRequestException exception)
        => IsConnectionRefused(exception) ? (BrandAiMessages.Unreachable, false) : base.MapConnectionFailure(exception);

    private static HttpRequestMessage CreateRequest(BrandAiConnection connection, JsonObject body)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"{connection.BaseUrl.TrimEnd('/')}/chat/completions")
        {
            Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json")
        };
        if (!string.IsNullOrEmpty(connection.ApiKey))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", connection.ApiKey);

        return request;
    }

    #endregion

    #region Methods

    public override async Task<BrandAiAnswer> GenerateAsync(BrandAiConnection connection, BrandAiRequest request, CancellationToken cancellationToken = default)
    {
        var body = new JsonObject
        {
            ["model"] = connection.Model,
            ["messages"] = new JsonArray(
                new JsonObject { ["role"] = "system", ["content"] = $"{BrandAiPrompt.System}\nSchema:\n{BrandAiPrompt.Schema}" },
                new JsonObject { ["role"] = "user", ["content"] = BrandAiPrompt.UserMessage(request) }),
            ["response_format"] = new JsonObject
            {
                ["type"] = "json_schema",
                ["json_schema"] = new JsonObject
                {
                    ["name"] = "brand_result",
                    ["strict"] = true,
                    ["schema"] = JsonNode.Parse(BrandAiPrompt.Schema)
                }
            }
        };

        var text = await SendAsync(() => CreateRequest(connection, body), connection, cancellationToken);
        var response = JsonNode.Parse(text);
        var choice = response?["choices"]?[0];

        switch ((string)choice?["finish_reason"])
        {
            case "length":
                throw new BrandAiException(BrandAiMessages.CutOff);
            case "content_filter":
                throw new BrandAiException(BrandAiMessages.Refused);
        }

        var answer = BrandAiPrompt.ParseAnswer((string)choice?["message"]?["content"]);

        var usage = response?["usage"];
        answer.InputTokens = (int?)usage?["prompt_tokens"];
        answer.OutputTokens = (int?)usage?["completion_tokens"];

        return answer;
    }

    public override async Task TestAsync(BrandAiConnection connection, CancellationToken cancellationToken = default)
    {
        var body = new JsonObject
        {
            ["model"] = connection.Model,
            ["messages"] = new JsonArray(new JsonObject { ["role"] = "user", ["content"] = "Reply with OK." })
        };

        await SendAsync(() => CreateRequest(connection, body), connection, cancellationToken);
    }

    #endregion
}
