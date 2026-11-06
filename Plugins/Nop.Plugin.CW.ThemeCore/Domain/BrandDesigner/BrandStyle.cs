namespace Nop.Plugin.CW.ThemeCore.Domain.BrandDesigner;

/// <summary>
/// A generated brand "style": the six accent colours plus a small set of non-colour knobs
/// (currently just the button corner radius). Colour values are hex WITHOUT a leading '#'.
/// This is what the Guided Brand Designer returns and what the admin preview/fields consume.
/// </summary>
public class BrandStyle
{
    public string PrimaryColor { get; set; }
    public string PrimaryTintColor { get; set; }
    public string SuccessColor { get; set; }
    public string DangerColor { get; set; }
    public string WarningColor { get; set; }
    public string DarkColor { get; set; }

    /// <summary>
    /// Button corner radius in px — drives the <c>--cw-btn-radius</c> token.
    /// ~2 = square, ~8 = rounded, 999 = pill. Default 8 (rounded).
    /// </summary>
    public int ButtonRadius { get; set; } = 8;

    /// <summary>Build a style from a preset palette, keeping the preset's own button radius.</summary>
    public static BrandStyle FromPalette(BrandPalette p, int buttonRadius = 8) => new()
    {
        PrimaryColor = p.PrimaryColor,
        PrimaryTintColor = p.PrimaryTintColor,
        SuccessColor = p.SuccessColor,
        DangerColor = p.DangerColor,
        WarningColor = p.WarningColor,
        DarkColor = p.DarkColor,
        ButtonRadius = buttonRadius
    };

    /// <summary>A shallow copy — safe because every member is a value/immutable string.</summary>
    public BrandStyle Clone() => (BrandStyle)MemberwiseClone();
}
