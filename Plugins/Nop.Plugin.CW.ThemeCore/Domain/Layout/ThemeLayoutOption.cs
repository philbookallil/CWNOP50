namespace Nop.Plugin.CW.ThemeCore.Domain.Layout;

/// <summary>
/// A single selectable layout for a section, declared in a theme's <c>cw-theme.json</c> manifest.
/// </summary>
public class ThemeLayoutOption
{
    /// <summary>Stable key persisted as the per-store selection (e.g. "two-column").</summary>
    public string Key { get; set; }

    /// <summary>Human-readable title shown in the admin dropdown (e.g. "Two Column").</summary>
    public string Title { get; set; }

    /// <summary>
    /// How the layout is applied: <see cref="CwThemeCoreDefaults.LayoutTypeCssClass"/> (a CSS class on
    /// the &lt;html&gt; tag) or <see cref="CwThemeCoreDefaults.LayoutTypePartial"/> (theme selects a partial).
    /// </summary>
    public string Type { get; set; }

    /// <summary>CSS class applied when this option is selected (for <c>css-class</c> layouts). May be empty for a default.</summary>
    public string CssClass { get; set; }

    /// <summary>Partial name/path the theme renders when this option is selected (for <c>partial</c> layouts).</summary>
    public string Partial { get; set; }

    /// <summary>
    /// Optional wireframe token that selects the mini-diagram shown on the admin layout picker card
    /// (e.g. "full", "boxed", "grid-2", "grid-3", "split", "stack", "narrow"). Unknown/empty → a generic
    /// wireframe. Purely a visual hint; it does not affect the storefront.
    /// </summary>
    public string Wireframe { get; set; }
}
