namespace Nop.Plugin.CW.Core.Helpers;

/// <summary>
/// Reads what the admin packname grids post for a bulk action or an export. Pure: no database.
/// </summary>
public static class PackNameBulkHelper
{
    /// <summary>
    /// Reads a comma-separated list of packname identifiers
    /// </summary>
    /// <remarks>
    /// Anything that is not a positive whole number is left out, and an identifier given twice counts once, in the order it
    /// was first given.
    /// </remarks>
    /// <param name="ids">The posted identifiers, for example "12,15,40"</param>
    /// <returns>The identifiers</returns>
    public static List<int> ParseIds(string ids)
    {
        if (string.IsNullOrWhiteSpace(ids))
            return new List<int>();

        return ids
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(value => int.TryParse(value, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var id) ? id : 0)
            .Where(id => id > 0)
            .Distinct()
            .ToList();
    }
}