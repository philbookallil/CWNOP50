using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Serialization;
using Nop.Core;
using Nop.Core.Domain.Logging;
using Nop.Services.Logging;

namespace Nop.Plugin.Payments.NABGateway.Services;

/// <summary>
/// Thin transport that serialises a request, posts it to the NAB Transact XML API and deserialises the response.
/// Wrapping <see cref="HttpClient"/> here keeps the processor focused on the business flow and makes the
/// gateway interaction unit-testable.
/// </summary>
public class NABXMLGatewayClient
{
    #region Fields

    protected readonly IHttpClientFactory _httpClientFactory;
    protected readonly ILogger _logger;
    protected readonly NABXMLPaymentSettings _settings;

    #endregion

    #region Ctor

    public NABXMLGatewayClient(IHttpClientFactory httpClientFactory,
        ILogger logger,
        NABXMLPaymentSettings settings)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _settings = settings;
    }

    #endregion

    #region Methods

    /// <summary>
    /// Sends a payment request to the NAB Transact XML API and returns the parsed response and the raw XML body
    /// (the latter is preserved so callers can log the exact gateway payload).
    /// </summary>
    /// <param name="request">The request payload</param>
    /// <param name="endpointUrl">Fully-qualified endpoint URL</param>
    /// <returns>The parsed response plus the raw response body</returns>
    public virtual async Task<(Models.Response.NABTransactMessage Response, string RawResponse)> SendPaymentAsync(
        Models.Request.NABTransactMessage request, string endpointUrl)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(endpointUrl);

        var requestXml = Serialize(request);

        if (_settings.DebugMode)
        {
            await _logger.InsertLogAsync(LogLevel.Debug,
                $"NAB XML Gateway request to {endpointUrl}",
                MaskSensitiveData(requestXml));
        }

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, endpointUrl)
        {
            Content = new StringContent(requestXml, Encoding.UTF8, "text/xml")
        };
        httpRequest.Content.Headers.ContentType = new MediaTypeHeaderValue("text/xml") { CharSet = "utf-8" };

        var client = _httpClientFactory.CreateClient(NABXMLPaymentDefaults.HttpClientName);
        using var httpResponse = await client.SendAsync(httpRequest);
        var rawResponse = await httpResponse.Content.ReadAsStringAsync();

        if (_settings.DebugMode)
        {
            await _logger.InsertLogAsync(LogLevel.Debug,
                $"NAB XML Gateway response from {endpointUrl} (HTTP {(int)httpResponse.StatusCode})",
                rawResponse);
        }

        // A non-success HTTP status is a transport/infrastructure failure (e.g. 502 Bad Gateway from a proxy
        // in front of the gateway), NOT a NAB business response. Surface the status and the response body so
        // the cause is visible in the error message and in the persisted transaction record. The body is
        // typically an HTML/proxy error page rather than NAB XML, so we deliberately don't try to deserialize it.
        if (!httpResponse.IsSuccessStatusCode)
        {
            var bodySnippet = string.IsNullOrWhiteSpace(rawResponse)
                ? "(empty response body)"
                : rawResponse.Length > 1000 ? rawResponse[..1000] + "…" : rawResponse;

            throw new NopException(
                $"NAB gateway returned HTTP {(int)httpResponse.StatusCode} ({httpResponse.ReasonPhrase}) for '{endpointUrl}'. " +
                "This is a transport-level error (not a card decline) — verify the configured Gateway URL " +
                "and outbound connectivity. Response body: " + bodySnippet);
        }

        var parsed = Deserialize<Models.Response.NABTransactMessage>(rawResponse);
        return (parsed, rawResponse);
    }

    #endregion

    #region Utilities

    /// <summary>
    /// Masks cardholder data (card number, CVV) in an XML payload before it is written to the log,
    /// so debug logging never persists full PAN/CVV.
    /// </summary>
    internal static string MaskSensitiveData(string xml)
    {
        if (string.IsNullOrEmpty(xml))
            return xml;

        // Keep only the last 4 digits of the card number.
        xml = Regex.Replace(xml, @"(<cardNumber>)(.*?)(</cardNumber>)", match =>
        {
            var value = match.Groups[2].Value;
            var last4 = value.Length >= 4 ? value[^4..] : value;
            return $"{match.Groups[1].Value}************{last4}{match.Groups[3].Value}";
        }, RegexOptions.IgnoreCase | RegexOptions.Singleline);

        // Fully mask the CVV.
        xml = Regex.Replace(xml, @"(<cvv>)(.*?)(</cvv>)", "$1***$3",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);

        return xml;
    }

    /// <summary>
    /// Serialises the given object to XML using the NAB root element name and a UTF-8 declaration.
    /// </summary>
    internal static string Serialize<T>(T source)
    {
        var serializer = new XmlSerializer(typeof(T), new XmlRootAttribute("NABTransactMessage"));

        // Suppress xsi/xsd namespace declarations to keep the payload compatible with the legacy plugin.
        var namespaces = new XmlSerializerNamespaces();
        namespaces.Add(string.Empty, string.Empty);

        using var stream = new MemoryStream();
        var settings = new XmlWriterSettings
        {
            Encoding = new UTF8Encoding(false),
            Indent = false,
            OmitXmlDeclaration = false
        };
        using (var writer = XmlWriter.Create(stream, settings))
        {
            serializer.Serialize(writer, source, namespaces);
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    /// <summary>
    /// Deserialises an XML payload into <typeparamref name="T"/>.
    /// </summary>
    internal static T Deserialize<T>(string xml) where T : class
    {
        if (string.IsNullOrWhiteSpace(xml))
            return null;

        var serializer = new XmlSerializer(typeof(T));
        using var reader = new StringReader(xml);
        return (T)serializer.Deserialize(reader);
    }

    #endregion
}
