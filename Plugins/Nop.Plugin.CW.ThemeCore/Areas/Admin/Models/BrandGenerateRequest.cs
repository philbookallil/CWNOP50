using Nop.Plugin.CW.ThemeCore.Domain.BrandDesigner;

namespace Nop.Plugin.CW.ThemeCore.Areas.Admin.Models;

/// <summary>
/// AJAX payload for the Guided Brand Designer "Generate" call: the prompt plus the style currently in the
/// form, so tweak-only prompts ("a more intense red") can adjust what's already there instead of resetting it.
/// </summary>
public class BrandGenerateRequest
{
    /// <summary>Plain-English description, a preset name, or a tweak.</summary>
    public string Prompt { get; set; }

    /// <summary>The colours + button radius currently shown in the form (the modifier base).</summary>
    public BrandStyle Current { get; set; }

    /// <summary>
    /// Optional preview region the user selected (e.g. "hero", "danger", "button"). When set, colour/shape
    /// tweaks are scoped to that region's token instead of the default (primary / colour-word) resolution.
    /// </summary>
    public string Region { get; set; }
}
