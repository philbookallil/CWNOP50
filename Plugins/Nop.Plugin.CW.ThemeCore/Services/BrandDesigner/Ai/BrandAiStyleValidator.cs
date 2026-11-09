using System.Text.RegularExpressions;
using Nop.Plugin.CW.ThemeCore.Domain.BrandDesigner;

namespace Nop.Plugin.CW.ThemeCore.Services.BrandDesigner.Ai;

/// <summary>
/// The model's answer is untrusted, whatever the schema said: a schema fixes the shape, never the values. Only a
/// six-digit hex colour and a radius in range reach the form, and with a selected element only that element's fields.
/// </summary>
internal static partial class BrandAiStyleValidator
{
    #region Constants

    private const int MAX_RADIUS = 999;

    private static readonly BrandField[] _colours =
    [
        BrandField.PrimaryColor,
        BrandField.PrimaryTintColor,
        BrandField.SuccessColor,
        BrandField.DangerColor,
        BrandField.WarningColor,
        BrandField.DarkColor
    ];

    #endregion

    #region Utilities

    [GeneratedRegex("^[0-9a-fA-F]{6}$")]
    private static partial Regex HexColour();

    private static string GetColour(BrandStyle style, BrandField field) => field switch
    {
        BrandField.PrimaryColor => style.PrimaryColor,
        BrandField.PrimaryTintColor => style.PrimaryTintColor,
        BrandField.SuccessColor => style.SuccessColor,
        BrandField.DangerColor => style.DangerColor,
        BrandField.WarningColor => style.WarningColor,
        BrandField.DarkColor => style.DarkColor,
        _ => null
    };

    private static void SetColour(BrandStyle style, BrandField field, string value)
    {
        switch (field)
        {
            case BrandField.PrimaryColor: style.PrimaryColor = value; break;
            case BrandField.PrimaryTintColor: style.PrimaryTintColor = value; break;
            case BrandField.SuccessColor: style.SuccessColor = value; break;
            case BrandField.DangerColor: style.DangerColor = value; break;
            case BrandField.WarningColor: style.WarningColor = value; break;
            case BrandField.DarkColor: style.DarkColor = value; break;
        }
    }

    #endregion

    #region Methods

    /// <summary>
    /// The style to apply: the current style with the answer's valid fields on top.
    /// </summary>
    /// <param name="answer">The model's style</param>
    /// <param name="current">The style in the form</param>
    /// <param name="region">The selected element, if any; only its fields are taken</param>
    /// <param name="invalid">The colours refused, which keep their current value</param>
    /// <returns>The style, or null when no colour of the answer was usable</returns>
    public static BrandStyle Apply(BrandStyle answer, BrandStyle current, string region, out IList<BrandField> invalid)
    {
        invalid = new List<BrandField>();
        var style = current?.Clone() ?? new BrandStyle();
        var allowed = BrandRegions.GetFields(region);

        var usable = 0;
        foreach (var field in _colours)
        {
            var value = GetColour(answer, field)?.Trim().TrimStart('#');
            if (value is null || !HexColour().IsMatch(value))
            {
                invalid.Add(field);
                continue;
            }

            usable++;
            if (allowed is null || allowed.Contains(field))
                SetColour(style, field, value.ToLowerInvariant());
        }

        if (usable == 0)
            return null;

        if (allowed is null || allowed.Contains(BrandField.ButtonRadius))
            style.ButtonRadius = Math.Clamp(answer.ButtonRadius, 0, MAX_RADIUS);

        return style;
    }

    #endregion
}
