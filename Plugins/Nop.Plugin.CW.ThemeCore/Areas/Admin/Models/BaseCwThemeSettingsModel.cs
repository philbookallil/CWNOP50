using Nop.Web.Framework.Models;

namespace Nop.Plugin.CW.ThemeCore.Areas.Admin.Models;

/// <summary>
/// Base admin model for a per-theme settings page. Carries the store-scope context;
/// per-theme models add their own fields (+ paired *_OverrideForStore flags).
/// </summary>
public abstract record BaseCwThemeSettingsModel : BaseNopModel
{
    public int ActiveStoreScopeConfiguration { get; set; }
}
