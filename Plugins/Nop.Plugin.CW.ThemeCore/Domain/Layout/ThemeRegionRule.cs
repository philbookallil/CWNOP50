namespace Nop.Plugin.CW.ThemeCore.Domain.Layout;

/// <summary>
/// One live click-to-edit rule (Phase 2): a storefront CSS selector and the curated region a click on a
/// matching element maps to. Declared by a theme in <c>cw-theme.json</c> (<c>regionMap</c>) and delivered to
/// the preview bridge. The region string is one of the shared vocabulary
/// (header/nav/link/button/tint/danger/success/warning/dark).
/// </summary>
public class ThemeRegionRule
{
    /// <summary>A CSS selector matched against clicked storefront elements (e.g. ".button-1, .button-2").</summary>
    public string Selector { get; set; }

    /// <summary>The curated region a match maps to (drives which <c>--cw-*</c> token the popover edits).</summary>
    public string Region { get; set; }
}
