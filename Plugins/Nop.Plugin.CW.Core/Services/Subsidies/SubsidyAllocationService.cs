using Nop.Plugin.CW.Core.Domain.Settings;
using Nop.Plugin.CW.Core.Domain.Subsidies;
using Nop.Plugin.CW.Core.Domain.Subsidies.DTOs;
using Nop.Plugin.CW.Core.Services.Orders;
using Nop.Plugin.CW.Core.Services.Settings;
using Nop.Services.Catalog;
using Nop.Services.Configuration;
using Nop.Services.Tax;

namespace Nop.Plugin.CW.Core.Services.Subsidies;

/// <summary>
/// The single subsidy allocation pass. See ISubsidyAllocationService.
/// </summary>
public class SubsidyAllocationService : ISubsidyAllocationService
{
    #region Fields

    protected readonly ISubsidyCreditService _subsidyCreditService;
    protected readonly IShoppingCartOverrideService _shoppingCartService;
    protected readonly IProductService _productService;
    protected readonly ITaxService _taxService;
    protected readonly ISettingService _settingService;
    protected readonly ICWFeatureService _cwFeatureService;

    #endregion

    #region Ctor

    public SubsidyAllocationService(ISubsidyCreditService subsidyCreditService,
        IShoppingCartOverrideService shoppingCartService,
        IProductService productService,
        ITaxService taxService,
        ISettingService settingService,
        ICWFeatureService cwFeatureService)
    {
        _subsidyCreditService = subsidyCreditService;
        _shoppingCartService = shoppingCartService;
        _productService = productService;
        _taxService = taxService;
        _settingService = settingService;
        _cwFeatureService = cwFeatureService;
    }

    #endregion

    #region Utilities

    /// <summary>
    /// Reduces an incl-tax offer to what is left of the budget. A null budget leaves the offer untouched.
    /// </summary>
    protected static decimal ClampToBudget(decimal inclTax, decimal? budget)
    {
        if (!budget.HasValue || budget.Value >= inclTax)
            return inclTax;

        if (budget.Value <= decimal.Zero || inclTax <= decimal.Zero)
            return decimal.Zero;

        return budget.Value;
    }

    /// <summary>
    /// Allocates subsidy against the goods in the cart, mutating <paramref name="packNameCredits"/> and
    /// appending to <paramref name="items"/>. Returns the money spent on unit-based allocations, which
    /// is the one figure the rows cannot carry (they store units, not money).
    /// </summary>
    /// <remarks>
    /// A replacement item from an approved exchange is allocated like any other line. The consumption
    /// of the item it replaces was reversed in kind when the return was authorised
    /// (ISubsidyCreditService.RestoreSubsidyForReturnedItemAsync, business decisions 7 and 18), so the
    /// restored unit or credit is exactly what this pass finds and spends on the replacement. Returned
    /// items are therefore not skipped here: the skip was the guard against consuming twice, and there is
    /// no longer anything to consume twice.
    /// </remarks>
    protected virtual async Task<decimal> AllocateGoodsAsync(SubsidyAllocationRequest request,
        IList<PackNameCredit> packNameCredits,
        IList<SubsidyOrderItem> items,
        Budget budget)
    {
        var quantityInclTax = decimal.Zero;

        foreach (var cartItem in request.Cart)
        {
            if (budget.IsExhausted)
                break;

            if (cartItem.Quantity <= 0)
                continue;

            var packNameCredit = packNameCredits.FirstOrDefault(x => x.PackNameId == cartItem.PackNameId);

            if (packNameCredit == null || !packNameCredit.SubsidyCredits.Any())
                continue;

            var product = await _productService.GetProductByIdAsync(cartItem.ProductId);

            if (product == null)
                continue;

            var sciSubTotal = (await _shoppingCartService.GetSubTotalAsync(cartItem, true)).subTotal;

            var (sciInclTax, _) = await _taxService.GetProductPriceAsync(product, sciSubTotal, true, request.Customer);

            if (sciInclTax <= decimal.Zero)
                continue;

            var unitPriceInclTax = sciInclTax / cartItem.Quantity;

            // --- unit allocation, capped at what the budget can carry in whole units ---
            // Subsidy runs first, so the budget is normally the whole settleable amount and the cap only
            // bites on a line the budget genuinely cannot carry. It still has to be here: it is what keeps
            // the budget from going negative — which is what made IsExhausted fire spuriously and made
            // the result depend on cart order.
            var offeredQuantity = cartItem.Quantity;

            if (budget.HasValue && unitPriceInclTax > decimal.Zero)
                offeredQuantity = Math.Min(offeredQuantity, (int)Math.Floor(budget.Remaining / unitPriceInclTax));

            var allocatedQuantity = 0;

            if (offeredQuantity > 0)
            {
                var quantityLeft = _subsidyCreditService.ApplyPackNameSubsidyQuantity(packNameCredit, cartItem, offeredQuantity, items);

                allocatedQuantity = offeredQuantity - quantityLeft;
            }

            if (allocatedQuantity > 0)
            {
                var spentInclTax = unitPriceInclTax * allocatedQuantity;

                quantityInclTax += spentInclTax;

                // Units draw the budget down like any other allocation. With the cap above, this can
                // never take it below zero.
                budget.Spend(spentInclTax);
            }

            var remainingQuantity = cartItem.Quantity - allocatedQuantity;

            if (remainingQuantity <= 0 || budget.IsExhausted)
                continue;

            // --- credit allocation: offer the unallocated value, clamped to the budget ---
            var offerInclTax = ClampToBudget(unitPriceInclTax * remainingQuantity, budget.RemainingOrNull);

            if (offerInclTax <= decimal.Zero)
                continue;

            var leftoverInclTax = _subsidyCreditService.ApplyPackNameSubsidyCredit(packNameCredit, cartItem, offerInclTax, items);

            budget.Spend(offerInclTax - leftoverInclTax);
        }

        return quantityInclTax;
    }

    /// <summary>
    /// Allocates subsidy against shipping, mutating <paramref name="packNameCredits"/> and appending to
    /// <paramref name="shippingCredits"/>. Shipping prices arrive pre-computed and pre-rounded.
    /// </summary>
    protected virtual void AllocateShipping(SubsidyAllocationRequest request,
        IList<PackNameCredit> packNameCredits,
        IList<SubsidyOrderShipping> shippingCredits,
        Budget budget)
    {
        foreach (var shippingPrice in request.ShippingPrices)
        {
            if (budget.IsExhausted)
                break;

            if (shippingPrice.PackNameIds == null || shippingPrice.PackNameIds.Count == 0)
                continue;

            // Null price = address not resolvable; zero price = free shipping. Neither is subsidisable.
            if (!shippingPrice.PriceInclTaxt.HasValue || shippingPrice.PriceInclTaxt.Value <= decimal.Zero)
                continue;

            var inclPerPackName = shippingPrice.PriceInclTaxt.Value / shippingPrice.PackNameIds.Count;

            foreach (var packNameId in shippingPrice.PackNameIds)
            {
                if (budget.IsExhausted)
                    break;

                var offerInclTax = ClampToBudget(inclPerPackName, budget.RemainingOrNull);

                if (offerInclTax <= decimal.Zero)
                    continue;

                var remainingInclTax = _subsidyCreditService.GetPackNameShippingRemaining(
                    packNameId, offerInclTax, packNameCredits, shippingCredits);

                budget.Spend(offerInclTax - remainingInclTax);
            }
        }
    }

    /// <summary>
    /// A running ceiling on how much money this pass may still allocate. Null total means uncapped, in
    /// which case every Spend is a no-op and IsExhausted is always false.
    /// </summary>
    protected sealed class Budget
    {
        private decimal _remaining;

        public Budget(decimal? total)
        {
            HasValue = total.HasValue;
            _remaining = total ?? decimal.Zero;
        }

        public bool HasValue { get; }

        public decimal Remaining => _remaining;

        public decimal? RemainingOrNull => HasValue ? _remaining : null;

        public bool IsExhausted => HasValue && _remaining <= decimal.Zero;

        public void Spend(decimal amount)
        {
            if (!HasValue || amount <= decimal.Zero)
                return;

            _remaining -= amount;
        }
    }

    #endregion

    #region Methods

    public virtual async Task<SubsidyAllocation> AllocateAsync(SubsidyAllocationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Customer);

        if (request.Cart == null || request.Cart.Count == 0)
            return SubsidyAllocation.Empty;

        if (request.BudgetInclTax is <= decimal.Zero)
            return SubsidyAllocation.Empty;

        if (!await _cwFeatureService.IsEnabledAsync(CWFeature.Subsidy, request.StoreId))
            return SubsidyAllocation.Empty;

        var packNameIds = request.Cart.Select(x => x.PackNameId).Distinct().ToList();

        // GetPackNamesSubsidyCreditAsync returns deep clones — safe to mutate as we allocate.
        var packNameCredits = await _subsidyCreditService.GetPackNamesSubsidyCreditAsync(packNameIds);

        var items = new List<SubsidyOrderItem>();
        var shippingCredits = new List<SubsidyOrderShipping>();
        var budget = new Budget(request.BudgetInclTax);

        var quantityInclTax = await AllocateGoodsAsync(request, packNameCredits, items, budget);

        var storeSettings = await _settingService.LoadSettingAsync<MultiStoreSettings>(request.StoreId);

        if (storeSettings.IncludeShippingOnSubsidy)
            AllocateShipping(request, packNameCredits, shippingCredits, budget);

        // Money allocated as credit is exact on the rows; money allocated as units is not, because the
        // rows carry units. Sum each from where it is exact.
        var creditInclTax = items.Sum(x => x.Credit + x.CreditTopup + x.ProductGroupCredit + x.ProductGroupCreditTopup);

        return new SubsidyAllocation
        {
            Items = items,
            ShippingCredits = shippingCredits,
            StockInclTax = quantityInclTax + creditInclTax,
            ShippingInclTax = shippingCredits.Sum(x => x.Credit + x.CreditTopup),
        };
    }

    #endregion
}