using Nop.Plugin.CW.ThemeCore.Domain.Layout;

namespace Nop.Plugin.CW.ThemeCore.Services.Layout;

/// <summary>
/// Loads (and caches) a theme's layout manifest from <c>~/Themes/{theme}/cw-theme.json</c>.
/// </summary>
public interface IThemeLayoutManifestService
{
    /// <summary>
    /// Returns the parsed manifest for the given theme, or an empty manifest when the theme ships none
    /// or the file cannot be parsed. Never returns null.
    /// </summary>
    Task<ThemeLayoutManifest> GetManifestAsync(string themeSystemName);
}
