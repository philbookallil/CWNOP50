using System.Text;
using System.Text.Json;
using Nop.Core.Caching;
using Nop.Core.Infrastructure;
using Nop.Plugin.CW.ThemeCore.Domain.Layout;

namespace Nop.Plugin.CW.ThemeCore.Services.Layout;

/// <summary>
/// Reads and caches the per-theme layout manifest (<c>cw-theme.json</c>). Parsing is tolerant:
/// a missing or malformed file yields an empty manifest so the storefront never breaks.
/// </summary>
public class ThemeLayoutManifestService : IThemeLayoutManifestService
{
    #region Fields

    protected readonly INopFileProvider _fileProvider;
    protected readonly IStaticCacheManager _staticCacheManager;

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    #endregion

    #region Ctor

    public ThemeLayoutManifestService(INopFileProvider fileProvider, IStaticCacheManager staticCacheManager)
    {
        _fileProvider = fileProvider;
        _staticCacheManager = staticCacheManager;
    }

    #endregion

    #region Methods

    public async Task<ThemeLayoutManifest> GetManifestAsync(string themeSystemName)
    {
        if (string.IsNullOrWhiteSpace(themeSystemName))
            return ThemeLayoutManifest.Empty(themeSystemName);

        var cacheKey = _staticCacheManager.PrepareKeyForDefaultCache(CwThemeCoreDefaults.LayoutManifestCacheKey, themeSystemName);
        return await _staticCacheManager.GetAsync(cacheKey, () => LoadManifestAsync(themeSystemName));
    }

    #endregion

    #region Utilities

    protected virtual async Task<ThemeLayoutManifest> LoadManifestAsync(string themeSystemName)
    {
        var path = _fileProvider.MapPath($"~/Themes/{themeSystemName}/{CwThemeCoreDefaults.ThemeManifestFileName}");
        if (!_fileProvider.FileExists(path))
            return ThemeLayoutManifest.Empty(themeSystemName);

        try
        {
            var json = await _fileProvider.ReadAllTextAsync(path, Encoding.UTF8);
            var manifest = JsonSerializer.Deserialize<ThemeLayoutManifest>(json, _jsonOptions);
            if (manifest is null)
                return ThemeLayoutManifest.Empty(themeSystemName);

            //normalise so callers never hit nulls
            manifest.ThemeSystemName ??= themeSystemName;
            manifest.Sections ??= new List<ThemeLayoutSection>();
            foreach (var section in manifest.Sections)
                section.Layouts ??= new List<ThemeLayoutOption>();
            manifest.RegionMap ??= new List<ThemeRegionRule>();

            return manifest;
        }
        catch
        {
            //a malformed manifest must never take the storefront down
            return ThemeLayoutManifest.Empty(themeSystemName);
        }
    }

    #endregion
}
