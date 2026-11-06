using Nop.Core.Configuration;

namespace Nop.Plugin.CW.ThemeCore.Settings;

/// <summary>
/// Base for a CW *theme's own* per-store settings (layout toggles etc.).
/// A per-theme plugin (referencing CW.ThemeCore.dll — Pattern C) derives from this and adds
/// theme-specific properties. CW.ThemeCore's central settings (accent colours, Custom CSS)
/// are separate and NOT part of this base.
/// </summary>
public abstract class BaseCwThemeSettings : ISettings
{
}
