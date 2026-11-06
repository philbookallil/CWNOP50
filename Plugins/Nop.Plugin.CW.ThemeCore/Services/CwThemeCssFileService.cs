using Nop.Core.Infrastructure;
using Nop.Plugin.CW.ThemeCore.Settings;
using Nop.Services.Configuration;
using Nop.Services.Stores;

namespace Nop.Plugin.CW.ThemeCore.Services;

/// <summary>
/// Writes each store's sanitized Custom CSS to a file inside the deployed plugin folder
/// (resolved via <see cref="ICwThemeCssPathResolver"/>), cache-busted by file write time.
/// </summary>
public class CwThemeCssFileService : ICwThemeCssFileService
{
    #region Fields

    protected readonly INopFileProvider _fileProvider;
    protected readonly ISettingService _settingService;
    protected readonly IStoreService _storeService;
    protected readonly ICwCssSanitizer _sanitizer;
    protected readonly ICwThemeCssPathResolver _pathResolver;

    #endregion

    #region Ctor

    public CwThemeCssFileService(
        INopFileProvider fileProvider,
        ISettingService settingService,
        IStoreService storeService,
        ICwCssSanitizer sanitizer,
        ICwThemeCssPathResolver pathResolver)
    {
        _fileProvider = fileProvider;
        _settingService = settingService;
        _storeService = storeService;
        _sanitizer = sanitizer;
        _pathResolver = pathResolver;
    }

    #endregion

    #region Utilities

    private string MapFilePath(int storeId)
        => _fileProvider.MapPath(_pathResolver.GetGeneratedFileVirtualPath(storeId));

    #endregion

    #region Methods

    public virtual async Task GenerateAsync(int storeId)
    {
        var settings = await _settingService.LoadSettingAsync<CwThemeSettings>(storeId);
        var sanitized = _sanitizer.Sanitize(settings.CustomCss, settings.CustomCssUrlPolicy);
        var physicalPath = MapFilePath(storeId);

        if (string.IsNullOrWhiteSpace(sanitized.Css))
        {
            if (File.Exists(physicalPath))
                File.Delete(physicalPath);
            return;
        }

        var directory = Path.GetDirectoryName(physicalPath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            Directory.CreateDirectory(directory);

        await File.WriteAllTextAsync(physicalPath, sanitized.Css, System.Text.Encoding.UTF8);
    }

    public virtual async Task GenerateForAllStoresAsync()
    {
        foreach (var store in await _storeService.GetAllStoresAsync())
            await GenerateAsync(store.Id);
    }

    public virtual async Task<CwGeneratedCssRef> GetRenderRefAsync(int storeId)
    {
        var settings = await _settingService.LoadSettingAsync<CwThemeSettings>(storeId);
        if (string.IsNullOrWhiteSpace(settings.CustomCss))
            return null;

        var physicalPath = MapFilePath(storeId);

        //lazily (re)create if missing — e.g. a redeploy wiped the plugin folder
        if (!File.Exists(physicalPath))
            await GenerateAsync(storeId);

        if (!File.Exists(physicalPath))
            return null;

        return new CwGeneratedCssRef
        {
            VirtualPath = _pathResolver.GetGeneratedFileVirtualPath(storeId),
            Version = File.GetLastWriteTimeUtc(physicalPath).Ticks
        };
    }

    public virtual Task DeleteAllAsync()
    {
        var directory = _fileProvider.MapPath(_pathResolver.GetGeneratedDirectoryVirtualPath());
        if (Directory.Exists(directory))
            Directory.Delete(directory, true);

        return Task.CompletedTask;
    }

    #endregion
}
