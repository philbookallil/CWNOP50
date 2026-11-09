using Nop.Core.Caching;

namespace Nop.Plugin.CW.ThemeCore;

/// <summary>
/// Represents constants for the CW Theme Core plugin
/// </summary>
public static class CwThemeCoreDefaults
{
    /// <summary>
    /// Plugin system name
    /// </summary>
    public static string SystemName => "CW.ThemeCore";

    /// <summary>
    /// System name of the CW.Core admin menu item this plugin anchors its menu under (soft dependency)
    /// </summary>
    public static string CwCoreMenuSystemName => "CW.Core";

    /// <summary>
    /// System name of this plugin's admin menu item
    /// </summary>
    public static string AdminMenuSystemName => "CW.ThemeCore.AdminMenu";

    /// <summary>
    /// Activity log keyword of one AI generation
    /// </summary>
    public const string AiGenerateActivityType = "CW.ThemeCore.AiGenerate";

    #region CSS custom property (token) names — provisional, pending design palette

    public const string CssVarPrimary = "--cw-primary";
    public const string CssVarPrimaryTint = "--cw-primary-tint";
    public const string CssVarSuccess = "--cw-success";
    public const string CssVarDanger = "--cw-danger";
    public const string CssVarWarning = "--cw-warning";
    public const string CssVarDark = "--cw-dark";

    /// <summary>Button corner radius token (value like "8px"), consumed by themes for button shape.</summary>
    public const string CssVarButtonRadius = "--cw-btn-radius";

    #endregion

    #region Layout system (per-store, per-section layout selection)

    /// <summary>
    /// File name of the per-theme layout manifest, read from the active theme's folder
    /// (~/Themes/{theme}/cw-theme.json). Declares the sections and layout options a theme offers.
    /// </summary>
    public const string ThemeManifestFileName = "cw-theme.json";

    /// <summary>
    /// Setting-key prefix for a per-store layout selection. Full key is
    /// "cwtheme.layout.{themeSystemName}.{sectionKey}" (lower-cased). Stored as a dynamic setting
    /// so new sections need no fixed ISettings property.
    /// </summary>
    public const string LayoutSettingKeyPrefix = "cwtheme.layout";

    /// <summary>Layout option type: value becomes a CSS class on the &lt;html&gt; tag.</summary>
    public const string LayoutTypeCssClass = "css-class";

    /// <summary>Layout option type: the theme selects a Razor partial by the option's Partial value.</summary>
    public const string LayoutTypePartial = "partial";

    /// <summary>Cache key for a parsed theme layout manifest ({0} = theme system name).</summary>
    public static CacheKey LayoutManifestCacheKey => new("CW.ThemeCore.layout.manifest.{0}");

    /// <summary>Cache key for a resolved theme brand-preview (fragments + css) ({0} = theme system name).</summary>
    public static CacheKey BrandPreviewCacheKey => new("CW.ThemeCore.brandpreview.{0}");

    #endregion
}
