using Nop.Core.Domain.Customers;
using Nop.Plugin.CW.Core.Domain.Orders;
using Nop.Plugin.CW.Core.Domain.Orders.Views;

namespace Nop.Plugin.CW.Core.Services.Subsidies;

/// <summary>
/// One subsidy allocation pass over a cart.
/// </summary>
public record SubsidyAllocationRequest
{
    public required Customer Customer { get; init; }

    public required int StoreId { get; init; }

    /// <summary>The packname cart to allocate over.</summary>
    public IList<PackNameShoppingCartItem> Cart { get; init; } = [];

    /// <summary>
    /// Per-address shipping prices, already computed and rounded by the caller. Entries with a null or
    /// non-positive <c>PriceInclTaxt</c> are skipped — that covers free shipping and unresolved
    /// addresses without this service needing to re-derive either.
    /// </summary>
    public IList<PackNameShippingPrice> ShippingPrices { get; init; } = [];

    /// <summary>
    /// Ceiling on the total money this pass may allocate, incl tax — the amount still unsettled when the
    /// pipeline reaches subsidy. Subsidy runs first, so on the ordinary path this is the
    /// whole settleable amount. <c>null</c> means uncapped, which reproduces the pre-phase-2 behaviour
    /// exactly.
    /// </summary>
    public decimal? BudgetInclTax { get; init; }
}