using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Nop.Plugin.CW.ThemeCore.Domain.BrandDesigner;

namespace Nop.Plugin.CW.ThemeCore.Services.BrandDesigner.Ai;

/// <summary>
/// What every provider is sent and how its answer is read, so a prompt change lands in one place. The system prompt
/// and the schema are constants: their text must stay byte-identical for prompt caching to hit. No store, customer,
/// order or product data is ever sent; the admin's own text is.
/// </summary>
internal static partial class BrandAiPrompt
{
    #region Constants

    public const string System = """
        You are a brand designer for a B2B nopCommerce storefront. Return only the JSON in the schema.

        The style has six colours and a button radius:
        - primaryColor paints buttons, links, navigation hover and the active checkout step. Text on it is white, so keep contrast at least 4.5:1.
        - primaryTintColor is a light background for info areas and banners. It must stay light enough for dark text.
        - successColor, dangerColor and warningColor are status colours and must stay recognisable.
        - darkColor is the text ink and the footer.
        - buttonRadius is in pixels: 2 is square, 8 rounded, 999 a pill.

        Colours are six-digit hex without '#'. Always return the whole style: fields you do not change keep their current value.

        A tweak request changes only what it names; a vibe request may change everything.
        Leave css empty unless told CSS is allowed and the style fields cannot express the request.
        Never output @import, url(), expression, or angle brackets.
        The summary is one plain sentence saying what changed and why.
        """;

    public const string Schema = """
        {"type":"object","additionalProperties":false,"required":["style","css","summary"],"properties":{"style":{"type":"object","additionalProperties":false,"required":["primaryColor","primaryTintColor","successColor","dangerColor","warningColor","darkColor","buttonRadius"],"properties":{"primaryColor":{"type":"string","description":"6-digit hex, no #"},"primaryTintColor":{"type":"string"},"successColor":{"type":"string"},"dangerColor":{"type":"string"},"warningColor":{"type":"string"},"darkColor":{"type":"string"},"buttonRadius":{"type":"integer","description":"px; 2 square, 8 rounded, 999 pill"}}},"css":{"type":"string","description":"Optional extra CSS; empty string when tokens suffice"},"summary":{"type":"string","description":"One sentence: what changed and why"}}}
        """;

    #endregion

    #region Utilities

    [GeneratedRegex(@"^```[a-zA-Z]*\s*|\s*```$")]
    private static partial Regex CodeFence();


    private static string Text(JsonObject source, string name)
        => source[name] is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;

    /// <summary>A radius may arrive as 8.0, so any number is accepted; anything else counts as zero and is judged by the validator.</summary>
    private static int Number(JsonObject source, string name)
        => source[name] is JsonValue value && value.TryGetValue<double>(out var number) ? (int)Math.Clamp(number, int.MinValue, int.MaxValue) : 0;

    #endregion

    #region Methods

    /// <summary>The per-request message: the request, the current style and, for a selected element, what it may change.</summary>
    public static string UserMessage(BrandAiRequest request)
    {
        var current = request.Current;
        var style = new JsonObject
        {
            ["primaryColor"] = current?.PrimaryColor,
            ["primaryTintColor"] = current?.PrimaryTintColor,
            ["successColor"] = current?.SuccessColor,
            ["dangerColor"] = current?.DangerColor,
            ["warningColor"] = current?.WarningColor,
            ["darkColor"] = current?.DarkColor,
            ["buttonRadius"] = current?.ButtonRadius
        };

        var message = new StringBuilder()
            .Append("Request: ").AppendLine(request.Prompt)
            .Append("Current style: ").AppendLine(style.ToJsonString());

        var fields = BrandRegions.GetFields(request.Region);
        if (fields is not null)
        {
            message.Append("Selected element: ").Append(request.Region.Trim())
                .Append(". Change only: ").AppendLine(string.Join(", ", fields));
        }

        return message.Append("CSS allowed: no").ToString();
    }

    /// <summary>
    /// Read a model's text as the answer. Some providers do not enforce the schema, so a Markdown fence is tolerated;
    /// anything else that is not the answer is invalid output.
    /// </summary>
    public static BrandAiAnswer ParseAnswer(string text)
    {
        JsonNode root;
        try
        {
            root = JsonNode.Parse(CodeFence().Replace((text ?? string.Empty).Trim(), string.Empty));
        }
        catch (JsonException e)
        {
            throw new BrandAiException(BrandAiMessages.InvalidOutput, e.Message);
        }

        if (root is not JsonObject answer || answer["style"] is not JsonObject style)
            throw new BrandAiException(BrandAiMessages.InvalidOutput, "the answer has no style object");

        return new BrandAiAnswer
        {
            Style = new BrandStyle
            {
                PrimaryColor = Text(style, "primaryColor"),
                PrimaryTintColor = Text(style, "primaryTintColor"),
                SuccessColor = Text(style, "successColor"),
                DangerColor = Text(style, "dangerColor"),
                WarningColor = Text(style, "warningColor"),
                DarkColor = Text(style, "darkColor"),
                ButtonRadius = Number(style, "buttonRadius")
            },
            Summary = Text(answer, "summary")
        };
    }

    #endregion
}
