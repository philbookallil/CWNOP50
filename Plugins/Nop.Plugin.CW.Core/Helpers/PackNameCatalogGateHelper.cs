using Nop.Plugin.CW.Core.Domain.Exchanges;

namespace Nop.Plugin.CW.Core.Helpers;

/// <summary>
/// What a packname's subsidies let it see in the catalogue, as a rule. Pure — no database, no context of its own.
/// </summary>
/// <remarks>
/// <para>
/// The rule, in order:
/// </para>
/// <list type="bullet">
/// <item>An exchange replacement of the <b>same</b> product (Exact or Same Style) is never restricted: the customer
/// already owns it.</item>
/// <item>A packname whose ordering period is closed sees no product — except when choosing or receiving a Different
/// Style replacement, which a closed period never blocks.</item>
/// <item>Otherwise the allocated products when its subsidies restrict it to them, and everything when they do
/// not.</item>
/// </list>
/// <para>
/// The answer is part of nop's cache keys through the role set (<see cref="RestrictionMarker"/>), so two packnames
/// with the same roles and different allocations never share a cached category list, menu or price.
/// </para>
/// </remarks>
public static class PackNameCatalogGateHelper
{
    /// <summary>
    /// Product ids a packname may see, or null when nothing restricts it.
    /// </summary>
    /// <param name="canOrder">Whether the packname's ordering period is open</param>
    /// <param name="allocatedProductIds">The allocated product ids when its subsidies restrict it to them; null when they do not</param>
    /// <param name="replacement">The exchange type when the check is for an exchange replacement; null when it is ordering</param>
    /// <returns>The visible product ids; an empty list when it may see none; null when unrestricted</returns>
    public static IList<int>? VisibleProductIds(bool canOrder, IList<int>? allocatedProductIds, ExchangeType? replacement)
    {
        if (replacement is ExchangeType.Exact or ExchangeType.SameStyle)
            return null;

        if (replacement == null && !canOrder)
            return [];

        return allocatedProductIds;
    }

    /// <summary>
    /// A deterministic fingerprint of a product id set: the same set in any order gives the same value, across
    /// processes and servers.
    /// </summary>
    /// <param name="productIds">Product ids</param>
    /// <returns>The fingerprint (FNV-1a over the sorted, distinct ids)</returns>
    public static long Fingerprint(IEnumerable<int> productIds)
    {
        ArgumentNullException.ThrowIfNull(productIds);

        unchecked
        {
            var hash = (long)14695981039346656037UL;

            foreach (var id in productIds.Distinct().OrderBy(id => id))
            {
                hash ^= id;
                hash *= 1099511628211L;
            }

            return hash;
        }
    }

    /// <summary>
    /// The role id that carries a restriction into nop's cache keys, or null when nothing restricts.
    /// </summary>
    /// <remarks>
    /// Far below any negated role id (customer role ids never reach a billion), so it matches no ACL record and
    /// never collides with the signed-in markers of <see cref="CustomerRoleSetHelper"/>.
    /// </remarks>
    /// <param name="visibleProductIds">The visible product ids; null when unrestricted</param>
    /// <returns>The marker, between <see cref="int.MinValue"/> and <see cref="int.MinValue"/> + 999,999,999; null when unrestricted</returns>
    public static int? RestrictionMarker(IEnumerable<int>? visibleProductIds)
    {
        if (visibleProductIds == null)
            return null;

        var bucket = (int)((ulong)Fingerprint(visibleProductIds) % 1_000_000_000UL);

        return int.MinValue + bucket;
    }

    /// <summary>
    /// The visible products together with their grouped-product relatives.
    /// </summary>
    /// <remarks>
    /// A grouped product is shown on its own page with its associated products, and only the associated ones go in
    /// the cart. A product group may hold either: allocating the parent has to let its associated products be added,
    /// and allocating an associated product has to let its parent - the only page that offers it - be found and
    /// opened.
    /// </remarks>
    /// <param name="productIds">The visible product ids</param>
    /// <param name="groupedRelations">Each associated product with its parent: the rows whose id or parent is in <paramref name="productIds"/></param>
    /// <returns>The visible ids plus the associated products of a visible parent and the parent of a visible associated product</returns>
    public static IList<int> WithGroupedRelatives(IList<int> productIds, IEnumerable<(int ProductId, int ParentGroupedProductId)> groupedRelations)
    {
        ArgumentNullException.ThrowIfNull(productIds);
        ArgumentNullException.ThrowIfNull(groupedRelations);

        var visible = productIds.ToHashSet();
        var result = new HashSet<int>(visible);

        foreach (var (productId, parentId) in groupedRelations.Where(relation => relation.ParentGroupedProductId > 0))
        {
            if (visible.Contains(parentId))
                result.Add(productId);

            if (visible.Contains(productId))
                result.Add(parentId);
        }

        return result.OrderBy(id => id).ToList();
    }

    /// <summary>
    /// The categories to show: those holding a visible product, and every ancestor of them.
    /// </summary>
    /// <param name="categoryIdsWithVisibleProducts">Categories mapped to at least one visible product</param>
    /// <param name="parentCategoryIdById">Each category's parent (0 for a root)</param>
    /// <returns>The category ids to show</returns>
    public static ISet<int> WithAncestors(IEnumerable<int> categoryIdsWithVisibleProducts, IReadOnlyDictionary<int, int> parentCategoryIdById)
    {
        ArgumentNullException.ThrowIfNull(categoryIdsWithVisibleProducts);
        ArgumentNullException.ThrowIfNull(parentCategoryIdById);

        var visible = new HashSet<int>();

        foreach (var categoryId in categoryIdsWithVisibleProducts)
        {
            var current = categoryId;

            //Add returns false for a category already walked, which also stops a broken parent loop.
            while (current > 0 && visible.Add(current))
                current = parentCategoryIdById.TryGetValue(current, out var parentId) ? parentId : 0;
        }

        return visible;
    }
}