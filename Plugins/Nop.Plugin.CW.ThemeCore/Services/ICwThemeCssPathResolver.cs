namespace Nop.Plugin.CW.ThemeCore.Services;

/// <summary>
/// Resolves where a store's generated Custom CSS file lives (§2 of the Phase 2a plan).
/// This is the single place to extend in Phase 2b: return the active CW theme's folder
/// when one is active, else the core plugin folder used now.
/// </summary>
public interface ICwThemeCssPathResolver
{
    /// <summary>
    /// App-relative (~/) virtual path of a store's generated file, e.g.
    /// <c>~/Plugins/CW.ThemeCore/Content/generated/theme.custom-3.css</c>.
    /// </summary>
    string GetGeneratedFileVirtualPath(int storeId);

    /// <summary>App-relative (~/) virtual path of the generated directory.</summary>
    string GetGeneratedDirectoryVirtualPath();
}
