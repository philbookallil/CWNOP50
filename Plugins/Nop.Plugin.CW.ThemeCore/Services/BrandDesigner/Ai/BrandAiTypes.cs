using Nop.Plugin.CW.ThemeCore.Domain.BrandDesigner;

namespace Nop.Plugin.CW.ThemeCore.Services.BrandDesigner.Ai;

/// <summary>
/// The connection in effect for a store scope, with the key already decrypted. Never logged and never sent to the browser.
/// </summary>
public record BrandAiConnection
{
    public BrandAiProvider Provider { get; init; }

    public string BaseUrl { get; init; }

    public string Model { get; init; }

    public string ApiKey { get; init; }

    public int TimeoutSeconds { get; init; }

    public string Effort { get; init; }

    public int DailyLimit { get; init; }

    /// <summary>The store that owns this connection; 0 for the global one.</summary>
    public int OwnerStoreId { get; init; }
}

/// <summary>What the admin asked for.</summary>
public record BrandAiRequest(string Prompt, BrandStyle Current, string Region);

/// <summary>The model's answer, parsed but not yet validated.</summary>
public record BrandAiAnswer
{
    public BrandStyle Style { get; init; }

    public string Summary { get; init; }

    public int? InputTokens { get; set; }

    public int? OutputTokens { get; set; }

    public int? CachedTokens { get; set; }
}

/// <summary>
/// A provider call that failed. <see cref="Exception.Message"/> is a locale resource key for the admin;
/// <see cref="Detail"/> is for the log and never holds the key.
/// </summary>
public class BrandAiException : Exception
{
    public BrandAiException(string resourceKey, string detail = null) : base(resourceKey)
    {
        Detail = detail;
    }

    public string Detail { get; }
}

/// <summary>Locale resource keys of the assistant's admin messages.</summary>
public static class BrandAiMessages
{
    private const string PREFIX = "Plugins.CW.ThemeCore.Ai.Error.";

    public const string KeyRejected = PREFIX + "KeyRejected";
    public const string AccountProblem = PREFIX + "AccountProblem";
    public const string InvalidRequest = PREFIX + "InvalidRequest";
    public const string ModelRejected = PREFIX + "ModelRejected";
    public const string RateLimited = PREFIX + "RateLimited";
    public const string FreeLimit = PREFIX + "FreeLimit";
    public const string Unavailable = PREFIX + "Unavailable";
    public const string Unreachable = PREFIX + "Unreachable";
    public const string Refused = PREFIX + "Refused";
    public const string CutOff = PREFIX + "CutOff";
    public const string InvalidOutput = PREFIX + "InvalidOutput";
    public const string DailyLimit = PREFIX + "DailyLimit";
    public const string Burst = PREFIX + "Burst";
    public const string EmptyPrompt = PREFIX + "EmptyPrompt";
    public const string InvalidUrl = PREFIX + "InvalidUrl";
    public const string UnknownProvider = PREFIX + "UnknownProvider";
    public const string ModelRequired = PREFIX + "ModelRequired";
    public const string NoConnection = PREFIX + "NoConnection";
}
