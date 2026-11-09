using System.Text.RegularExpressions;
using Nop.Plugin.CW.ThemeCore.Domain.BrandDesigner;

namespace Nop.Plugin.CW.ThemeCore.Services.BrandDesigner;

/// <summary>
/// The offline generator: deterministic, no external calls. It answers when no AI connection is set, and always
/// for "Surprise me". A prompt is interpreted as <em>base vibe → modifiers</em>: a curated preset (or the current
/// style) is the base, then plain-English tweaks (intensity, lightness, warmth, button shape) are applied on top.
/// The same prompt against the same base always yields the same result.
/// </summary>
internal class MockBrandGenerator
{
    #region Fields

    private static readonly Random _rng = new();

    // words → the accent token a colour-scoped modifier targets
    private static readonly (string[] Words, BrandField Target)[] _colorWords =
    {
        (new[] { "red", "crimson", "scarlet" }, BrandField.DangerColor),
        (new[] { "green", "emerald", "olive" }, BrandField.SuccessColor),
        (new[] { "blue", "navy", "azure", "teal", "indigo" }, BrandField.PrimaryColor),
        (new[] { "amber", "yellow", "gold", "orange" }, BrandField.WarningColor)
    };

    #endregion

    #region Methods

    /// <summary>Generate a style from a prompt; see <see cref="ICwBrandDesignerService.GenerateAsync"/>.</summary>
    public BrandStyle Generate(string prompt, BrandStyle current, string region = null)
    {
        var text = (prompt ?? string.Empty).ToLowerInvariant().Trim();
        var scopedTarget = BrandRegions.GetColorField(region);

        // exact preset name (used by the vibe chips) → return that preset verbatim, no modifier pass
        var named = BrandPresets.All.FirstOrDefault(p => string.Equals(p.Name, text, StringComparison.OrdinalIgnoreCase));
        if (named != null)
            return BrandStyle.FromPalette(named.Palette, named.ButtonRadius);

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

        return style;
    }

    /// <summary>A fresh, tasteful random style.</summary>
    public BrandStyle Random() => FromHue(_rng.NextDouble() * 360.0);

    #endregion

    #region Utilities — prompt interpretation

    /// <summary>Score presets by how many of their keywords appear in the prompt; return the best (or null).</summary>
    private static BrandPreset BestPresetMatch(string text)
    {
        BrandPreset best = null;
        var bestScore = 0;
        foreach (var preset in BrandPresets.All)
        {
            var score = preset.Keywords.Count(k => ContainsWord(text, k));
            if (score > bestScore)
            {
                bestScore = score;
                best = preset;
            }
        }
        return best;
    }

    /// <summary>Whole words only, so a tweak such as "darker" does not read as the keyword "dark".</summary>
    private static bool ContainsWord(string text, string word)
        => Regex.IsMatch(text, $@"(?<![\p{{L}}\p{{N}}]){Regex.Escape(word)}(?![\p{{L}}\p{{N}}])");

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

        var scopedTarget = BrandRegions.GetColorField(region);
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
        var targets = scoped ? new List<BrandField> { scopedTarget.Value } : ResolveColorTargets(text);

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
    private static List<BrandField> ResolveColorTargets(string text)
    {
        var targets = new List<BrandField>();
        foreach (var (words, target) in _colorWords)
            if (ContainsAny(text, words) && !targets.Contains(target))
                targets.Add(target);

        if (targets.Count == 0)
            targets.Add(BrandField.PrimaryColor);
        return targets;
    }

    private static void AdjustColor(BrandStyle style, BrandField target, double satDelta, double lightDelta)
    {
        SetColor(style, target, Nudge(GetColor(style, target), satDelta, lightDelta));

        // keep the tint coherent with the (possibly) shifted primary
        if (target == BrandField.PrimaryColor && IsHex(style.PrimaryColor))
        {
            var (h, _, _) = HexToHsl(style.PrimaryColor);
            style.PrimaryTintColor = HslToHex(h, 0.40, 0.95);
        }
    }

    /// <summary>Rotate a single token's hue partway toward <paramref name="targetHue"/> (tint follows when primary moves).</summary>
    private static void ShiftTargetHue(BrandStyle style, BrandField target, double targetHue, double amount)
    {
        var hex = GetColor(style, target);
        if (!IsHex(hex))
            return;

        var (h, s, l) = HexToHsl(hex);
        var nh = MoveHueToward(h, targetHue, amount);
        SetColor(style, target, HslToHex(nh, s, l));

        if (target == BrandField.PrimaryColor)
            style.PrimaryTintColor = HslToHex(nh, 0.40, 0.95);
    }

    private static string GetColor(BrandStyle s, BrandField t) => t switch
    {
        BrandField.PrimaryColor => s.PrimaryColor,
        BrandField.PrimaryTintColor => s.PrimaryTintColor,
        BrandField.SuccessColor => s.SuccessColor,
        BrandField.DangerColor => s.DangerColor,
        BrandField.WarningColor => s.WarningColor,
        BrandField.DarkColor => s.DarkColor,
        _ => null
    };

    private static void SetColor(BrandStyle s, BrandField t, string v)
    {
        switch (t)
        {
            case BrandField.PrimaryColor: s.PrimaryColor = v; break;
            case BrandField.PrimaryTintColor: s.PrimaryTintColor = v; break;
            case BrandField.SuccessColor: s.SuccessColor = v; break;
            case BrandField.DangerColor: s.DangerColor = v; break;
            case BrandField.WarningColor: s.WarningColor = v; break;
            case BrandField.DarkColor: s.DarkColor = v; break;
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
