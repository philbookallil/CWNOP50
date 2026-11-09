using Nop.Core.Configuration;

namespace Nop.Plugin.CW.ThemeCore.Settings;

/// <summary>
/// The Brand Designer's AI assistant. A connection (provider, address, model, key, timeout) is global, and a store
/// may replace it as a whole: one provider's key must never go to another provider's address. Effort and the daily
/// limit are install-wide and only ever saved at store 0.
/// </summary>
public class CwThemeAiSettings : ISettings
{
    #region Connection (per store, or global)

    /// <summary>Provider preset name. Empty = no connection, so the Brand Designer runs in offline mode.</summary>
    public string Provider { get; set; }

    /// <summary>The provider's API address. It receives the key, so only an admin who manages plugins can set it.</summary>
    public string BaseUrl { get; set; }

    /// <summary>The model name, as the provider writes it.</summary>
    public string Model { get; set; }

    /// <summary>API key, encrypted with IEncryptionService and never sent to the browser. Empty for a provider that needs none.</summary>
    public string ApiKeyEncrypted { get; set; }

    /// <summary>How long to wait for each attempt, in seconds.</summary>
    public int TimeoutSeconds { get; set; } = 30;

    #endregion

    #region Install-wide

    /// <summary>Claude only: low, medium or high.</summary>
    public string Effort { get; set; } = "medium";

    /// <summary>Cap on AI generations per UTC day, counted per connection. 0 = unlimited.</summary>
    public int DailyLimit { get; set; } = 300;

    #endregion
}
