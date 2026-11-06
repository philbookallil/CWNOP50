using Nop.Plugin.CW.ThemeCore.Settings;

namespace Nop.Plugin.CW.Theme.Standard.Settings;

/// <summary>
/// Per-store settings specific to the CW.Standard theme. Inherits the shared base from
/// CW.ThemeCore (Pattern C — DLL reference). The theme reads these by setting key
/// (`standardthemesettings.*`) and adjusts its CSS tokens accordingly.
/// </summary>
public class StandardThemeSettings : BaseCwThemeSettings
{
    /// <summary>Use a light navigation bar instead of the dark default.</summary>
    public bool LightNavBar { get; set; }

    /// <summary>Use square corners instead of rounded.</summary>
    public bool SquareCorners { get; set; }
}
