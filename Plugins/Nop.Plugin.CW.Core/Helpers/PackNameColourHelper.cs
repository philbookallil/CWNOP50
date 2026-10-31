using System.Text.RegularExpressions;

namespace Nop.Plugin.CW.Core.Helpers;

/// <summary>
/// The colour a packname is shown in: always a six-digit hex colour such as #1A2B3C, because the storefront selector and
/// info strip write it into HTML. Pure: no database.
/// </summary>
public static partial class PackNameColourHelper
{
    [GeneratedRegex("^#[0-9A-Fa-f]{6}$")]
    private static partial Regex HexColourRegex();

    [GeneratedRegex("^#[0-9A-Fa-f]{3}$")]
    private static partial Regex ShortHexColourRegex();

    /// <summary>
    /// The posted or imported colour as a six-digit hex colour
    /// </summary>
    /// <param name="colour">The colour as entered; surrounding spaces are ignored</param>
    /// <returns>
    /// The colour when it is a six-digit hex colour; a three-digit one written out (#F00 is #FF0000); null for anything
    /// else
    /// </returns>
    public static string Normalize(string colour)
    {
        colour = colour?.Trim() ?? string.Empty;

        if (ShortHexColourRegex().IsMatch(colour))
            return $"#{colour[1]}{colour[1]}{colour[2]}{colour[2]}{colour[3]}{colour[3]}";

        return HexColourRegex().IsMatch(colour) ? colour : null;
    }

    /// <summary>
    /// The text colour that reads on a packname's colour: black on a light colour, white on a dark one
    /// </summary>
    /// <remarks>
    /// The brightness weighs red, green and blue as the eye does (0.299, 0.587, 0.114), on a 0–255 scale; above 130 is
    /// light. A colour that is not a hex colour (one saved as free text before the admin saves normalised it) gets black.
    /// </remarks>
    /// <param name="colour">The packname's colour</param>
    /// <returns>"#000" or "#FFF"</returns>
    public static string ForegroundFor(string colour)
    {
        var hex = Normalize(colour);

        if (hex == null)
            return "#000";

        var red = Convert.ToInt32(hex.Substring(1, 2), 16);
        var green = Convert.ToInt32(hex.Substring(3, 2), 16);
        var blue = Convert.ToInt32(hex.Substring(5, 2), 16);
        var brightness = (int)Math.Sqrt(red * red * .299 + green * green * .587 + blue * blue * .114);

        return brightness > 130 ? "#000" : "#FFF";
    }
}