namespace Nop.Plugin.CW.ThemeCore.Domain.Layout;

/// <summary>
/// The <c>brandPreview</c> node of a theme's <c>cw-theme.json</c>: the preview stylesheet and the per-view
/// HTML fragments the theme ships for the admin Brand Designer. Paths are relative to the theme root
/// (e.g. <c>brand-preview/home.html</c>). Its presence marks the theme as a "CW theme" for the picker.
/// </summary>
public class BrandPreviewManifest
{
    /// <summary>Relative path to the preview stylesheet (styles the fragments; token-driven via --cw-*).</summary>
    public string Css { get; set; }

    /// <summary>The preview views the theme offers (one selectable mock per storefront page).</summary>
    public List<BrandPreviewViewRef> Views { get; set; } = new();
}

/// <summary>One preview view a theme declares: its key, display title, and the HTML fragment file.</summary>
public class BrandPreviewViewRef
{
    /// <summary>Stable key used for the view selector (e.g. "home", "category", "product").</summary>
    public string Key { get; set; }

    /// <summary>Label shown in the preview's view dropdown.</summary>
    public string Title { get; set; }

    /// <summary>Relative path to the HTML fragment (theme-authored; carries the data-cw-style hooks).</summary>
    public string File { get; set; }
}
