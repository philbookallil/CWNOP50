namespace Nop.Plugin.CW.Core.Helpers;

/// <summary>
/// Who a packname order line is for, as printed under the product on the invoice and the packing slip.
/// </summary>
public static class PackNameOrderItemHelper
{
    /// <summary>
    /// The separator between the packname, the department and the cost centre.
    /// </summary>
    public const string FOR_SEPARATOR = " · ";

    /// <summary>
    /// Joins the packname, department and cost centre stored on an order line, leaving out the empty ones.
    /// </summary>
    /// <param name="packName">Packname name stored at placement</param>
    /// <param name="department">Department name stored at placement</param>
    /// <param name="costCenter">Cost centre name stored at placement</param>
    /// <returns>The joined text, or null when all three are empty</returns>
    public static string FormatFor(string packName, string department, string costCenter)
    {
        var parts = new[] { packName, department, costCenter }
            .Where(part => !string.IsNullOrWhiteSpace(part))
            .Select(part => part.Trim())
            .ToList();

        return parts.Count == 0 ? null : string.Join(FOR_SEPARATOR, parts);
    }
}