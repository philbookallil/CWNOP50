using Nop.Core.Domain.Customers;
using Nop.Core.Domain.Discounts;
using Nop.Core.Domain.Orders;
using Nop.Plugin.CW.Core.Domain.Orders;
using Nop.Plugin.CW.Core.Domain.Orders.Views;
using Nop.Plugin.CW.Core.Domain.Subsidies.DTOs;
using Nop.Plugin.CW.Core.Services.Settlement;
using Nop.Services.Orders;

namespace Nop.Plugin.CW.Core.Services.Orders;

public interface IOrderTotalCalculationOverrideService : IOrderTotalCalculationService
{
    /// <summary>
    /// Gets shopping cart shipping total
    /// </summary>
    /// <param name="packNameCart">Cart</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the shipping total
    /// </returns>
    Task<IList<PackNameShippingPrice>> GetShoppingCartShippingTotalAsync(IList<PackNameShoppingCartItem> packNameCart);

    /// <summary>
    /// Gets shopping cart shipping total
    /// </summary>
    /// <param name="packNameCart">Cart</param>
    /// <param name="includingTax">A value indicating whether calculated price should include tax</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the shipping total. Applied tax rate. Applied discounts
    /// </returns>
    Task<(List<PackNameShippingPrice> shippingTotal, decimal taxRate, List<Discount> appliedDiscounts)> GetShoppingCartShippingTotalAsync(IList<PackNameShoppingCartItem> packNameCart, bool includingTax);

    /// <summary>
    /// Gets shopping cart shipping total
    /// </summary>
    /// <param name="packNameCart">Cart</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the shipping total. Applied tax rate. Applied discounts
    /// </returns>
    Task<(List<PackNameShippingPrice>, decimal taxRate, List<Discount> appliedDiscounts)> GetShoppingCartShippingTotalsAsync(IList<PackNameShoppingCartItem> packNameCart);

    /// <summary>
    /// Quotes the settlement pipeline against a cart: what credit notes and subsidy would cover, and
    /// what the customer would still owe. Persists nothing.
    /// </summary>
    /// <param name="cart">The cart being settled</param>
    /// <param name="settleableAmount">
    /// The amount to settle, incl tax. Null means "compute the pre-settlement cart total first" —
    /// which is what every display caller wants.
    /// </param>
    Task<SettlementPlan> QuoteCartSettlementAsync(IList<ShoppingCartItem> cart, decimal? settleableAmount = null);

    /// <summary>
    /// Runs one subsidy allocation pass over a packname cart and returns the full breakdown.
    /// </summary>
    /// <param name="budgetInclTax">
    /// Ceiling on what subsidy may allocate, incl tax — normally the amount credit notes left
    /// unsettled. Null means uncapped.
    /// </param>
    Task<SubsidyAllocation> AllocateCartSubsidyAsync(IList<PackNameShoppingCartItem> cart, Customer customer, int storeId, decimal? budgetInclTax = null);

    /// <summary>
    /// The subsidy allocation for a cart exactly as the settlement pipeline computed it — the same quote
    /// the totals used, under the same budget. Anything that displays subsidy usage must go through this,
    /// or it will disagree with what placement actually writes.
    /// </summary>
    /// <remarks>
    /// Before shipping can be rated (the packname checkout has not yet given every packname an address and a
    /// method), there is no settlement budget: the goods' subsidy is then allocated without the order-total cap,
    /// and shipping has no share yet.
    /// </remarks>
    Task<SubsidyAllocation> GetCartSubsidyBreakdownAsync(IList<PackNameShoppingCartItem> packNameCart);

    /// <summary>
    /// Gets the order subtotal discount a cart actually applies. For a packname cart it comes off what subsidy and
    /// store credit left the customer to pay, so it is never more than that; for any other cart it is nop's.
    /// </summary>
    /// <param name="cart">Cart</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the discount applied, including and excluding tax, and the discounts applied (empty when
    /// the discount takes nothing off)
    /// </returns>
    Task<(decimal discountAmountInclTax, decimal discountAmountExclTax, List<Discount> appliedDiscounts)> GetAppliedSubTotalDiscountAsync(IList<ShoppingCartItem> cart);
}