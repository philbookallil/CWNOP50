namespace Nop.Plugin.CW.ThemeCore.Domain.BrandDesigner;

/// <summary>
/// A theme's brand preview, resolved to inline content: the preview CSS and each view's HTML, read from the
/// theme's declared fragment files. <see cref="HasPreview"/> is the capability flag the admin uses to decide
/// whether to render the accurate preview + enable the point-and-click picker, or fall back to a generic one.
/// </summary>
public class ThemeBrandPreview
{
    public string ThemeSystemName { get; set; }

    /// <summary>Inline CSS content that styles the fragments (may be empty).</summary>
    public string Css { get; set; }

    /// <summary>The resolved views (key + title + inline HTML), in declaration order.</summary>
    public List<ThemeBrandPreviewView> Views { get; set; } = new();

    /// <summary>True when the theme shipped at least one usable preview view — i.e. it's a CW theme.</summary>
    public bool HasPreview => Views is { Count: > 0 };

    /// <summary>The "no preview" result for a non-CW theme (or one that failed to load).</summary>
    public static ThemeBrandPreview None(string themeSystemName) => new() { ThemeSystemName = themeSystemName };
}

/// <summary>One resolved preview view: its key/title and the inline HTML fragment (with data-cw-style hooks).</summary>
public class ThemeBrandPreviewView
{
    public string Key { get; set; }
    public string Title { get; set; }
    public string Html { get; set; }
}
