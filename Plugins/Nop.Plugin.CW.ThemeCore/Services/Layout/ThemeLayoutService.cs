using Nop.Plugin.CW.ThemeCore.Domain.Layout;
using Nop.Services.Configuration;

namespace Nop.Plugin.CW.ThemeCore.Services.Layout;

/// <summary>
/// Default <see cref="IThemeLayoutService"/> backed by nop's setting service (dynamic keys) and the theme manifest.
/// </summary>
public class ThemeLayoutService : IThemeLayoutService
{
    #region Fields

    protected readonly ISettingService _settingService;
    protected readonly IThemeLayoutManifestService _manifestService;

    #endregion

    #region Ctor

    public ThemeLayoutService(ISettingService settingService, IThemeLayoutManifestService manifestService)
    {
        _settingService = settingService;
        _manifestService = manifestService;
    }

    #endregion

    #region Methods

    public async Task<string> GetSelectedLayoutKeyAsync(string themeSystemName, string sectionKey, int storeId)
    {
        var manifest = await _manifestService.GetManifestAsync(themeSystemName);
        var section = FindSection(manifest, sectionKey);
        if (section is null)
            return null;

        var key = BuildSettingKey(themeSystemName, sectionKey);
        var selected = await _settingService.GetSettingByKeyAsync<string>(key, section.Default, storeId, loadSharedValueIfNotFound: true);

        //guard against a stale/unknown value (e.g. a layout removed from the manifest)
        if (string.IsNullOrWhiteSpace(selected) || section.Layouts.All(l => !KeyEquals(l.Key, selected)))
            selected = section.Default;

        return selected;
    }

    public async Task<bool> IsOverriddenForStoreAsync(string themeSystemName, string sectionKey, int storeId)
    {
        if (storeId <= 0)
            return false;

        var key = BuildSettingKey(themeSystemName, sectionKey);
        var setting = await _settingService.GetSettingAsync(key, storeId, loadSharedValueIfNotFound: false);
        return setting is not null;
    }

    public async Task SetSelectedLayoutKeyAsync(string themeSystemName, string sectionKey, string layoutKey, bool overrideForStore, int storeId)
    {
        var key = BuildSettingKey(themeSystemName, sectionKey);
        var value = layoutKey ?? string.Empty;

        if (storeId <= 0)
        {
            //all-stores (default) value
            await _settingService.SetSettingAsync(key, value, storeId: 0, clearCache: false);
            return;
        }

        if (overrideForStore)
        {
            await _settingService.SetSettingAsync(key, value, storeId, clearCache: false);
        }
        else
        {
            //remove any per-store value so the store inherits the all-stores default
            var existing = await _settingService.GetSettingAsync(key, storeId, loadSharedValueIfNotFound: false);
            if (existing is not null)
                await _settingService.DeleteSettingAsync(existing);
        }
    }

    public async Task<IList<string>> GetActivePageCssClassesAsync(string themeSystemName, int storeId)
    {
        var manifest = await _manifestService.GetManifestAsync(themeSystemName);
        var classes = new List<string>();

        foreach (var section in manifest.Sections)
        {
            var selectedKey = await GetSelectedLayoutKeyAsync(themeSystemName, section.Key, storeId);
            var option = section.Layouts.FirstOrDefault(l => KeyEquals(l.Key, selectedKey));
            if (option is null)
                continue;

            if (string.Equals(option.Type, CwThemeCoreDefaults.LayoutTypeCssClass, StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(option.CssClass))
            {
                classes.Add(option.CssClass.Trim());
            }
        }

        return classes;
    }

    #endregion

    #region Utilities

    protected static string BuildSettingKey(string themeSystemName, string sectionKey)
        => $"{CwThemeCoreDefaults.LayoutSettingKeyPrefix}.{themeSystemName}.{sectionKey}".ToLowerInvariant();

    protected static ThemeLayoutSection FindSection(ThemeLayoutManifest manifest, string sectionKey)
        => manifest.Sections.FirstOrDefault(s => KeyEquals(s.Key, sectionKey));

    protected static bool KeyEquals(string a, string b)
        => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    #endregion
}
