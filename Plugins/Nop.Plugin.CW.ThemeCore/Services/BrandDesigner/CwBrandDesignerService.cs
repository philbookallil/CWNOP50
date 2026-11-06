using Nop.Plugin.CW.ThemeCore.Domain.BrandDesigner;

namespace Nop.Plugin.CW.ThemeCore.Services.BrandDesigner;

/// <summary>
/// MVP mock implementation of <see cref="ICwBrandDesignerService"/> — deterministic, no external calls.
/// A prompt is interpreted as <em>base vibe → modifiers</em>: a curated preset (or the current style) is the
/// base, then plain-English tweaks (intensity, lightness, warmth, button shape) are applied on top. The same
/// prompt against the same base always yields the same result.
/// </summary>
public class CwBrandDesignerService : ICwBrandDesignerService
{
    #region Fields

    private static readonly Random _rng = new();

    // brand accent tokens: primary, primary-tint, success, danger, warning, dark (hex, no '#')
    private static readonly IList<BrandPreset> _presets = new List<BrandPreset>
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

    // words → the accent token a colour-scoped modifier targets
    private static readonly (string[] Words, ColorTarget Target)[] _colorWords =
    {
        (new[] { "red", "crimson", "scarlet" }, ColorTarget.Danger),
        (new[] { "green", "emerald", "olive" }, ColorTarget.Success),
        (new[] { "blue", "navy", "azure", "teal", "indigo" }, ColorTarget.Primary),
        (new[] { "amber", "yellow", "gold", "orange" }, ColorTarget.Warning)
    };

    private enum ColorTarget { Primary, PrimaryTint, Success, Danger, Warning, Dark }

    // preview region (data-cw-style) → the accent token a scoped tweak targets
    private static ColorTarget? RegionToTarget(string region) => region?.Trim().ToLowerInvariant() switch
    {
        "header" or "button" or "nav" or "link" => ColorTarget.Primary,
        "hero" or "tint" => ColorTarget.PrimaryTint,
        "danger" => ColorTarget.Danger,
        "success" => ColorTarget.Success,
        "warning" => ColorTarget.Warning,
        "dark" or "footer" => ColorTarget.Dark,
        _ => null
    };

    #endregion

    #region Methods

    public IList<BrandPreset> GetPresets() => _presets;

    public Task<BrandStyle> GenerateAsync(string prompt, BrandStyle current, string region = null)
    {
        var text = (prompt ?? string.Empty).ToLowerInvariant().Trim();
        var scopedTarget = RegionToTarget(region);

        // exact preset name (used by the vibe chips) → return that preset verbatim, no modifier pass
        var named = _presets.FirstOrDefault(p => string.Equals(p.Name, text, StringComparison.OrdinalIgnoreCase));
        if (named != null)
            return Task.FromResult(BrandStyle.FromPalette(named.Palette, named.ButtonRadius));

        // a scoped tweak ("more square" on the selected button) always adjusts the current style, never a new vibe
        var best = scopedTarget.HasValue ? null : BestPresetMatch(text);
        BrandStyle style;
        if (best != null)
            style = BrandStyle.FromPalette(best.Palette, best.ButtonRadius);
        else if (HasUsableColors(current))
            style = current.Clone();
        else
            style = FromHue(HashToHue(text));

        // apply plain-English tweaks on top of the base (scoped to the selected region when one was given)
        ApplyModifiers(style, text, region);

        return Task.FromResult(style);
    }

    public BrandStyle Random() => FromHue(_rng.NextDouble() * 360.0);

    #endregion

    #region Utilities — prompt interpretation

    /// <summary>Score presets by how many of their keywords appear in the prompt; return the best (or null).</summary>
    private static BrandPreset BestPresetMatch(string text)
    {
        BrandPreset best = null;
        var bestScore = 0;
        foreach (var preset in _presets)
        {
            var score = preset.Keywords.Count(k => text.Contains(k));
            if (score > bestScore)
            {
                bestScore = score;
                best = preset;
            }
        }
        return best;
    }

    private static bool HasUsableColors(BrandStyle s)
        => s != null && IsHex(s.PrimaryColor) && IsHex(s.PrimaryTintColor) && IsHex(s.DarkColor);

    /// <summary>
    /// Apply the modifier phrases found in the prompt to <paramref name="style"/> in place. When
    /// <paramref name="region"/> names a selected preview element, colour/shape tweaks are confined to that
    /// region's token; otherwise they target colour-word matches (default primary) and the brand hue family.
    /// </summary>
    private static void ApplyModifiers(BrandStyle style, string text, string region = null)
    {
        if (string.IsNullOrEmpty(text))
            return;

        var scopedTarget = RegionToTarget(region);
        var scoped = scopedTarget.HasValue;

        // --- button shape --- (unscoped, or the selected region IS the buttons — never reshape buttons while another element is selected)
        if (!scoped || string.Equals(region?.Trim(), "button", StringComparison.OrdinalIgnoreCase))
        {
            if (ContainsAny(text, "pill", "fully round", "fully rounded", "round button", "circular"))
                style.ButtonRadius = 999;
            else if (ContainsAny(text, "square", "sharp", "boxy", "angular", "rectangular"))
                style.ButtonRadius = 2;
            else if (ContainsAny(text, "rounded", "soft corner", "soft-corner", "pill-ish"))
                style.ButtonRadius = 12;
        }

        // --- colour tweaks: intensity / lightness — scoped to the selected region, else colour words (default primary) ---
        var targets = scoped ? new List<ColorTarget> { scopedTarget.Value } : ResolveColorTargets(text);

        var satDelta = 0.0;
        if (ContainsAny(text, "more intense", "intense", "vivid", "vibrant", "saturated", "bold", "punchy", "rich", "stronger", "more colour", "more color"))
            satDelta += 0.22;
        if (ContainsAny(text, "muted", "softer", "soft ", "subtle", "pastel", "pale", "washed", "desaturat", "toned down", "calmer", "gentle"))
            satDelta -= 0.22;

        var lightDelta = 0.0;
        if (ContainsAny(text, "darker", "deeper", "deep ", "moodier", "richer dark"))
            lightDelta -= 0.12;
        if (ContainsAny(text, "lighter", "brighter", "airier", "paler"))
            lightDelta += 0.12;

        if (satDelta != 0.0 || lightDelta != 0.0)
            foreach (var target in targets)
                AdjustColor(style, target, satDelta, lightDelta);

        // --- warmth: scoped → shift only the selected token's hue; unscoped → shift the brand hue family ---
        double? warmHue = ContainsAny(text, "warmer", "warm ", "cosier", "cozier") ? 32       // toward orange
                        : ContainsAny(text, "cooler", "cool ", "colder", "icy") ? 210          // toward blue
                        : null;
        if (warmHue.HasValue)
        {
            if (scoped)
                ShiftTargetHue(style, scopedTarget.Value, warmHue.Value, 18);
            else
                ShiftBrandHue(style, warmHue.Value, 18);
        }
    }

    /// <summary>Which accent(s) a colour tweak applies to: any colour words named, else the primary.</summary>
    private static List<ColorTarget> ResolveColorTargets(string text)
    {
        var targets = new List<ColorTarget>();
        foreach (var (words, target) in _colorWords)
            if (ContainsAny(text, words) && !targets.Contains(target))
                targets.Add(target);

        if (targets.Count == 0)
            targets.Add(ColorTarget.Primary);
        return targets;
    }

    private static void AdjustColor(BrandStyle style, ColorTarget target, double satDelta, double lightDelta)
    {
        SetColor(style, target, Nudge(GetColor(style, target), satDelta, lightDelta));

        // keep the tint coherent with the (possibly) shifted primary
        if (target == ColorTarget.Primary && IsHex(style.PrimaryColor))
        {
            var (h, _, _) = HexToHsl(style.PrimaryColor);
            style.PrimaryTintColor = HslToHex(h, 0.40, 0.95);
        }
    }

    /// <summary>Rotate a single token's hue partway toward <paramref name="targetHue"/> (tint follows when primary moves).</summary>
    private static void ShiftTargetHue(BrandStyle style, ColorTarget target, double targetHue, double amount)
    {
        var hex = GetColor(style, target);
        if (!IsHex(hex))
            return;

        var (h, s, l) = HexToHsl(hex);
        var nh = MoveHueToward(h, targetHue, amount);
        SetColor(style, target, HslToHex(nh, s, l));

        if (target == ColorTarget.Primary)
            style.PrimaryTintColor = HslToHex(nh, 0.40, 0.95);
    }

    private static string GetColor(BrandStyle s, ColorTarget t) => t switch
    {
        ColorTarget.Primary => s.PrimaryColor,
        ColorTarget.PrimaryTint => s.PrimaryTintColor,
        ColorTarget.Success => s.SuccessColor,
        ColorTarget.Danger => s.DangerColor,
        ColorTarget.Warning => s.WarningColor,
        ColorTarget.Dark => s.DarkColor,
        _ => null
    };

    private static void SetColor(BrandStyle s, ColorTarget t, string v)
    {
        switch (t)
        {
            case ColorTarget.Primary: s.PrimaryColor = v; break;
            case ColorTarget.PrimaryTint: s.PrimaryTintColor = v; break;
            case ColorTarget.Success: s.SuccessColor = v; break;
            case ColorTarget.Danger: s.DangerColor = v; break;
            case ColorTarget.Warning: s.WarningColor = v; break;
            case ColorTarget.Dark: s.DarkColor = v; break;
        }
    }

    /// <summary>Rotate the brand-hue family (primary, tint, dark) partway toward <paramref name="targetHue"/>.</summary>
    private static void ShiftBrandHue(BrandStyle style, double targetHue, double amount)
    {
        if (!IsHex(style.PrimaryColor))
            return;

        var (h, s, l) = HexToHsl(style.PrimaryColor);
        var nh = MoveHueToward(h, targetHue, amount);
        style.PrimaryColor = HslToHex(nh, s, l);
        style.PrimaryTintColor = HslToHex(nh, 0.40, 0.95);
        if (IsHex(style.DarkColor))
        {
            var (_, ds, dl) = HexToHsl(style.DarkColor);
            style.DarkColor = HslToHex(nh, ds, dl);
        }
    }

    #endregion

    #region Utilities — colour maths

    /// <summary>Build a coherent style around a base hue (primary/tint/dark vary; status colours stay sensible).</summary>
    private static BrandStyle FromHue(double hue) => new()
    {
        PrimaryColor = HslToHex(hue, 0.55, 0.42),
        PrimaryTintColor = HslToHex(hue, 0.40, 0.95),
        DarkColor = HslToHex(hue, 0.28, 0.14),
        SuccessColor = "2e7d32",
        DangerColor = "c62828",
        WarningColor = "ef6c00",
        ButtonRadius = 8
    };

    /// <summary>Deterministic hue [0,360) from arbitrary text, so the same prompt always yields the same palette.</summary>
    private static double HashToHue(string text)
    {
        var hash = 17;
        foreach (var ch in text)
            hash = unchecked(hash * 31 + ch);
        return ((hash % 360) + 360) % 360;
    }

    /// <summary>Adjust a hex colour's saturation and/or lightness by the given deltas (no-op on invalid input).</summary>
    private static string Nudge(string hex, double satDelta, double lightDelta)
    {
        if (!IsHex(hex))
            return hex;
        var (h, s, l) = HexToHsl(hex);
        s = Clamp01(s + satDelta);
        l = Clamp01(l + lightDelta);
        return HslToHex(h, s, l);
    }

    private static bool ContainsAny(string text, params string[] needles)
        => needles.Any(text.Contains);

    private static double Clamp01(double v) => v < 0 ? 0 : v > 1 ? 1 : v;

    /// <summary>Move a hue toward a target by up to <paramref name="amount"/> degrees along the shorter arc.</summary>
    private static double MoveHueToward(double from, double target, double amount)
    {
        var diff = ((target - from + 540) % 360) - 180; // shortest signed distance [-180,180)
        var step = Math.Sign(diff) * Math.Min(Math.Abs(diff), amount);
        return (from + step + 360) % 360;
    }

    private static bool IsHex(string v)
        => !string.IsNullOrWhiteSpace(v) && v.Trim().TrimStart('#').Length == 6
           && v.Trim().TrimStart('#').All(Uri.IsHexDigit);

    /// <summary>6-digit hex (with/without '#') → HSL (h in [0,360), s/l in [0,1]).</summary>
    private static (double H, double S, double L) HexToHsl(string hex)
    {
        var h = hex.Trim().TrimStart('#');
        var r = Convert.ToInt32(h.Substring(0, 2), 16) / 255.0;
        var g = Convert.ToInt32(h.Substring(2, 2), 16) / 255.0;
        var b = Convert.ToInt32(h.Substring(4, 2), 16) / 255.0;

        var max = Math.Max(r, Math.Max(g, b));
        var min = Math.Min(r, Math.Min(g, b));
        var l = (max + min) / 2.0;

        double hue = 0, sat = 0;
        var delta = max - min;
        if (delta > 1e-9)
        {
            sat = l > 0.5 ? delta / (2.0 - max - min) : delta / (max + min);
            if (max == r) hue = (g - b) / delta + (g < b ? 6 : 0);
            else if (max == g) hue = (b - r) / delta + 2;
            else hue = (r - g) / delta + 4;
            hue *= 60;
        }
        return (hue, sat, l);
    }

    /// <summary>HSL (h in [0,360), s/l in [0,1]) → 6-digit hex without '#'.</summary>
    private static string HslToHex(double h, double s, double l)
    {
        var c = (1 - Math.Abs(2 * l - 1)) * s;
        var x = c * (1 - Math.Abs(h / 60.0 % 2 - 1));
        var m = l - c / 2;

        double r, g, b;
        if (h < 60) { r = c; g = x; b = 0; }
        else if (h < 120) { r = x; g = c; b = 0; }
        else if (h < 180) { r = 0; g = c; b = x; }
        else if (h < 240) { r = 0; g = x; b = c; }
        else if (h < 300) { r = x; g = 0; b = c; }
        else { r = c; g = 0; b = x; }

        var red = (int)Math.Round((r + m) * 255);
        var green = (int)Math.Round((g + m) * 255);
        var blue = (int)Math.Round((b + m) * 255);
        return $"{red:x2}{green:x2}{blue:x2}";
    }

    #endregion
}
