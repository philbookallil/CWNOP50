using Nop.Plugin.CW.ThemeCore.Areas.Admin.Models;
using Nop.Web.Framework.Mvc.ModelBinding;

namespace Nop.Plugin.CW.Theme.Standard.Areas.Admin.Models;

/// <summary>
/// Admin model for the CW.Standard theme settings page. Inherits the store-scope base
/// (BaseCwThemeSettingsModel) from CW.ThemeCore.
/// </summary>
public record StandardThemeSettingsModel : BaseCwThemeSettingsModel
{
    [NopResourceDisplayName("Plugins.CW.Theme.Standard.Fields.DarkNavBar")]
    public bool DarkNavBar { get; set; }
    public bool DarkNavBar_OverrideForStore { get; set; }

    [NopResourceDisplayName("Plugins.CW.Theme.Standard.Fields.SquareCorners")]
    public bool SquareCorners { get; set; }
    public bool SquareCorners_OverrideForStore { get; set; }
}
