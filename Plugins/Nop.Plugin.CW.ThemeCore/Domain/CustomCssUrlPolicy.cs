namespace Nop.Plugin.CW.ThemeCore.Domain;

/// <summary>
/// How the CSS sanitizer treats <c>url(...)</c> in admin-authored Custom CSS.
/// (The dangerous set — @import, expression(), javascript:/vbscript:, behavior:, markup-breakout —
/// is always blocked and is NOT configurable.)
/// </summary>
public enum CustomCssUrlPolicy
{
    /// <summary>Permit url(...) freely (external images, fonts, backgrounds).</summary>
    Allow = 0,

    /// <summary>Allow only data: URIs and relative/same-origin paths; strip external http(s) hosts.</summary>
    DataAndSameOriginOnly = 1,

    /// <summary>Strip all url(...) references.</summary>
    Block = 2
}
