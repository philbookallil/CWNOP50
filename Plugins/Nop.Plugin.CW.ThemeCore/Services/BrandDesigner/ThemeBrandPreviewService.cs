using System.Text;
using Nop.Core.Caching;
using Nop.Core.Infrastructure;
using Nop.Plugin.CW.ThemeCore.Domain.BrandDesigner;
using Nop.Plugin.CW.ThemeCore.Services.Layout;

namespace Nop.Plugin.CW.ThemeCore.Services.BrandDesigner;

/// <summary>
/// Reads a theme's <c>brandPreview</c> declaration (via the layout-manifest service) and resolves it to inline
/// content by reading the fragment/CSS files from the theme folder. Tolerant: a missing declaration or file
/// yields a "none"/partial result rather than throwing, so the admin page never breaks on a bad theme.
/// </summary>
public class ThemeBrandPreviewService : IThemeBrandPreviewService
{
    #region Fields

    protected readonly IThemeLayoutManifestService _manifestService;
    protected readonly INopFileProvider _fileProvider;
    protected readonly IStaticCacheManager _staticCacheManager;

    #endregion

    #region Ctor

    public ThemeBrandPreviewService(
        IThemeLayoutManifestService manifestService,
        INopFileProvider fileProvider,
        IStaticCacheManager staticCacheManager)
    {
        _manifestService = manifestService;
        _fileProvider = fileProvider;
        _staticCacheManager = staticCacheManager;
    }

    #endregion

    #region Methods

    public async Task<ThemeBrandPreview> GetPreviewAsync(string themeSystemName)
    {
        if (string.IsNullOrWhiteSpace(themeSystemName))
            return ThemeBrandPreview.None(themeSystemName);

        var cacheKey = _staticCacheManager.PrepareKeyForDefaultCache(CwThemeCoreDefaults.BrandPreviewCacheKey, themeSystemName);
        return await _staticCacheManager.GetAsync(cacheKey, () => LoadPreviewAsync(themeSystemName));
    }

    #endregion

    #region Utilities

    protected virtual async Task<ThemeBrandPreview> LoadPreviewAsync(string themeSystemName)
    {
        var manifest = await _manifestService.GetManifestAsync(themeSystemName);
        var declaration = manifest?.BrandPreview;
        if (declaration?.Views is null || declaration.Views.Count == 0)
            return ThemeBrandPreview.None(themeSystemName);

        var preview = new ThemeBrandPreview { ThemeSystemName = themeSystemName };

        if (!string.IsNullOrWhiteSpace(declaration.Css))
            preview.Css = await ReadThemeFileAsync(themeSystemName, declaration.Css) ?? string.Empty;

        foreach (var view in declaration.Views)
        {
            if (string.IsNullOrWhiteSpace(view.Key) || string.IsNullOrWhiteSpace(view.File))
                continue;

            //a declared-but-missing fragment is skipped rather than fatal
            var html = await ReadThemeFileAsync(themeSystemName, view.File);
            if (html is null)
                continue;

            preview.Views.Add(new ThemeBrandPreviewView
            {
                Key = view.Key,
                Title = string.IsNullOrWhiteSpace(view.Title) ? view.Key : view.Title,
                Html = html
            });
        }

        return preview;
    }

    /// <summary>Read a theme-relative file (e.g. "brand-preview/home.html"); null if missing or unreadable.</summary>
    private async Task<string> ReadThemeFileAsync(string themeSystemName, string relativePath)
    {
        var relative = relativePath.Replace('\\', '/').TrimStart('/');
        var path = _fileProvider.MapPath($"~/Themes/{themeSystemName}/{relative}");
        if (!_fileProvider.FileExists(path))
            return null;

        try
        {
            return await _fileProvider.ReadAllTextAsync(path, Encoding.UTF8);
        }
        catch
        {
            return null;
        }
    }

    #endregion
}
