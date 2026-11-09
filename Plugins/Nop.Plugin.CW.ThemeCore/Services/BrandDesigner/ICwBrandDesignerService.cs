using Nop.Plugin.CW.ThemeCore.Domain.BrandDesigner;

namespace Nop.Plugin.CW.ThemeCore.Services.BrandDesigner;

/// <summary>
/// Produces a brand style (palette + shape knobs): from a preset, from the AI assistant, or offline.
/// </summary>
public interface ICwBrandDesignerService
{
    /// <summary>The curated "vibe" presets, surfaced as one-click chips in the admin.</summary>
    IList<BrandPreset> GetPresets();

    /// <summary>
    /// Generate a style from a prompt, using the AI connection in effect for the admin's store scope, or the offline
    /// generator when there is none.
    /// </summary>
    /// <param name="prompt">Plain-English description, a preset name, or a tweak.</param>
    /// <param name="current">The style currently in the form. May be null.</param>
    /// <param name="region">
    /// Optional selected preview element (e.g. "hero", "danger", "button"). When set, only that element's fields change.
    /// </param>
    /// <returns>The result; its Error is set, and its Style null, when generation failed</returns>
    Task<BrandGenerateResult> GenerateAsync(string prompt, BrandStyle current, string region = null);

    /// <summary>A fresh, tasteful random style ("Surprise me").</summary>
    BrandStyle Random();

    /// <summary>Check the connection in effect for the admin's store scope.</summary>
    /// <returns>An admin-readable message when it failed; null when it works</returns>
    Task<string> TestConnectionAsync();
}
