namespace Nop.Plugin.CW.ThemeCore.Domain.BrandDesigner;

/// <summary>
/// The curated "vibe" presets, shown as one-click chips and matched against typed prompts.
/// Colours are the brand accent tokens as hex without '#'.
/// </summary>
public static class BrandPresets
{
    /// <summary>All presets, in the order the chips show them.</summary>
    public static IList<BrandPreset> All { get; } = new List<BrandPreset>
    {
        new()
        {
            Name = "Premium jewellery",
            Keywords = new[] { "jewel", "jewellery", "jewelry", "premium", "luxury", "luxe", "elegant", "boutique", "gold", "refined" },
            Palette = new BrandPalette { PrimaryColor = "1f3a5f", PrimaryTintColor = "eef1f7", SuccessColor = "2e7d55", DangerColor = "b23a48", WarningColor = "c1922e", DarkColor = "12203a" },
            ButtonRadius = 6
        },
        new()
        {
            Name = "Bold & techy",
            Keywords = new[] { "tech", "techy", "bold", "modern", "electric", "digital", "startup", "neon", "gadget", "innovative" },
            Palette = new BrandPalette { PrimaryColor = "2563eb", PrimaryTintColor = "e7efff", SuccessColor = "16a34a", DangerColor = "dc2626", WarningColor = "f59e0b", DarkColor = "0f172a" },
            ButtonRadius = 3
        },
        new()
        {
            Name = "Earthy organic",
            Keywords = new[] { "eco", "organic", "natural", "earth", "green", "sustainable", "botanical", "plant", "herbal", "wellness" },
            Palette = new BrandPalette { PrimaryColor = "3b7a57", PrimaryTintColor = "eaf3ee", SuccessColor = "4a8f4f", DangerColor = "c0563b", WarningColor = "caa63d", DarkColor = "24312a" },
            ButtonRadius = 10
        },
        new()
        {
            Name = "Corporate blue",
            Keywords = new[] { "corporate", "professional", "business", "office", "trust", "finance", "formal", "b2b", "enterprise", "reliable" },
            Palette = new BrandPalette { PrimaryColor = "00549f", PrimaryTintColor = "e8f1fa", SuccessColor = "2e7d32", DangerColor = "c62828", WarningColor = "ef6c00", DarkColor = "22303f" },
            ButtonRadius = 6
        },
        new()
        {
            Name = "Warm boutique",
            Keywords = new[] { "warm", "cozy", "cosy", "artisan", "craft", "handmade", "rustic", "terracotta", "coral", "friendly" },
            Palette = new BrandPalette { PrimaryColor = "c05746", PrimaryTintColor = "f8ede9", SuccessColor = "6a8e5a", DangerColor = "b8323b", WarningColor = "d98a2b", DarkColor = "3a2620" },
            ButtonRadius = 14
        },
        new()
        {
            Name = "Dark luxe",
            Keywords = new[] { "dark", "luxe", "sleek", "minimal", "monochrome", "noir", "sophisticated", "high-end", "moody", "exclusive" },
            Palette = new BrandPalette { PrimaryColor = "b08d57", PrimaryTintColor = "f3efe7", SuccessColor = "5e7d5a", DangerColor = "a63a3a", WarningColor = "c99a3a", DarkColor = "1a1a1c" },
            ButtonRadius = 2
        }
    };
}
