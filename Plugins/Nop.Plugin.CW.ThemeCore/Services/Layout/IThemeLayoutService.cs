namespace Nop.Plugin.CW.ThemeCore.Services.Layout;

/// <summary>
/// Resolves and persists a store's per-section layout selection against a theme's manifest.
/// Selections are stored as dynamic settings ("cwtheme.layout.{theme}.{section}") so adding a
/// section needs no fixed ISettings property.
/// </summary>
public interface IThemeLayoutService
{
    /// <summary>
    /// The layout key currently in effect for a section and store: the store's own selection,
    /// falling back to the all-stores value, then the manifest default. Null when the section
    /// isn't in the manifest.
    /// </summary>
    Task<string> GetSelectedLayoutKeyAsync(string themeSystemName, string sectionKey, int storeId);

    /// <summary>True when a store-specific (storeId &gt; 0) selection exists for this section.</summary>
    Task<bool> IsOverriddenForStoreAsync(string themeSystemName, string sectionKey, int storeId);

    /// <summary>
    /// Saves a section's layout selection. For storeId 0 the all-stores value is written. For a specific
    /// store, <paramref name="overrideForStore"/> writes a per-store value; false removes it (inherit).
    /// Caller is responsible for clearing the settings cache afterwards.
    /// </summary>
    Task SetSelectedLayoutKeyAsync(string themeSystemName, string sectionKey, string layoutKey, bool overrideForStore, int storeId);

    /// <summary>
    /// The CSS classes to place on the &lt;html&gt; tag for a store: the css-class value of each section's
    /// selected layout (empty values skipped). Used by the page-class filter.
    /// </summary>
    Task<IList<string>> GetActivePageCssClassesAsync(string themeSystemName, int storeId);
}
