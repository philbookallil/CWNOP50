using Nop.Plugin.CW.ThemeCore.Domain.BrandDesigner;

namespace Nop.Plugin.CW.ThemeCore.Services.BrandDesigner;

/// <summary>
/// Resolves a theme's Brand-Designer preview: reads the theme's <c>brandPreview</c> declaration
/// (from <c>cw-theme.json</c>) and the fragment/CSS files it points to. A theme that ships a preview is a
/// "CW theme" (picker enabled); one that doesn't returns <see cref="ThemeBrandPreview.None"/> (generic fallback).
/// </summary>
public interface IThemeBrandPreviewService
{
    /// <summary>Load the resolved preview (inline HTML + CSS) for a theme, or a "none" result if it ships none.</summary>
    Task<ThemeBrandPreview> GetPreviewAsync(string themeSystemName);
}
