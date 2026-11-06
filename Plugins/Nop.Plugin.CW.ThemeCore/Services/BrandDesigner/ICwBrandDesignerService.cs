using Nop.Plugin.CW.ThemeCore.Domain.BrandDesigner;

namespace Nop.Plugin.CW.ThemeCore.Services.BrandDesigner;

/// <summary>
/// Produces a brand style (palette + shape knobs) from a plain-English prompt. This MVP is a
/// deterministic MOCK (no external AI) so the demo is instant, free and reliable; the interface is
/// shaped so the real Claude-backed implementation can be dropped in later without any UI change.
/// </summary>
public interface ICwBrandDesignerService
{
    /// <summary>The curated "vibe" presets, surfaced as one-click chips in the admin.</summary>
    IList<BrandPreset> GetPresets();

    /// <summary>
    /// Generate a style from a prompt. A prompt is read as <em>base vibe → modifiers</em>: if it names
    /// (or clearly matches) a vibe, that preset is the base; otherwise <paramref name="current"/> is the
    /// base so tweak-only prompts ("a more intense red", "squarer buttons") adjust what's already there.
    /// Modifiers (intensity, lightness, warmth, button shape) are then applied. Deterministic:
    /// same prompt + same base → same result.
    /// </summary>
    /// <param name="prompt">Plain-English description, a preset name, or a tweak.</param>
    /// <param name="current">The style currently in the form; used as the base when no vibe is matched. May be null.</param>
    /// <param name="region">
    /// Optional selected preview element (e.g. "hero", "danger", "button"). When set, tweaks are scoped to that
    /// region's token instead of the default colour-word/primary resolution, and no new vibe is matched.
    /// </param>
    Task<BrandStyle> GenerateAsync(string prompt, BrandStyle current, string region = null);

    /// <summary>A fresh, tasteful random style ("Surprise me").</summary>
    BrandStyle Random();
}
