using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Text;

namespace Nop.Plugin.CW.ThemeCore.Services.BrandDesigner.Ai;

/// <summary>
/// What both clients share: a timeout per attempt, one retry, and a cap on what a provider may send back.
/// </summary>
public abstract class CwBrandAiClientBase : ICwBrandAiClient
{
    #region Constants

    /// <summary>The answer is a small JSON object; the address may be one an admin typed.</summary>
    private const int MAX_RESPONSE_BYTES = 1_000_000;

    private static readonly TimeSpan _maxRetryWait = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan _defaultRetryWait = TimeSpan.FromMilliseconds(500);

    #endregion

    #region Fields

    protected readonly HttpClient _httpClient;

    #endregion

    #region Ctor

    protected CwBrandAiClientBase(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    #endregion

    #region Utilities

    /// <summary>What a failed status means.</summary>
    /// <returns>The message for the admin, and whether one retry may help.</returns>
    protected abstract (string ResourceKey, bool Retry) MapStatus(int status, string body);

    /// <summary>What a failed connection means. Generic providers may simply not be running.</summary>
    protected virtual (string ResourceKey, bool Retry) MapConnectionFailure(HttpRequestException exception)
        => (BrandAiMessages.Unavailable, true);

    private static async Task<string> ReadBodyAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var buffer = new MemoryStream();
        var chunk = new byte[16 * 1024];
        int read;
        while ((read = await stream.ReadAsync(chunk, cancellationToken)) > 0)
        {
            if (buffer.Length + read > MAX_RESPONSE_BYTES)
                throw new BrandAiException(BrandAiMessages.InvalidOutput, "the response exceeded the size limit");

            buffer.Write(chunk, 0, read);
        }

        return Encoding.UTF8.GetString(buffer.GetBuffer(), 0, (int)buffer.Length);
    }

    private static TimeSpan RetryWait(RetryConditionHeaderValue retryAfter)
    {
        if (retryAfter?.Delta is not { } delta)
            return _defaultRetryWait;

        return delta > _maxRetryWait ? _maxRetryWait : delta;
    }

    /// <summary>The key must never reach a log, even when a provider echoes it back in an error.</summary>
    private static string Masked(string text, string apiKey)
        => string.IsNullOrEmpty(apiKey) ? text : text.Replace(apiKey, "***");

    /// <summary>
    /// Send a request and return the body of a successful response. A request can be sent only once, so
    /// <paramref name="createRequest"/> builds it for each attempt.
    /// </summary>
    protected async Task<string> SendAsync(Func<HttpRequestMessage> createRequest, BrandAiConnection connection, CancellationToken cancellationToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            string resourceKey, detail;
            bool retry;
            var wait = _defaultRetryWait;

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(connection.TimeoutSeconds));

            try
            {
                using var request = createRequest();
                using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
                var body = await ReadBodyAsync(response, timeout.Token);
                if (response.IsSuccessStatusCode)
                    return body;

                var status = (int)response.StatusCode;
                (resourceKey, retry) = MapStatus(status, body);
                detail = $"status {status}: {Masked(body[..Math.Min(body.Length, 300)], connection.ApiKey)}";
                wait = RetryWait(response.Headers.RetryAfter);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                (resourceKey, retry) = (BrandAiMessages.Unavailable, true);
                detail = $"no answer within {connection.TimeoutSeconds} s";
            }
            catch (HttpRequestException e)
            {
                (resourceKey, retry) = MapConnectionFailure(e);
                detail = e.Message;
            }

            if (!retry || attempt > 1)
                throw new BrandAiException(resourceKey, detail);

            await Task.Delay(wait, cancellationToken);
        }
    }

    /// <summary>Whether nothing is listening at the address, which is how a stopped local server fails.</summary>
    protected static bool IsConnectionRefused(HttpRequestException exception)
        => exception.InnerException is SocketException { SocketErrorCode: SocketError.ConnectionRefused };

    #endregion

    #region Methods

    public abstract Task<BrandAiAnswer> GenerateAsync(BrandAiConnection connection, BrandAiRequest request, CancellationToken cancellationToken = default);

    public abstract Task TestAsync(BrandAiConnection connection, CancellationToken cancellationToken = default);

    #endregion
}
