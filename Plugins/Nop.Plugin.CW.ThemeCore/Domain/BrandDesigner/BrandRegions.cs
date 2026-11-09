namespace Nop.Plugin.CW.ThemeCore.Domain.BrandDesigner;

/// <summary>
/// Which fields of a <see cref="BrandStyle"/> each preview region (<c>data-cw-style</c>) may change. The single
/// source of that map: the offline generator and the AI answer's region filter both read it, so a tweak on a
/// selected element cannot reach another element's colours.
/// </summary>
public static class BrandRegions
{
    #region Fields

    private static readonly IReadOnlyDictionary<string, IReadOnlyList<BrandField>> _regions =
        new Dictionary<string, IReadOnlyList<BrandField>>(StringComparer.OrdinalIgnoreCase)
        {
            ["header"] = [BrandField.PrimaryColor],
            ["nav"] = [BrandField.PrimaryColor],
            ["link"] = [BrandField.PrimaryColor],
            ["button"] = [BrandField.PrimaryColor, BrandField.ButtonRadius],
            ["hero"] = [BrandField.PrimaryTintColor],
            ["tint"] = [BrandField.PrimaryTintColor],
            ["danger"] = [BrandField.DangerColor],
            ["success"] = [BrandField.SuccessColor],
            ["warning"] = [BrandField.WarningColor],
            ["dark"] = [BrandField.DarkColor],
            ["footer"] = [BrandField.DarkColor]
        };

    #endregion

    #region Methods

    /// <summary>The fields a region may change, or null for an empty or unknown region.</summary>
    public static IReadOnlyList<BrandField> GetFields(string region)
    {
        return !string.IsNullOrWhiteSpace(region) && _regions.TryGetValue(region.Trim(), out var fields) ? fields : null;
    }

    /// <summary>The colour a scoped tweak targets, or null for an empty or unknown region.</summary>
    public static BrandField? GetColorField(string region) => GetFields(region)?[0];

    #endregion
}
