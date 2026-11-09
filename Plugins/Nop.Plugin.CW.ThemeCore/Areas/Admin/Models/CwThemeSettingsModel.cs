using System.ComponentModel.DataAnnotations;
using Nop.Plugin.CW.ThemeCore.Domain;
using Nop.Web.Framework.Models;
using Nop.Web.Framework.Mvc.ModelBinding;

namespace Nop.Plugin.CW.ThemeCore.Areas.Admin.Models;

/// <summary>
/// Admin settings model for CW Theme Core (accent colours + logo), per-store overridable.
/// Colour values are hex WITHOUT a leading '#'.
/// </summary>
public record CwThemeSettingsModel : BaseNopModel
{
    public int ActiveStoreScopeConfiguration { get; set; }

    [NopResourceDisplayName("Plugins.CW.ThemeCore.Fields.PrimaryColor")]
    public string PrimaryColor { get; set; }
    public bool PrimaryColor_OverrideForStore { get; set; }

    [NopResourceDisplayName("Plugins.CW.ThemeCore.Fields.PrimaryTintColor")]
    public string PrimaryTintColor { get; set; }
    public bool PrimaryTintColor_OverrideForStore { get; set; }

    [NopResourceDisplayName("Plugins.CW.ThemeCore.Fields.SuccessColor")]
    public string SuccessColor { get; set; }
    public bool SuccessColor_OverrideForStore { get; set; }

    [NopResourceDisplayName("Plugins.CW.ThemeCore.Fields.DangerColor")]
    public string DangerColor { get; set; }
    public bool DangerColor_OverrideForStore { get; set; }

    [NopResourceDisplayName("Plugins.CW.ThemeCore.Fields.WarningColor")]
    public string WarningColor { get; set; }
    public bool WarningColor_OverrideForStore { get; set; }

    [NopResourceDisplayName("Plugins.CW.ThemeCore.Fields.DarkColor")]
    public string DarkColor { get; set; }
    public bool DarkColor_OverrideForStore { get; set; }

    [NopResourceDisplayName("Plugins.CW.ThemeCore.Fields.ButtonRadius")]
    public int ButtonRadius { get; set; } = 8;
    public bool ButtonRadius_OverrideForStore { get; set; }

    [NopResourceDisplayName("Plugins.CW.ThemeCore.Fields.LogoPictureId")]
    [UIHint("Picture")]
    public int LogoPictureId { get; set; }
    public bool LogoPictureId_OverrideForStore { get; set; }

    [NopResourceDisplayName("Plugins.CW.ThemeCore.Fields.SecondLogoPictureId")]
    [UIHint("Picture")]
    public int SecondLogoPictureId { get; set; }
    public bool SecondLogoPictureId_OverrideForStore { get; set; }

    [NopResourceDisplayName("Plugins.CW.ThemeCore.Fields.SecondLogoLinkUrl")]
    public string SecondLogoLinkUrl { get; set; }
    public bool SecondLogoLinkUrl_OverrideForStore { get; set; }

    [NopResourceDisplayName("Plugins.CW.ThemeCore.Fields.ReverseLogos")]
    public bool ReverseLogos { get; set; }
    public bool ReverseLogos_OverrideForStore { get; set; }

    [NopResourceDisplayName("Plugins.CW.ThemeCore.Fields.DisableAccentColors")]
    public bool DisableAccentColors { get; set; }
    public bool DisableAccentColors_OverrideForStore { get; set; }

    [NopResourceDisplayName("Plugins.CW.ThemeCore.Fields.CustomCss")]
    public string CustomCss { get; set; }
    public bool CustomCss_OverrideForStore { get; set; }

    [NopResourceDisplayName("Plugins.CW.ThemeCore.Fields.CustomCssUrlPolicy")]
    public CustomCssUrlPolicy CustomCssUrlPolicy { get; set; }
    public bool CustomCssUrlPolicy_OverrideForStore { get; set; }

    /// <summary>Per-store layout selection, one row per section declared in the active theme's manifest.</summary>
    public List<LayoutSectionModel> LayoutSections { get; set; } = new();

    /// <summary>Guided Brand Designer: curated "vibe" presets, rendered as one-click chips coloured with each preset's primary colour.</summary>
    public IList<BrandPresetChipModel> BrandPresets { get; set; } = new List<BrandPresetChipModel>();

    /// <summary>The store's active storefront theme (informational; used for the "not a CW theme" note).</summary>
    /// <summary>The AI assistant card, which saves through its own action.</summary>
    public BrandAiCardModel AiCard { get; set; } = new();

    public string PreviewThemeName { get; set; }

    /// <summary>True when the active theme ships a Brand-Designer preview → accurate preview + picker enabled.</summary>
    public bool HasThemePreview { get; set; }

    /// <summary>Inline preview CSS shipped by the active theme (styles the fragments below).</summary>
    public string PreviewCss { get; set; }

    /// <summary>The active theme's preview views (selectable mocks), in declaration order.</summary>
    public IList<PreviewViewModel> PreviewViews { get; set; } = new List<PreviewViewModel>();

    /// <summary>Preview header logo (mirrors the live store): resolved URL of the PRIMARY logo — nop core
    /// StoreInformationSettings.LogoPictureId, what the CW.Standard header renders. Empty when unset.</summary>
    public string PreviewPrimaryLogoUrl { get; set; }

    /// <summary>Preview header logo: resolved URL of the SECOND (co-brand) logo — CwThemeSettings.SecondLogoPictureId. Empty when unset.</summary>
    public string PreviewSecondLogoUrl { get; set; }

    /// <summary>Live preview: true when the configured store has a resolvable public URL to frame.</summary>
    public bool LivePreviewEnabled { get; set; }

    /// <summary>Id of the store the live preview frames — sent back when the page asks for a fresh token.</summary>
    public int PreviewStoreId { get; set; }

    /// <summary>Origin (scheme://host[:port]) of the configured store — the postMessage target/allowed reply origin.</summary>
    public string PreviewStoreOrigin { get; set; }

    /// <summary>Live-preview URLs on the store's own domain, keyed by preview view (home/categories/product/cart/
    /// checkout). Token-free: the page appends the current <see cref="PreviewToken"/> when it loads a view.</summary>
    public IDictionary<string, string> PreviewUrls { get; set; } = new Dictionary<string, string>();

    /// <summary>Signed live-preview token minted with the page.</summary>
    public string PreviewToken { get; set; }

    /// <summary>Seconds after page load at which the page asks for a fresh token before loading a view.</summary>
    public int PreviewTokenRefreshAfterSeconds { get; set; }

    /// <summary>Layout CSS-class map for the live bridge: sectionKey → (layoutKey → cssClass), from the theme manifest.</summary>
    public IDictionary<string, IDictionary<string, string>> PreviewLayoutClasses { get; set; } = new Dictionary<string, IDictionary<string, string>>();
}

/// <summary>One selectable preview view rendered in the Brand Designer (theme-provided HTML fragment).</summary>
public record PreviewViewModel : BaseNopModel
{
    public string Key { get; set; }
    public string Title { get; set; }
    public string Html { get; set; }
}

/// <summary>One "vibe" preset chip: its name (also the prompt) and its primary colour (hex, no '#') used as the chip fill.</summary>
public record BrandPresetChipModel : BaseNopModel
{
    public string Name { get; set; }
    public string PrimaryColor { get; set; }
}

/// <summary>
/// One section's layout choice on the settings page. The list of options is built from the theme manifest,
/// so new sections/layouts appear here with no model changes.
/// </summary>
public record LayoutSectionModel : BaseNopModel
{
    /// <summary>Section key (round-tripped so POST knows which section each row is).</summary>
    public string SectionKey { get; set; }

    /// <summary>Field label (section title from the manifest).</summary>
    public string Title { get; set; }

    /// <summary>The selected layout key.</summary>
    public string SelectedLayoutKey { get; set; }

    /// <summary>Whether this section is overridden for the current store scope.</summary>
    public bool OverrideForStore { get; set; }

    /// <summary>The layouts this section offers — rendered as wireframe picker cards, and used to fill the hidden
    /// select that binds the chosen value and carries the per-store override enable/disable.</summary>
    public IList<LayoutOptionModel> Options { get; set; } = new List<LayoutOptionModel>();
}

/// <summary>One layout option: its key + title + the wireframe token that selects its picker-card mini-diagram.</summary>
public record LayoutOptionModel : BaseNopModel
{
    public string Key { get; set; }
    public string Title { get; set; }
    public string Wireframe { get; set; }
}
