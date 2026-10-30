using Nop.Plugin.CW.Core.Domain.PackNames.DTOs;

namespace Nop.Plugin.CW.Core.Helpers;

/// <summary>
/// Picks the packnames the storefront selector shows out of the roster a manager may order for. Pure: no database.
/// </summary>
public static class PackNameSelectorHelper
{
    /// <summary>
    /// How many packnames one page of the selector holds, besides the selected packname and those with cart lines
    /// </summary>
    public const int PageSize = 50;

    /// <summary>
    /// Picks one page of a roster for the selector, ordered by name
    /// </summary>
    /// <remarks>
    /// Without a search term the page is the first <paramref name="pageSize"/> packnames by name, plus the selected
    /// packname and every packname with lines in the cart wherever they fall in the roster, so the selector always shows
    /// the packname in context and the cart badges. With a term it is the first <paramref name="pageSize"/> packnames
    /// whose name, or employee number when <paramref name="searchEmployeeNumber"/> is set, contains the term, ignoring case.
    /// </remarks>
    /// <param name="roster">Every packname the manager may order for in the chosen selector entry</param>
    /// <param name="term">What the manager typed; empty or null for the first page</param>
    /// <param name="selectedPackNameId">The packname in context; 0 for none</param>
    /// <param name="cartCounts">The cart quantities by packname identifier</param>
    /// <param name="searchEmployeeNumber">Whether a term also matches employee numbers</param>
    /// <param name="pageSize">How many packnames the page holds</param>
    /// <returns>The page, and whether the roster (or, with a term, the matches) holds packnames the page does not show</returns>
    public static (IList<PackNameDTO> Page, bool HasMore) SelectPage(IReadOnlyCollection<PackNameDTO> roster, string term,
        int selectedPackNameId, IReadOnlyDictionary<int, int> cartCounts, bool searchEmployeeNumber, int pageSize = PageSize)
    {
        ArgumentNullException.ThrowIfNull(roster);

        var ordered = roster
            .OrderBy(packName => packName.Name ?? string.Empty, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(packName => packName.Id)
            .ToList();

        term = term?.Trim();

        if (!string.IsNullOrEmpty(term))
        {
            var matches = ordered
                .Where(packName => Contains(packName.Name, term) || (searchEmployeeNumber && Contains(packName.EmployeeNumber, term)))
                .ToList();

            return (matches.Take(pageSize).ToList(), matches.Count > pageSize);
        }

        var firstPageIds = ordered.Take(pageSize).Select(packName => packName.Id).ToHashSet();

        var page = ordered
            .Where(packName => firstPageIds.Contains(packName.Id)
                || (selectedPackNameId > 0 && packName.Id == selectedPackNameId)
                || (cartCounts != null && cartCounts.TryGetValue(packName.Id, out var quantity) && quantity > 0))
            .ToList();

        return (page, ordered.Count > page.Count);
    }

    private static bool Contains(string value, string term)
    {
        return !string.IsNullOrEmpty(value) && value.Contains(term, StringComparison.CurrentCultureIgnoreCase);
    }
}