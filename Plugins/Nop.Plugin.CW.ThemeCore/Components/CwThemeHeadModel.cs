namespace Nop.Plugin.CW.ThemeCore.Components;

/// <summary>
/// View model for the head injection: inline accent-colour tokens plus (optionally)
/// a link to the store's generated Custom CSS file.
/// </summary>
public class CwThemeHeadModel
{
    /// <summary>The <c>:root{--cw-*}</c> block (inline).</summary>
    public string RootCss { get; set; }

    /// <summary>App-relative (~/) path to the generated Custom CSS file, or null when there is none.</summary>
    public string CustomCssVirtualPath { get; set; }

    /// <summary>Cache-busting version for the Custom CSS link.</summary>
    public long CustomCssVersion { get; set; }

    /// <summary>
    /// When set (a validated live-preview request), the admin origin the preview bridge is allowed to talk to.
    /// Null on every normal storefront request → the bridge is not injected.
    /// </summary>
    public string PreviewAdminOrigin { get; set; }

    /// <summary>
    /// JSON array (camelCase <c>{ selector, region }</c>) of the active theme's live click-to-edit rules,
    /// inlined into <c>window.__cwPreview.regionMap</c> for the bridge. Only populated in preview mode; empty
    /// array when the theme declares none.
    /// </summary>
    public string PreviewRegionMapJson { get; set; }

    /// <summary>Cache-buster (bridge file's last-write ticks) appended to the bridge script URL, so a changed
    /// bridge always reloads in the framed iframe instead of a stale cached copy.</summary>
    public long PreviewBridgeVersion { get; set; }
}
