namespace Nop.Plugin.CW.ThemeCore.Domain.Layout;

/// <summary>
/// A storefront section (Home, Catalog, Product details, …) that offers a choice of layouts,
/// declared in a theme's <c>cw-theme.json</c> manifest.
/// </summary>
public class ThemeLayoutSection
{
    /// <summary>Stable key used in the layout setting key (e.g. "product").</summary>
    public string Key { get; set; }

    /// <summary>Human-readable title shown as the admin field label (e.g. "Product details screen").</summary>
    public string Title { get; set; }

    /// <summary>Key of the layout applied when the store has made no selection.</summary>
    public string Default { get; set; }

    /// <summary>The layouts this section offers.</summary>
    public List<ThemeLayoutOption> Layouts { get; set; } = new();
}
