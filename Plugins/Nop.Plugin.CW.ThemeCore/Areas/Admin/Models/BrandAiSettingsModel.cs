using Nop.Plugin.CW.ThemeCore.Domain.BrandDesigner;

namespace Nop.Plugin.CW.ThemeCore.Areas.Admin.Models;

/// <summary>
/// What the AI assistant card posts. Saved through its own action: the key is never part of the page's form.
/// </summary>
public class BrandAiSettingsModel
{
    public string Provider { get; set; }

    public string BaseUrl { get; set; }

    public string Model { get; set; }

    /// <summary>A new key; empty keeps the saved one.</summary>
    public string ApiKey { get; set; }

    public bool RemoveKey { get; set; }

    public int TimeoutSeconds { get; set; }

    public string Effort { get; set; }

    public int DailyLimit { get; set; }

    public bool OverrideForStore { get; set; }
}

/// <summary>
/// What the AI assistant card shows.
/// </summary>
public class BrandAiCardModel
{
    public string Provider { get; set; }

    public string BaseUrl { get; set; }

    public string Model { get; set; }

    /// <summary>The saved key's last four characters; empty when none is saved.</summary>
    public string KeyLastFour { get; set; }

    public int TimeoutSeconds { get; set; }

    public string Effort { get; set; }

    public int DailyLimit { get; set; }

    public bool OverrideForStore { get; set; }

    public bool IsStoreScope { get; set; }

    /// <summary>The saved provider is a free cloud tier, so the prompt box shows the data notice.</summary>
    public bool IsFreeCloud { get; set; }

    public IReadOnlyList<BrandAiProvider> Providers { get; set; } = [];
}
