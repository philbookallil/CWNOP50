namespace Nop.Plugin.CW.ThemeCore.Domain.BrandDesigner;

/// <summary>
/// A curated "vibe" preset — shown as a one-click chip in the admin, and used to match typed prompts.
/// </summary>
public class BrandPreset
{
    /// <summary>Display name (e.g. "Premium jewellery").</summary>
    public string Name { get; set; }

    /// <summary>Lower-case keywords that map a typed prompt to this preset.</summary>
    public string[] Keywords { get; set; } = System.Array.Empty<string>();

    /// <summary>The palette this preset applies.</summary>
    public BrandPalette Palette { get; set; }

    /// <summary>Button corner radius (px) this vibe implies — e.g. sharp for techy, soft for boutique. Default 8.</summary>
    public int ButtonRadius { get; set; } = 8;
}
