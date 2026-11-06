namespace Nop.Plugin.CW.ThemeCore.Services;

/// <summary>
/// Phase 2a resolver: generated CSS lives inside the deployed <c>CW.ThemeCore</c> plugin folder
/// (so it only exists/serves when the plugin is deployed).
/// </summary>
/// <remarks>
/// Phase 2b extension point: when a CW theme is active, return
/// <c>~/Themes/{theme}/Content/css/theme.custom-{storeId}.css</c> instead.
/// </remarks>
public class CwThemeCssPathResolver : ICwThemeCssPathResolver
{
    private const string GeneratedDirectory = "~/Plugins/CW.ThemeCore/Content/generated";

    public virtual string GetGeneratedDirectoryVirtualPath()
        => GeneratedDirectory;

    public virtual string GetGeneratedFileVirtualPath(int storeId)
        => $"{GeneratedDirectory}/theme.custom-{storeId}.css";
}
