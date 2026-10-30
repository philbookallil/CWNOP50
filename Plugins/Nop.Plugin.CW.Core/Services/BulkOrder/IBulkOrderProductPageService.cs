using Nop.Plugin.CW.Core.Models.BulkOrder;

namespace Nop.Plugin.CW.Core.Services.BulkOrder;

/// <summary>
/// Decides whether the bulk grid replaces the stock controls on one product's details page,
/// and builds it when it does.
/// </summary>
/// <remarks>
/// One method rather than a predicate plus a separate build, on purpose. The last condition of the gate is
/// "can this product be expressed as a grid at all", and the only honest answer comes from building it —
/// <c>BulkOrderGridBuilder</c> already reports Grouped, GiftCard, Rental, CustomerEntersPrice, Recurring,
/// BuyDisabled and RequiredTextAttribute through <c>UnsupportedReason</c>. A predicate would either
/// duplicate that logic or build the grid and throw it away, and the page needs the built grid moments
/// later anyway.
///
/// Takes an id, not a Product: <c>BulkOrderService.GetVisibleProductByIdAsync</c> is a query rather than an
/// entity-cache read, and <c>PrepareGridModelAsync</c> already performs it. Taking the entity would make
/// every caller pay for it twice.
/// </remarks>
public interface IBulkOrderProductPageService
{
    /// <summary>
    /// The grid for this product's page, or <c>null</c> when the page must be left exactly as nop rendered
    /// it. A null return is the instruction to suppress nothing.
    /// </summary>
    Task<BulkOrderGridModel> TryPrepareAsync(int productId);
}