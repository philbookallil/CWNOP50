namespace Nop.Plugin.CW.ThemeCore.Domain.Layout;

/// <summary>
/// The layout catalogue a theme declares in <c>~/Themes/{theme}/cw-theme.json</c>. Read by CW.ThemeCore
/// to build the admin layout selectors and to resolve the CSS classes / partials a store's selection maps to.
/// A theme with no manifest simply offers no layout choices (the plugin degrades gracefully).
/// </summary>
public class ThemeLayoutManifest
{
    /// <summary>The theme this manifest belongs to (informational; the file location is authoritative).</summary>
    public string ThemeSystemName { get; set; }

    /// <summary>The sections offering layout choices.</summary>
    public List<ThemeLayoutSection> Sections { get; set; } = new();

    /// <summary>
    /// Optional Brand-Designer preview declaration. A theme that ships this is treated as a "CW theme":
    /// its preview fragments render in the admin Brand Designer and the point-and-click picker is enabled.
    /// Absent → the picker is disabled and a generic preview is shown.
    /// </summary>
    public BrandPreviewManifest BrandPreview { get; set; }

    /// <summary>
    /// Optional selector→region rules for live click-to-edit (Phase 2): mapping stable storefront CSS selectors
    /// to a curated region (header/nav/link/button/tint/danger/success/warning/dark). Delivered to the framed
    /// page's preview bridge so a click on a real element resolves to the token it should edit. Ordered
    /// specific→general (the first match wins). Elements carrying <c>data-cw-style</c> take precedence over this.
    /// </summary>
    public List<ThemeRegionRule> RegionMap { get; set; } = new();

    /// <summary>An empty manifest for a theme that ships none (or one that failed to parse).</summary>
    public static ThemeLayoutManifest Empty(string themeSystemName) => new()
    {
        ThemeSystemName = themeSystemName,
        Sections = new List<ThemeLayoutSection>()
    };
}
