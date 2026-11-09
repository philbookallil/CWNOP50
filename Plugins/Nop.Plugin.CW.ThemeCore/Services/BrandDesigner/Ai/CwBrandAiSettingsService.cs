using Nop.Plugin.CW.ThemeCore.Domain.BrandDesigner;
using Nop.Plugin.CW.ThemeCore.Settings;
using Nop.Services.Configuration;
using Nop.Services.Security;

namespace Nop.Plugin.CW.ThemeCore.Services.BrandDesigner.Ai;

/// <summary>What the AI card posts.</summary>
public record BrandAiSettingsInput
{
    public string Provider { get; init; }

    public string BaseUrl { get; init; }

    public string Model { get; init; }

    /// <summary>A new key; empty keeps the saved one.</summary>
    public string ApiKey { get; init; }

    public bool RemoveKey { get; init; }

    public int TimeoutSeconds { get; init; }

    public string Effort { get; init; }

    public int DailyLimit { get; init; }

    public bool OverrideForStore { get; init; }
}

/// <summary>What the AI card shows. Holds no key, only its last four characters.</summary>
public record BrandAiSettingsView
{
    public string Provider { get; init; }

    public string BaseUrl { get; init; }

    public string Model { get; init; }

    public string KeyLastFour { get; init; }

    public int TimeoutSeconds { get; init; }

    public string Effort { get; init; }

    public int DailyLimit { get; init; }

    public bool OverrideForStore { get; init; }
}

/// <summary>
/// The AI connection: which one is in effect for a store scope, and saving it.
/// </summary>
public interface ICwBrandAiSettingsService
{
    /// <summary>The connection in effect, or null when the scope has none, which is offline mode.</summary>
    Task<BrandAiConnection> GetConnectionAsync(int storeScope);

    /// <summary>The values the AI card shows for a scope.</summary>
    Task<BrandAiSettingsView> GetViewAsync(int storeScope);

    /// <summary>Save the card.</summary>
    /// <returns>A locale resource key when the input is refused; otherwise null</returns>
    Task<string> SaveAsync(BrandAiSettingsInput input, int storeScope);
}

/// <inheritdoc />
public class CwBrandAiSettingsService : ICwBrandAiSettingsService
{
    #region Constants

    private const string API_KEY_SETTING = "cwthemeaisettings.apikeyencrypted";
    private const int MIN_TIMEOUT_SECONDS = 5;
    private const int MAX_TIMEOUT_SECONDS = 300;

    private static readonly string[] _efforts = ["low", "medium", "high"];

    #endregion

    #region Fields

    private readonly IEncryptionService _encryptionService;
    private readonly ISettingService _settingService;

    #endregion

    #region Ctor

    public CwBrandAiSettingsService(IEncryptionService encryptionService, ISettingService settingService)
    {
        _encryptionService = encryptionService;
        _settingService = settingService;
    }

    #endregion

    #region Utilities

    /// <summary>
    /// The address receives the key, so it must be https. Plain http is for a server on this machine only.
    /// </summary>
    internal static bool IsAllowedAddress(string baseUrl)
    {
        if (!Uri.TryCreate(baseUrl?.Trim(), UriKind.Absolute, out var uri))
            return false;

        return uri.Scheme == Uri.UriSchemeHttps
            || uri.Scheme == Uri.UriSchemeHttp && (uri.IsLoopback || uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase));
    }

    private string Decrypt(string encrypted)
        => string.IsNullOrEmpty(encrypted) ? string.Empty : _encryptionService.DecryptText(encrypted);

    /// <summary>
    /// The key to save. A key belongs to the address it was typed for: when the provider or address changes without a
    /// new key, the old one is dropped, so that switching provider never sends one provider's key to another.
    /// </summary>
    private async Task<string> ResolveKeyAsync(BrandAiSettingsInput input, CwThemeAiSettings saved, int storeScope, bool sameTarget)
    {
        if (!string.IsNullOrWhiteSpace(input.ApiKey))
            return _encryptionService.EncryptText(input.ApiKey.Trim());

        if (input.RemoveKey || !sameTarget)
            return string.Empty;

        //the store's own row only: a loaded setting falls back to the global key, which must not be copied into the store
        return storeScope == 0
            ? saved.ApiKeyEncrypted
            : await _settingService.GetSettingByKeyAsync(API_KEY_SETTING, string.Empty, storeScope);
    }

    #endregion

    #region Methods

    public async Task<BrandAiConnection> GetConnectionAsync(int storeScope)
    {
        var settings = await _settingService.LoadSettingAsync<CwThemeAiSettings>(storeScope);
        var provider = BrandAiProviders.Find(settings.Provider);
        if (provider is null || string.IsNullOrWhiteSpace(settings.BaseUrl) || string.IsNullOrWhiteSpace(settings.Model))
            return null;

        var apiKey = Decrypt(settings.ApiKeyEncrypted);
        if (provider.RequiresKey && apiKey.Length == 0)
            return null;

        var ownConnection = storeScope > 0 && await _settingService.SettingExistsAsync(settings, x => x.Provider, storeScope);

        return new BrandAiConnection
        {
            Provider = provider,
            BaseUrl = settings.BaseUrl,
            Model = settings.Model,
            ApiKey = apiKey,
            TimeoutSeconds = settings.TimeoutSeconds,
            Effort = settings.Effort,
            DailyLimit = settings.DailyLimit,
            OwnerStoreId = ownConnection ? storeScope : 0
        };
    }

    public async Task<BrandAiSettingsView> GetViewAsync(int storeScope)
    {
        var settings = await _settingService.LoadSettingAsync<CwThemeAiSettings>(storeScope);
        var key = Decrypt(settings.ApiKeyEncrypted);

        return new BrandAiSettingsView
        {
            Provider = settings.Provider,
            BaseUrl = settings.BaseUrl,
            Model = settings.Model,
            KeyLastFour = key.Length > 4 ? key[^4..] : string.Empty,
            TimeoutSeconds = settings.TimeoutSeconds,
            Effort = settings.Effort,
            DailyLimit = settings.DailyLimit,
            OverrideForStore = storeScope > 0 && await _settingService.SettingExistsAsync(settings, x => x.Provider, storeScope)
        };
    }

    public async Task<string> SaveAsync(BrandAiSettingsInput input, int storeScope)
    {
        var overrideForStore = storeScope > 0 && input.OverrideForStore;
        var settings = await _settingService.LoadSettingAsync<CwThemeAiSettings>(storeScope);

        //a store left on the global connection has nothing of its own to check
        if (storeScope == 0 || overrideForStore)
        {
            var provider = BrandAiProviders.Find(input.Provider);
            if (!string.IsNullOrEmpty(input.Provider) && provider is null)
                return BrandAiMessages.UnknownProvider;

            if (provider is not null)
            {
                if (!IsAllowedAddress(input.BaseUrl))
                    return BrandAiMessages.InvalidUrl;

                if (string.IsNullOrWhiteSpace(input.Model))
                    return BrandAiMessages.ModelRequired;
            }

            var sameTarget = string.Equals(settings.Provider, input.Provider, StringComparison.OrdinalIgnoreCase)
                && string.Equals(settings.BaseUrl, input.BaseUrl?.Trim(), StringComparison.OrdinalIgnoreCase);

            settings.ApiKeyEncrypted = await ResolveKeyAsync(input, settings, storeScope, sameTarget);
            settings.Provider = input.Provider ?? string.Empty;
            settings.BaseUrl = input.BaseUrl?.Trim() ?? string.Empty;
            settings.Model = input.Model?.Trim() ?? string.Empty;
            settings.TimeoutSeconds = Math.Clamp(input.TimeoutSeconds, MIN_TIMEOUT_SECONDS, MAX_TIMEOUT_SECONDS);
        }

        await _settingService.SaveSettingOverridablePerStoreAsync(settings, x => x.Provider, overrideForStore, storeScope, false);
        await _settingService.SaveSettingOverridablePerStoreAsync(settings, x => x.BaseUrl, overrideForStore, storeScope, false);
        await _settingService.SaveSettingOverridablePerStoreAsync(settings, x => x.Model, overrideForStore, storeScope, false);
        await _settingService.SaveSettingOverridablePerStoreAsync(settings, x => x.ApiKeyEncrypted, overrideForStore, storeScope, false);
        await _settingService.SaveSettingOverridablePerStoreAsync(settings, x => x.TimeoutSeconds, overrideForStore, storeScope, false);

        if (storeScope == 0)
        {
            settings.Effort = _efforts.Contains(input.Effort) ? input.Effort : settings.Effort;
            settings.DailyLimit = Math.Max(input.DailyLimit, 0);
            await _settingService.SaveSettingAsync(settings, x => x.Effort, 0, false);
            await _settingService.SaveSettingAsync(settings, x => x.DailyLimit, 0, false);
        }

        await _settingService.ClearCacheAsync();

        return null;
    }

    #endregion
}
