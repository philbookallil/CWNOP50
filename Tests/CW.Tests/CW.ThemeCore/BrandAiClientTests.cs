using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json.Nodes;
using AwesomeAssertions;
using Nop.Plugin.CW.ThemeCore.Domain.BrandDesigner;
using Nop.Plugin.CW.ThemeCore.Services.BrandDesigner.Ai;
using NUnit.Framework;

namespace CW.Tests.CW.ThemeCore;

/// <summary>
/// What goes to each provider and how its answer or failure is read. Every call here stops at a fake handler: the
/// requests have never reached a real provider in these tests.
/// </summary>
[TestFixture]
public class BrandAiClientTests
{
    #region Utilities

    private const string API_KEY = "test-key-value";

    private const string STYLE = """{"style":{"primaryColor":"112233","primaryTintColor":"eeeeee","successColor":"00aa00","dangerColor":"aa0000","warningColor":"aaaa00","darkColor":"111111","buttonRadius":6},"css":"","summary":"Darker."}""";

    private sealed class FakeHandler(Func<HttpRequestMessage, int, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public List<(HttpRequestMessage Request, string Body)> Calls { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            Calls.Add((request, body));
            return respond(request, Calls.Count);
        }
    }

    private static HttpResponseMessage Response(HttpStatusCode status, string body)
        => new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private static string ClaudeOk(string text, string stopReason = "end_turn") => new JsonObject
    {
        ["model"] = "claude-sonnet-5-5",
        ["stop_reason"] = stopReason,
        ["content"] = new JsonArray(new JsonObject { ["type"] = "thinking", ["thinking"] = "" }, new JsonObject { ["type"] = "text", ["text"] = text }),
        ["usage"] = new JsonObject { ["input_tokens"] = 100, ["output_tokens"] = 50, ["cache_read_input_tokens"] = 80 }
    }.ToJsonString();

    private static string GenericOk(string text, string finishReason = "stop") => new JsonObject
    {
        ["choices"] = new JsonArray(new JsonObject
        {
            ["finish_reason"] = finishReason,
            ["message"] = new JsonObject { ["role"] = "assistant", ["content"] = text }
        }),
        ["usage"] = new JsonObject { ["prompt_tokens"] = 100, ["completion_tokens"] = 50 }
    }.ToJsonString();

    private static BrandAiConnection Claude(string model = "claude-sonnet-5-5") => new()
    {
        Provider = BrandAiProviders.Find("Claude"),
        BaseUrl = "https://api.anthropic.com",
        Model = model,
        ApiKey = API_KEY,
        TimeoutSeconds = 5,
        Effort = "medium"
    };

    private static BrandAiConnection Generic(string apiKey = API_KEY) => new()
    {
        Provider = BrandAiProviders.Find("Groq"),
        BaseUrl = "https://example.test/v1/",
        Model = "some-model",
        ApiKey = apiKey,
        TimeoutSeconds = 5
    };

    private static BrandAiRequest Request(string region = null) =>
        new("a bit darker", new BrandStyle { PrimaryColor = "3366cc", ButtonRadius = 8 }, region);

    private static (CwClaudeBrandAiClient Client, FakeHandler Handler) ClaudeClient(Func<HttpRequestMessage, int, HttpResponseMessage> respond)
    {
        var handler = new FakeHandler(respond);
        return (new CwClaudeBrandAiClient(new HttpClient(handler)), handler);
    }

    private static (CwOpenAiCompatibleBrandAiClient Client, FakeHandler Handler) GenericClient(Func<HttpRequestMessage, int, HttpResponseMessage> respond)
    {
        var handler = new FakeHandler(respond);
        return (new CwOpenAiCompatibleBrandAiClient(new HttpClient(handler)), handler);
    }

    private static async Task<BrandAiException> FailureOf(Func<Task> call)
    {
        var failure = await call.Should().ThrowAsync<BrandAiException>();
        return failure.Which;
    }

    #endregion

    #region Claude

    [Test]
    public async Task Claude_gets_the_key_the_api_version_and_the_schema()
    {
        var (client, handler) = ClaudeClient((_, _) => Response(HttpStatusCode.OK, ClaudeOk(STYLE)));

        await client.GenerateAsync(Claude(), Request());

        var (request, body) = handler.Calls.Single();
        request.RequestUri!.ToString().Should().Be("https://api.anthropic.com/v1/messages");
        request.Headers.GetValues("x-api-key").Should().Equal(API_KEY);
        request.Headers.GetValues("anthropic-version").Should().Equal("2026-10-21");

        var json = JsonNode.Parse(body)!;
        ((string)json["output_config"]!["effort"]!).Should().Be("medium");
        ((string)json["output_config"]!["format"]!["type"]!).Should().Be("json_schema");
        json["temperature"].Should().BeNull();
        json["thinking"].Should().BeNull();
    }

    [Test]
    public async Task Claude_reads_the_text_block_after_a_thinking_block_and_the_usage()
    {
        var (client, _) = ClaudeClient((_, _) => Response(HttpStatusCode.OK, ClaudeOk(STYLE)));

        var answer = await client.GenerateAsync(Claude(), Request());

        answer.Style.PrimaryColor.Should().Be("112233");
        answer.Summary.Should().Be("Darker.");
        answer.InputTokens.Should().Be(100);
        answer.CachedTokens.Should().Be(80);
    }

    [TestCase("claude-sonnet-5-5", true)]
    [TestCase("claude-haiku-5-5", false)]
    public async Task The_refusal_fallback_goes_only_to_models_that_accept_it(string model, bool expected)
    {
        var (client, handler) = ClaudeClient((_, _) => Response(HttpStatusCode.OK, ClaudeOk(STYLE)));

        await client.GenerateAsync(Claude(model), Request());

        var (request, body) = handler.Calls.Single();
        request.Headers.Contains("anthropic-beta").Should().Be(expected);
        (JsonNode.Parse(body)!["fallbacks"] is not null).Should().Be(expected);
    }

    [TestCase("refusal", BrandAiMessages.Refused)]
    [TestCase("max_tokens", BrandAiMessages.CutOff)]
    public async Task A_stop_reason_is_read_before_the_content(string stopReason, string message)
    {
        var (client, _) = ClaudeClient((_, _) => Response(HttpStatusCode.OK, ClaudeOk("not json", stopReason)));

        (await FailureOf(() => client.GenerateAsync(Claude(), Request()))).Message.Should().Be(message);
    }

    [TestCase(HttpStatusCode.Unauthorized, "authentication_error", BrandAiMessages.KeyRejected, 1)]
    [TestCase(HttpStatusCode.PaymentRequired, "billing_error", BrandAiMessages.AccountProblem, 1)]
    [TestCase(HttpStatusCode.Forbidden, "permission_error", BrandAiMessages.AccountProblem, 1)]
    [TestCase(HttpStatusCode.BadRequest, "invalid_request_error", BrandAiMessages.InvalidRequest, 1)]
    [TestCase(HttpStatusCode.TooManyRequests, "rate_limit_error", BrandAiMessages.RateLimited, 2)]
    [TestCase((HttpStatusCode)529, "overloaded_error", BrandAiMessages.Unavailable, 2)]
    public async Task Claude_failures_map_to_a_message_and_retry_only_what_may_recover(HttpStatusCode status, string type, string message, int calls)
    {
        var body = new JsonObject { ["error"] = new JsonObject { ["type"] = type } }.ToJsonString();
        var (client, handler) = ClaudeClient((_, _) => Response(status, body));

        (await FailureOf(() => client.GenerateAsync(Claude(), Request()))).Message.Should().Be(message);
        handler.Calls.Should().HaveCount(calls);
    }

    [Test]
    public async Task One_retry_can_succeed()
    {
        var (client, _) = ClaudeClient((_, call) => call == 1
            ? Response((HttpStatusCode)529, """{"error":{"type":"overloaded_error"}}""")
            : Response(HttpStatusCode.OK, ClaudeOk(STYLE)));

        (await client.GenerateAsync(Claude(), Request())).Style.PrimaryColor.Should().Be("112233");
    }

    #endregion

    #region Generic

    [Test]
    public async Task A_generic_provider_gets_the_chat_request_with_the_schema_also_as_text()
    {
        var (client, handler) = GenericClient((_, _) => Response(HttpStatusCode.OK, GenericOk(STYLE)));

        await client.GenerateAsync(Generic(), Request());

        var (request, body) = handler.Calls.Single();
        request.RequestUri!.ToString().Should().Be("https://example.test/v1/chat/completions");
        request.Headers.Authorization!.ToString().Should().Be($"Bearer {API_KEY}");

        var json = JsonNode.Parse(body)!;
        ((string)json["response_format"]!["type"]!).Should().Be("json_schema");
        ((string)json["messages"]![0]!["content"]!).Should().Contain("primaryTintColor");
    }

    [Test]
    public async Task A_provider_without_a_key_gets_no_authorization_header()
    {
        var (client, handler) = GenericClient((_, _) => Response(HttpStatusCode.OK, GenericOk(STYLE)));

        await client.GenerateAsync(Generic(apiKey: ""), Request());

        handler.Calls.Single().Request.Headers.Authorization.Should().BeNull();
    }

    [TestCase("```json\n" + STYLE + "\n```")]
    [TestCase(STYLE)]
    public async Task Plain_and_fenced_json_both_parse(string text)
    {
        var (client, _) = GenericClient((_, _) => Response(HttpStatusCode.OK, GenericOk(text)));

        (await client.GenerateAsync(Generic(), Request())).Style.DarkColor.Should().Be("111111");
    }

    [TestCase("Sure! Here is your palette.")]
    [TestCase("""{"summary":"no style"}""")]
    [TestCase("")]
    public async Task Other_text_is_invalid_output(string text)
    {
        var (client, _) = GenericClient((_, _) => Response(HttpStatusCode.OK, GenericOk(text)));

        (await FailureOf(() => client.GenerateAsync(Generic(), Request()))).Message.Should().Be(BrandAiMessages.InvalidOutput);
    }

    [TestCase("length", BrandAiMessages.CutOff)]
    [TestCase("content_filter", BrandAiMessages.Refused)]
    public async Task A_finish_reason_is_read_before_the_content(string finishReason, string message)
    {
        var (client, _) = GenericClient((_, _) => Response(HttpStatusCode.OK, GenericOk("x", finishReason)));

        (await FailureOf(() => client.GenerateAsync(Generic(), Request()))).Message.Should().Be(message);
    }

    [TestCase(HttpStatusCode.Unauthorized, BrandAiMessages.KeyRejected, 1)]
    [TestCase(HttpStatusCode.Forbidden, BrandAiMessages.KeyRejected, 1)]
    [TestCase(HttpStatusCode.NotFound, BrandAiMessages.ModelRejected, 1)]
    [TestCase(HttpStatusCode.BadRequest, BrandAiMessages.ModelRejected, 1)]
    [TestCase(HttpStatusCode.TooManyRequests, BrandAiMessages.FreeLimit, 2)]
    [TestCase(HttpStatusCode.ServiceUnavailable, BrandAiMessages.Unavailable, 2)]
    [TestCase(HttpStatusCode.Found, BrandAiMessages.Unavailable, 1)]
    public async Task Generic_failures_map_on_the_status_alone(HttpStatusCode status, string message, int calls)
    {
        var (client, handler) = GenericClient((_, _) => Response(status, "<html>nope</html>"));

        (await FailureOf(() => client.GenerateAsync(Generic(), Request()))).Message.Should().Be(message);
        handler.Calls.Should().HaveCount(calls);
    }

    [Test]
    public async Task A_server_that_is_not_running_says_so_and_is_not_retried()
    {
        var (client, handler) = GenericClient((_, _) =>
            throw new HttpRequestException("refused", new SocketException((int)SocketError.ConnectionRefused)));

        (await FailureOf(() => client.GenerateAsync(Generic(), Request()))).Message.Should().Be(BrandAiMessages.Unreachable);
        handler.Calls.Should().HaveCount(1);
    }

    [Test]
    public async Task The_key_never_reaches_the_failure_detail()
    {
        var (client, _) = GenericClient((_, _) => Response(HttpStatusCode.Unauthorized, $"bad key {API_KEY}"));

        var failure = await FailureOf(() => client.GenerateAsync(Generic(), Request()));

        failure.Detail.Should().NotContain(API_KEY);
        failure.Message.Should().NotContain(API_KEY);
    }

    [Test]
    public async Task An_oversized_answer_is_refused()
    {
        var (client, _) = GenericClient((_, _) => Response(HttpStatusCode.OK, new string('x', 1_100_000)));

        (await FailureOf(() => client.GenerateAsync(Generic(), Request()))).Message.Should().Be(BrandAiMessages.InvalidOutput);
    }

    #endregion

    #region Prompt

    [Test]
    public void The_user_message_names_a_selected_elements_fields()
    {
        var message = BrandAiPrompt.UserMessage(Request("button"));

        message.Should().Contain("a bit darker").And.Contain("3366cc").And.Contain("Change only: PrimaryColor, ButtonRadius");
    }

    [Test]
    public void The_schema_is_valid_json()
    {
        JsonNode.Parse(BrandAiPrompt.Schema)!["required"]!.AsArray().Should().HaveCount(3);
    }

    #endregion
}
