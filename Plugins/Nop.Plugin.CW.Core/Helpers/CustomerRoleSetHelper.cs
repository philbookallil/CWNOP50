namespace Nop.Plugin.CW.Core.Helpers;

/// <summary>
/// The customer role set the catalogue sees while a customer orders for a packname, as a rule. Pure — no
/// database, no context of its own.
/// </summary>
/// <remarks>
/// <para>
/// The catalogue follows the packname: its own roles and its department's role. What the customer pays and may
/// do follows the person signed in. nop keys its caches on the role ids, including caches whose content is
/// computed from the signed-in customer (the product price), so the signed-in roles have to be part of the key
/// without taking part in what is visible.
/// </para>
/// <para>
/// They are carried as negative ids. A customer role id is never negative, so a negative id matches no ACL
/// record and no role limit, while two customers who see the same packname roles but hold different roles
/// themselves still produce different sets — and so different cache entries.
/// </para>
/// <para>
/// Both halves are sorted and free of duplicates, so the same inputs in any order give the same set and the
/// same cache key.
/// </para>
/// <para>
/// When the packname's subsidies restrict what it sees, a last id carries that restriction
/// (<see cref="PackNameCatalogGateHelper.RestrictionMarker"/>): far below any negated role id, it too matches nothing,
/// and keeps two packnames with the same roles but different allocations apart in every cache key.
/// </para>
/// </remarks>
public static class CustomerRoleSetHelper
{
    /// <summary>
    /// The role ids the catalogue sees for a packname, followed by the signed-in customer's roles as negative ids and
    /// the restriction marker.
    /// </summary>
    /// <param name="packNameRoleIds">The packname's own role ids</param>
    /// <param name="departmentRoleId">The role id of the packname's department; null or 0 when it has none</param>
    /// <param name="signedInRoleIds">The role ids of the customer signed in</param>
    /// <param name="restrictionMarker">The subsidy restriction's marker; null when nothing restricts the packname</param>
    /// <returns>The visible role ids ascending, then the signed-in role ids negated, by ascending role id, then the restriction marker</returns>
    public static int[] ComposeCatalogRoleIds(IEnumerable<int> packNameRoleIds, int? departmentRoleId, IEnumerable<int> signedInRoleIds,
        int? restrictionMarker = null)
    {
        ArgumentNullException.ThrowIfNull(packNameRoleIds);
        ArgumentNullException.ThrowIfNull(signedInRoleIds);

        var visibleRoleIds = packNameRoleIds.Where(id => id > 0).ToList();

        if (departmentRoleId > 0)
            visibleRoleIds.Add(departmentRoleId.Value);

        var signedInMarkers = signedInRoleIds
            .Where(id => id > 0)
            .Distinct()
            .OrderBy(id => id)
            .Select(id => -id);

        var roleSet = visibleRoleIds
            .Distinct()
            .OrderBy(id => id)
            .Concat(signedInMarkers);

        return restrictionMarker.HasValue
            ? roleSet.Append(restrictionMarker.Value).ToArray()
            : roleSet.ToArray();
    }
}