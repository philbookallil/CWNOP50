using Nop.Core.Configuration;
using Nop.Plugin.CW.ThemeCore.Domain;

namespace Nop.Plugin.CW.ThemeCore.Settings;

/// <summary>
/// Central CW theme settings (per store). Holds the accent-colour palette exposed to
/// all CW plugins/themes as CSS custom properties, plus the store logo.
/// Colour values are stored as hex WITHOUT a leading '#'.
/// </summary>
/// <remarks>
/// Token names/defaults are provisional pending the design palette. Per-store hex values are
/// admin settings regardless of the final default palette.
/// </remarks>
public class CwThemeSettings : ISettings
{
    /// <summary>Primary brand accent (hex, no '#')</summary>
    public string PrimaryColor { get; set; }

    /// <summary>Light tint of the primary accent (hex, no '#')</summary>
    public string PrimaryTintColor { get; set; }

    /// <summary>Success/positive accent (hex, no '#')</summary>
    public string SuccessColor { get; set; }

    /// <summary>Danger/error accent (hex, no '#')</summary>
    public string DangerColor { get; set; }

    /// <summary>Warning accent (hex, no '#')</summary>
    public string WarningColor { get; set; }

    /// <summary>Dark ink/surface accent (hex, no '#')</summary>
    public string DarkColor { get; set; }

    /// <summary>
    /// Button corner radius in px, exposed as the <c>--cw-btn-radius</c> token (themes read it for button
    /// shape). ~2 = square, ~8 = rounded, 999 = pill. Default 8. 0 = emit nothing (fall back to the theme).
    /// </summary>
    public int ButtonRadius { get; set; } = 8;

    // NOTE (decide later): this theme LogoPictureId may overlap with nop core's
    // StoreInformationSettings.LogoPictureId (Admin → Settings → General & Common), which is also
    // per-store and likely points at the same logo. Options to revisit:
    //   (a) drop this and read the core StoreInformationSettings.LogoPictureId,
    //   (b) keep this only as a theme-specific override of the core logo, or
    //   (c) keep both as-is.
    // Also pending: whether SecondLogoLinkUrl (below) stays or the second logo is image-only.
    /// <summary>Store logo picture identifier (nop Picture)</summary>
    public int LogoPictureId { get; set; }

    /// <summary>
    /// Optional second (co-brand) logo picture identifier, shown on the right of the header alongside
    /// the primary logo (e.g. a partner brand). 0 = none. Per store.
    /// </summary>
    public int SecondLogoPictureId { get; set; }

    /// <summary>Optional URL the second logo links to (e.g. a partner site). Empty = not a link.</summary>
    public string SecondLogoLinkUrl { get; set; }

    /// <summary>
    /// When true, the header logos are laid out right-to-left instead of left-to-right — i.e. the second
    /// (co-brand) logo shows on the left and the primary logo on the right. Default false. Per store.
    /// </summary>
    public bool ReverseLogos { get; set; }

    /// <summary>
    /// When true, CW.ThemeCore does NOT emit the accent-colour custom properties (the :root{--cw-*} block),
    /// leaving colour control entirely to the active theme. Default false (accent colours applied).
    /// Custom CSS (Capability 2) is unaffected by this flag.
    /// </summary>
    public bool DisableAccentColors { get; set; }

    /// <summary>Raw admin-authored Custom CSS applied to the storefront (Capability 2). Stored raw; sanitized on generation.</summary>
    public string CustomCss { get; set; }

    /// <summary>How the sanitizer treats url(...) in the Custom CSS. The dangerous set is always blocked regardless.</summary>
    public CustomCssUrlPolicy CustomCssUrlPolicy { get; set; }
}
