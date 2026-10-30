using Nop.Plugin.CW.Core.Domain.Subsidies.DTOs;

namespace Nop.Plugin.CW.Core.Helpers;

/// <summary>
/// The allocated-quantity cap on a packname's cart, as a rule. Pure — no database, no cart of its own.
/// </summary>
/// <remarks>
/// <para>
/// Only the subsidies whose product groups contain the product decide. A subsidy that does not contain it
/// neither caps it nor lifts the cap on it.
/// </para>
/// <list type="bullet">
/// <item>No subsidy contains the product: no cap — the product is not allocated.</item>
/// <item>A subsidy containing it does not restrict to the allocated quantity: no cap.</item>
/// <item>A subsidy containing it, or a group containing it, still has credit available: no cap — the units
/// beyond the allocated quantity are paid from that credit. The cap returns once the credit is spent.</item>
/// <item>Otherwise the cap is the units still allocated in the groups containing the product, less what the
/// rest of the packname's cart already takes from those groups.</item>
/// </list>
/// <para>
/// <c>RestrictToAllocatedQty</c> arrives already manager-aware from the credit view: a subsidy restricts a
/// manager only when it includes managers.
/// </para>
/// </remarks>
public static class SubsidyQuantityCapHelper
{
    /// <summary>
    /// The units of a product the packname's cart may still take, or null when no cap applies.
    /// </summary>
    /// <param name="packNameCredit">The packname's credit for its subsidies in ordering</param>
    /// <param name="productId">The product being added or changed</param>
    /// <param name="cartUnits">Units the rest of the packname's cart already takes from each group, keyed by subsidy and product group</param>
    /// <returns>The units left, never negative; null when no cap applies</returns>
    public static int? GetQuantityLeft(PackNameCredit packNameCredit, int productId, IReadOnlyDictionary<(int SubsidyId, int ProductGroupId), int> cartUnits)
    {
        ArgumentNullException.ThrowIfNull(packNameCredit);
        ArgumentNullException.ThrowIfNull(cartUnits);

        var matching = packNameCredit.SubsidyCredits
            .SelectMany(subsidy => subsidy.ProductGroupCredits
                .Where(group => group.ProductIds.Contains(productId))
                .Select(group => (Subsidy: subsidy, Group: group)))
            .ToList();

        if (matching.Count == 0)
            return null;

        if (matching.Any(match => !match.Subsidy.RestrictToAllocatedQty))
            return null;

        if (matching.Any(match => match.Subsidy.AvailableCredit + match.Subsidy.AvailableCreditTopup > decimal.Zero
                               || match.Group.AvailableCredit + match.Group.AvailableCreditTopup > decimal.Zero))
            return null;

        var allocated = matching.Sum(match => match.Group.AvailableQuantity + match.Group.AvailableQuantityTopup);
        var inCart = matching.Sum(match => cartUnits.GetValueOrDefault((match.Subsidy.SubsidyId, match.Group.ProductGroupId)));

        return Math.Max(allocated - inCart, 0);
    }
}