using Nop.Plugin.CW.ThemeCore.Domain;

namespace Nop.Plugin.CW.ThemeCore.Services;

/// <summary>
/// Result of sanitizing admin-authored Custom CSS.
/// </summary>
public class CwCssSanitizeResult
{
    /// <summary>The sanitized CSS, safe to write to the served file.</summary>
    public string Css { get; set; } = string.Empty;

    /// <summary>Human-readable notes describing what was removed (surfaced to the admin).</summary>
    public IList<string> Warnings { get; set; } = new List<string>();
}

/// <summary>
/// Sanitizes admin-authored Custom CSS before it is served. The dangerous set
/// (@import, expression(), javascript:/vbscript:, behavior:/-moz-binding, markup-breakout)
/// is always removed; <c>url(...)</c> handling follows the supplied policy.
/// </summary>
public interface ICwCssSanitizer
{
    CwCssSanitizeResult Sanitize(string css, CustomCssUrlPolicy urlPolicy);
}
