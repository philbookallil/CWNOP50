using Nop.Core;
using Nop.Core.Caching;
using Nop.Core.Domain.Catalog;
using Nop.Core.Domain.Customers;
using Nop.Core.Domain.Discounts;
using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Shipping;
using Nop.Core.Domain.Tax;
using Nop.Plugin.CW.Core.Domain.Orders;
using Nop.Plugin.CW.Core.Domain.Orders.Views;
using Nop.Plugin.CW.Core.Domain.Settings;
using Nop.Plugin.CW.Core.Domain.Subsidies.DTOs;
using Nop.Plugin.CW.Core.Helpers;
using Nop.Plugin.CW.Core.Services.Customers;
using Nop.Plugin.CW.Core.Services.Exchanges;
using Nop.Plugin.CW.Core.Services.PackNames;
using Nop.Plugin.CW.Core.Services.Settlement;
using Nop.Plugin.CW.Core.Services.Subsidies;
using Nop.Services.Attributes;
using Nop.Services.Catalog;
using Nop.Services.Common;
using Nop.Services.Configuration;
using Nop.Services.Discounts;
using Nop.Services.Logging;
using Nop.Services.Orders;
using Nop.Services.Payments;
using Nop.Services.Shipping;
using Nop.Services.Tax;

namespace Nop.Plugin.CW.Core.Services.Orders;

public class OrderTotalCalculationOverrideService : OrderTotalCalculationService, IOrderTotalCalculationOverrideService
{
    #region Fields

    protected readonly IExchangeService _exchangeService;
    protected readonly ISettingService _settingService;
    protected new readonly IShoppingCartOverrideService _shoppingCartService;
    protected readonly IPackNameContext _packNameContext;
    protected new readonly ICustomerOverrideService _customerService;
    protected readonly IPackNameShippingAssignmentService _shippingAssignmentService;
    protected readonly ISettlementPipeline _settlementPipeline;
    protected readonly ISubsidyAllocationService _subsidyAllocationService;
    protected readonly IShortTermCacheManager _shortTermCacheManager;
    protected readonly ILogger _logger;

    #endregion

    #region Ctor

    public OrderTotalCalculationOverrideService(IExchangeService exchangeService,
        CatalogSettings catalogSettings,
        IAddressService addressService,
        IAttributeParser<CheckoutAttribute,
        CheckoutAttributeValue> checkoutAttributeParser,
        ICustomerOverrideService customerService,
        IDiscountService discountService,
        IGenericAttributeService genericAttributeService,
        IGiftCardService giftCardService,
        IOrderService orderService,
        IPaymentService paymentService,
        IPriceCalculationService priceCalculationService,
        IProductService productService,
        IRewardPointService rewardPointService,
        IShippingPluginManager shippingPluginManager,
        IShippingService shippingService,
        IShoppingCartOverrideService shoppingCartService,
        IStoreContext storeContext,
        ITaxService taxService,
        IWorkContext workContext,
        RewardPointsSettings rewardPointsSettings,
        ShippingSettings shippingSettings,
        ShoppingCartSettings shoppingCartSettings,
        TaxSettings taxSettings,
        ISettingService settingService,
        IPackNameContext packNameContext,
        IPackNameShippingAssignmentService shippingAssignmentService,
        ISettlementPipeline settlementPipeline,
        ISubsidyAllocationService subsidyAllocationService,
        IShortTermCacheManager shortTermCacheManager,
        ILogger logger) : base(catalogSettings,
            addressService,
            checkoutAttributeParser,
            customerService,
            discountService,
            genericAttributeService,
            giftCardService,
            orderService,
            paymentService,
            priceCalculationService,
            productService,
            rewardPointService,
            shippingPluginManager,
            shippingService,
            shoppingCartService,
            storeContext,
            taxService,
            workContext,
            rewardPointsSettings,
            shippingSettings,
            shoppingCartSettings,
            taxSettings)
    {
        _exchangeService = exchangeService;
        _settingService = settingService;
        _shoppingCartService = shoppingCartService;
        _packNameContext = packNameContext;
        _customerService = customerService;
        _shippingAssignmentService = shippingAssignmentService;
        _settlementPipeline = settlementPipeline;
        _subsidyAllocationService = subsidyAllocationService;
        _shortTermCacheManager = shortTermCacheManager;
        _logger = logger;
    }

    #endregion

    #region Utilities

    /// <summary>
    /// Whether a cart is priced as the current customer's packname cart: a packname is selected and the cart is
    /// the customer's saved cart. A cart built in memory for a return order carries no ids; it is priced as it
    /// stands, whatever packname the person approving the return has selected.
    /// </summary>
    /// <param name="cart">Cart</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains true when the cart is priced through the packname cart
    /// </returns>
    protected virtual async Task<bool> IsPackNameCartAsync(IList<ShoppingCartItem> cart)
    {
        if (cart?.Count > 0 && cart.All(item => item.Id == 0))
            return false;

        return await _packNameContext.GetCurrentPackNameAsync(await _workContext.GetCurrentCustomerAsync()) != null;
    }

    /// <summary>
    /// Builds the settlement context for a cart. Shipping prices are resolved here, once, and carried
    /// on the context so that no settlement source has to reach back into this service.
    /// </summary>
    protected virtual async Task<SettlementContext> BuildCartSettlementContextAsync(Customer customer, int storeId, IList<ShoppingCartItem> cart = null)
    {
        //A synthetic cart is not the customer's cart. BuildProductsListForReturnOrderAsync builds items in
        //memory that are never saved, so they carry no Id, while a real cart row always has one — the same
        //test GetShoppingCartShippingTotalAsync uses. Reading the customer's SAVED packname cart here would
        //allocate subsidy against whatever else they happen to have in it and net that off the return
        //order's total, where it writes no rows and simply vanishes. The replacement's own settlement is
        //quoted by OrderProcessingOverrideService.QuoteReturnOrderSettlementAsync, which knows the packname.
        var isDetachedCart = cart?.Count > 0 && cart.All(item => item.Id == 0);

        var packNameCart = isDetachedCart
            ? []
            : await _shoppingCartService.GetPackNameShoppingCartItemsAsync(customer, 0, storeId);

        var shippingPrices = packNameCart.Count == 0
            ? []
            : (await GetShoppingCartShippingTotalAsync(packNameCart, false)).shippingTotal;

        return new SettlementContext
        {
            Customer = customer,
            StoreId = storeId,
            PackNameCart = packNameCart,
            ShippingPrices = shippingPrices,
        };
    }

    protected virtual async Task<(List<PackNameShippingPrice> shippingTotals, decimal taxRate, List<Discount> appliedDiscounts)> GetShippingTotalsByPackNames(IList<PackNameShoppingCartItem> packNameCart, MultiStoreSettings storeSettings, bool includingTax, bool returnOnNull)
    {
        var cart = packNameCart.Cast<ShoppingCartItem>().ToList();
        var customer = await _customerService.GetShoppingCartCustomerAsync(cart);
        var store = await _storeContext.GetCurrentStoreAsync();

        var shippingTotals = new List<PackNameShippingPrice>();
        var taxRate = decimal.Zero;
        var appliedDiscounts = new List<Discount>();

        var assignments = await _shippingAssignmentService.LoadAsync(customer, store.Id);

        var packNameIds = packNameCart.Select(item => item.PackNameId).Distinct().ToList();

        //There could be packnames with a different shipping address from the branch or same address from other branches
        //We need to check each packname and then group them by the address identifier to make sure we group them by address even if they are from different branches.
        foreach (var packNameId in packNameIds)
        {
            var branchId = packNameCart.FirstOrDefault(x => x.PackNameId == packNameId)?.BranchId ?? 0;

            var shippingOption = assignments.GetSelectedOption(packNameId);
            var packNameAddressId = assignments.GetAddressId(packNameId);

            if (!packNameAddressId.HasValue || packNameAddressId.Value == 0 || !await _shoppingCartService.IsAddressValidForCheckoutAsync(branchId, packNameId, packNameAddressId.Value, customer, store.Id, storeSettings))
            {
                if (returnOnNull)
                    return (null, taxRate, appliedDiscounts);

                shippingTotals.Add(new PackNameShippingPrice { PackNameIds = new List<int> { packNameId }, Price = null, PriceInclTaxt = null, PriceExclTaxt = null, ShippingOption = shippingOption });
                continue;
            }

            //Try to find if there is a packName with the same address (same branch) and add to that list
            var shippingPrice = shippingTotals.FirstOrDefault(x => x.AddressId == packNameAddressId.Value && x.ShippingOption?.Name == shippingOption?.Name);

            if (shippingPrice != null)
                shippingPrice.PackNameIds.Add(packNameId);
            else
                shippingTotals.Add(new PackNameShippingPrice { PackNameIds = new List<int> { packNameId }, AddressId = packNameAddressId.Value, ShippingOption = shippingOption });
        }

        foreach (var shippingPrice in shippingTotals)
        {
            if (shippingPrice.AddressId == 0)
                continue;

            //The lines going to this destination. Free shipping, the products' additional shipping charges and the
            //shipping discount are worked out on them, not on the whole cart: each destination is its own parcel.
            var destinationCart = packNameCart
                .Where(item => shippingPrice.PackNameIds.Contains(item.PackNameId))
                .Cast<ShoppingCartItem>()
                .ToList();
            var isPackNameFreeShipping = await IsFreeShippingAsync(destinationCart);

            if (isPackNameFreeShipping)
            {
                shippingPrice.Price = decimal.Zero;
                shippingPrice.PriceInclTaxt = decimal.Zero;
                shippingPrice.PriceExclTaxt = decimal.Zero;

                continue;
            }

            decimal? packNameShippingTotal = null;

            if (shippingPrice.ShippingOption != null)
            {
                //use last shipping option (get from cache)
                (packNameShippingTotal, var packNameAppliedDiscounts) = await AdjustShippingRateAsync(shippingPrice.ShippingOption.Rate, destinationCart, shippingPrice.ShippingOption.IsPickupInStore);

                //A shipping discount applies to every destination, but it is one discount: record it once.
                foreach (var discount in packNameAppliedDiscounts)
                    if (!_discountService.ContainsDiscount(appliedDiscounts, discount))
                        appliedDiscounts.Add(discount);
            }

            if (!packNameShippingTotal.HasValue)
            {
                if (returnOnNull)
                    return (null, taxRate, appliedDiscounts);

                shippingPrice.Price = null;
                shippingPrice.PriceInclTaxt = null;
                shippingPrice.PriceExclTaxt = null;

                continue;
            }

            if (packNameShippingTotal.Value <= decimal.Zero)
            {
                shippingPrice.Price = decimal.Zero;
                shippingPrice.PriceInclTaxt = decimal.Zero;
                shippingPrice.PriceExclTaxt = decimal.Zero;

                continue;
            }

            decimal? packNameShippingTotalTaxedInclTaxt, packNameShippingTotalTaxedExclTaxt;

            (packNameShippingTotalTaxedInclTaxt, var packNameTaxRate) = await _taxService.GetShippingPriceAsync(packNameShippingTotal.Value,
                true,
                customer);

            taxRate = packNameTaxRate;

            (packNameShippingTotalTaxedExclTaxt, _) = await _taxService.GetShippingPriceAsync(packNameShippingTotal.Value,
                false,
                customer);

            //round
            if (_shoppingCartSettings.RoundPricesDuringCalculation)
            {
                packNameShippingTotalTaxedInclTaxt = await _priceCalculationService.RoundPriceAsync(packNameShippingTotalTaxedInclTaxt.Value);
                packNameShippingTotalTaxedExclTaxt = await _priceCalculationService.RoundPriceAsync(packNameShippingTotalTaxedExclTaxt.Value);
            }

            var shippingTotalTaxed = includingTax ? packNameShippingTotalTaxedInclTaxt : packNameShippingTotalTaxedExclTaxt;

            shippingPrice.Price = shippingTotalTaxed;
            shippingPrice.PriceInclTaxt = packNameShippingTotalTaxedInclTaxt;
            shippingPrice.PriceExclTaxt = packNameShippingTotalTaxedExclTaxt;
        }

        return (shippingTotals, taxRate, appliedDiscounts);
    }

    /// <summary>
    /// Gets the amount the settlement pipeline is offered for a cart. For a packname cart it is the goods at
    /// their line prices plus shipping, both including tax, before the subtotal discount, the payment method
    /// fee and any order-level discount: subsidy and store credit settle the full price of what is bought,
    /// and a discount the customer brings reduces only the customer's share. For any other cart it is nop's
    /// own total.
    /// </summary>
    /// <param name="cart">Cart</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the settleable amount, or null when shipping cannot be calculated yet
    /// </returns>
    protected virtual async Task<decimal?> GetSettleableAmountAsync(IList<ShoppingCartItem> cart)
    {
        if (!await IsPackNameCartAsync(cart))
            return (await base.GetShoppingCartTotalAsync(cart)).shoppingCartTotal;

        var customer = await _customerService.GetShoppingCartCustomerAsync(cart);

        if (customer == null)
            return decimal.Zero;

        var store = await _storeContext.GetCurrentStoreAsync();
        var packNameCart = await _shoppingCartService.GetPackNameShoppingCartItemsAsync(customer, 0, store.Id);

        //Asked for by the totals and by the applied discount in the same request: the subtotal prices every line,
        //so it is worked out once per request for a cart of the same lines and quantities and the same addresses
        //(the tax depends on them, and placing an order can set the billing address). Shipping is not kept:
        //a request can change the selected method or address between the two.
        var subTotalWithoutDiscountInclTax = await _shortTermCacheManager.GetAsync(async () =>
            (await GetShoppingCartSubTotalAsync(packNameCart.Cast<ShoppingCartItem>().ToList(), true)).subTotalWithoutDiscount,
            OrderServicesDefaults.PackNameCartSubTotalCacheKey,
            customer.Id,
            store.Id,
            string.Join(',', packNameCart.OrderBy(x => x.Id).Select(x => $"{x.Id}:{x.Quantity}")),
            customer.BillingAddressId ?? 0,
            customer.ShippingAddressId ?? 0);

        var shippingTotals = (await GetShoppingCartShippingTotalAsync(packNameCart, true)).shippingTotal;

        //nop's contract: a destination that cannot be rated yet nulls the whole figure
        if (shippingTotals.Any(x => !x.PriceInclTaxt.HasValue))
            return null;

        var settleable = subTotalWithoutDiscountInclTax + shippingTotals.Sum(x => x.PriceInclTaxt.Value);

        if (_shoppingCartSettings.RoundPricesDuringCalculation)
            settleable = await _priceCalculationService.RoundPriceAsync(settleable);

        return settleable;
    }

    /// <summary>
    /// Gets the order subtotal discount of the current customer's packname cart, limited to the balance it comes off
    /// </summary>
    /// <param name="cart">Cart</param>
    /// <param name="balanceDue">What subsidy and store credit left to pay, including tax</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the discount applied, including and excluding tax, and the discounts applied (empty when
    /// the discount takes nothing off)
    /// </returns>
    protected virtual async Task<(decimal discountAmountInclTax, decimal discountAmountExclTax, List<Discount> appliedDiscounts)> GetPackNameSubTotalDiscountAsync(IList<ShoppingCartItem> cart, decimal balanceDue)
    {
        var customer = await _customerService.GetShoppingCartCustomerAsync(cart);

        if (customer == null)
            return (decimal.Zero, decimal.Zero, new List<Discount>());

        var store = await _storeContext.GetCurrentStoreAsync();
        var packNameCart = await _shoppingCartService.GetPackNameShoppingCartItemsAsync(customer, 0, store.Id);

        var (discountInclTax, discountExclTax, appliedDiscounts, _, _, _, _, _) = await GetShoppingCartSubTotalsAsync(packNameCart.Cast<ShoppingCartItem>().ToList());

        var (appliedInclTax, appliedExclTax) = OrderSettlementHelper.LimitDiscount(discountInclTax, discountExclTax, balanceDue);

        if (appliedInclTax <= decimal.Zero)
            return (decimal.Zero, decimal.Zero, new List<Discount>());

        if (_shoppingCartSettings.RoundPricesDuringCalculation && appliedInclTax < discountInclTax)
        {
            appliedInclTax = await _priceCalculationService.RoundPriceAsync(appliedInclTax);
            appliedExclTax = await _priceCalculationService.RoundPriceAsync(appliedExclTax);
        }

        return (appliedInclTax, appliedExclTax, appliedDiscounts);
    }

    /// <summary>
    /// Gets the balance the customer owes for a cart, before exchange fees
    /// </summary>
    /// <remarks>
    /// For a packname cart the settlement pipeline runs first, on the settleable amount: subsidy, then store
    /// credit, against the goods and shipping at full price. What the pools leave is the customer's: the
    /// subtotal discount comes off it, the payment method fee goes onto it, and then the order total discount,
    /// gift cards and reward points apply, each to what is still due. A discount never reaches the subsidy,
    /// so it never moves employer money to the customer. For any other cart nop's total is settled as it is.
    /// </remarks>
    /// <param name="cart">Cart</param>
    /// <param name="useRewardPoints">A value indicating reward points should be used; null to detect current choice of the customer</param>
    /// <param name="usePaymentMethodAdditionalFee">A value indicating whether we should use payment method additional fee when calculating order total</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the balance due; null if it could not be calculated now. Applied discount amount. Applied discounts. Applied gift cards. Reward points to redeem. Reward points amount in primary store currency to redeem
    /// </returns>
    protected virtual async Task<(decimal? balanceDue, decimal discountAmount, List<Discount> appliedDiscounts, List<AppliedGiftCard> appliedGiftCards, int redeemedRewardPoints, decimal redeemedRewardPointsAmount)> GetShoppingCartTotalInternalAsync(IList<ShoppingCartItem> cart,
        bool? useRewardPoints = null, bool usePaymentMethodAdditionalFee = true)
    {
        if (!await IsPackNameCartAsync(cart))
        {
            var (total, totalDiscount, totalDiscounts, totalGiftCards, totalPoints, totalPointsAmount) =
                await base.GetShoppingCartTotalAsync(cart, useRewardPoints, usePaymentMethodAdditionalFee);

            if (!total.HasValue)
                return (null, totalDiscount, totalDiscounts, totalGiftCards, totalPoints, totalPointsAmount);

            var plainPlan = await QuoteCartSettlementAsync(cart, total.Value);

            return (plainPlan.BalanceDue, totalDiscount, totalDiscounts, totalGiftCards, totalPoints, totalPointsAmount);
        }

        var redeemedRewardPoints = 0;
        var redeemedRewardPointsAmount = decimal.Zero;

        var customer = await _customerService.GetShoppingCartCustomerAsync(cart);

        if (customer == null)
            return (decimal.Zero, decimal.Zero, new List<Discount>(), new List<AppliedGiftCard>(), 0, decimal.Zero);

        var settleableAmount = await GetSettleableAmountAsync(cart);

        if (!settleableAmount.HasValue)
            return (null, decimal.Zero, new List<Discount>(), new List<AppliedGiftCard>(), 0, decimal.Zero);

        //subsidy first, then store credit, on the goods and shipping at full price
        var plan = await QuoteCartSettlementAsync(cart, settleableAmount.Value);

        var store = await _storeContext.GetCurrentStoreAsync();

        //the customer's share: the subtotal discount comes off what the pools left, and never more than that
        var (subTotalDiscountInclTax, _, _) = await GetPackNameSubTotalDiscountAsync(cart, plan.BalanceDue);

        var resultTemp = plan.BalanceDue - subTotalDiscountInclTax;

        if (resultTemp < decimal.Zero)
            resultTemp = decimal.Zero;

        //payment method additional fee, including tax
        if (usePaymentMethodAdditionalFee)
        {
            var paymentMethodSystemName = await _genericAttributeService.GetAttributeAsync<string>(customer,
                NopCustomerDefaults.SelectedPaymentMethodAttribute, store.Id);

            if (!string.IsNullOrEmpty(paymentMethodSystemName))
            {
                var paymentMethodAdditionalFee = await _paymentService.GetAdditionalHandlingFeeAsync(cart,
                    paymentMethodSystemName);
                resultTemp += (await _taxService.GetPaymentMethodAdditionalFeeAsync(paymentMethodAdditionalFee,
                    true, customer)).price;
            }
        }

        if (_shoppingCartSettings.RoundPricesDuringCalculation)
            resultTemp = await _priceCalculationService.RoundPriceAsync(resultTemp);

        //order total discount
        var (discountAmount, appliedDiscounts) = await GetOrderTotalDiscountAsync(customer, resultTemp);

        if (resultTemp < discountAmount)
            discountAmount = resultTemp;

        //a discount that takes nothing off is not applied, so its use is not recorded
        if (discountAmount <= decimal.Zero)
            appliedDiscounts = new List<Discount>();

        resultTemp -= discountAmount;

        if (resultTemp < decimal.Zero)
            resultTemp = decimal.Zero;
        if (_shoppingCartSettings.RoundPricesDuringCalculation)
            resultTemp = await _priceCalculationService.RoundPriceAsync(resultTemp);

        //let's apply gift cards now (gift cards that can be used)
        var appliedGiftCards = new List<AppliedGiftCard>();
        resultTemp = await AppliedGiftCardsAsync(cart, appliedGiftCards, customer, resultTemp);

        if (resultTemp < decimal.Zero)
            resultTemp = decimal.Zero;
        if (_shoppingCartSettings.RoundPricesDuringCalculation)
            resultTemp = await _priceCalculationService.RoundPriceAsync(resultTemp);

        var balanceDue = resultTemp;

        //reward points
        (redeemedRewardPoints, redeemedRewardPointsAmount) = await SetRewardPointsAsync(redeemedRewardPoints, redeemedRewardPointsAmount, useRewardPoints, customer, balanceDue);

        balanceDue -= redeemedRewardPointsAmount;

        return (balanceDue, discountAmount, appliedDiscounts, appliedGiftCards, redeemedRewardPoints, redeemedRewardPointsAmount);
    }

    protected virtual async Task<SettlementPlan> QuoteCartSettlementUncachedAsync(IList<ShoppingCartItem> cart, Customer customer, int storeId, decimal settleableAmount)
    {
        ArgumentNullException.ThrowIfNull(cart);

        var context = await BuildCartSettlementContextAsync(customer, storeId, cart);

        return await _settlementPipeline.QuoteAsync(context, settleableAmount);
    }

    #endregion

    #region Overrides

    public override async Task<(decimal?, decimal, List<Discount>, List<AppliedGiftCard>, int, decimal)> GetShoppingCartTotalAsync(IList<ShoppingCartItem> cart, bool? useRewardPoints = null, bool usePaymentMethodAdditionalFee = true)
    {
        // The balance the pools left the customer, after the discounts that are the customer's to use.
        // Exchange fees are added last: fees are always cash.
        var (balance, discount, discounts, giftCards, rrPoints, rrAmount) =
            await GetShoppingCartTotalInternalAsync(cart, useRewardPoints, usePaymentMethodAdditionalFee);

        if (!balance.HasValue)
            return (balance, discount, discounts, giftCards, rrPoints, rrAmount);

        var feesAmount = cart?.Any() != true ? decimal.Zero : (await _exchangeService.GetExchangeFeesAmountsAsync(cart)).Values.Sum();

        var payable = balance.Value + feesAmount;

        if (_shoppingCartSettings.RoundPricesDuringCalculation)
            payable = await _priceCalculationService.RoundPriceAsync(payable);

        return (payable, discount, discounts, giftCards, rrPoints, rrAmount);
    }

    /// <summary>
    /// Gets shopping cart shipping total
    /// </summary>
    /// <param name="cart">Cart</param>
    /// <param name="includingTax">A value indicating whether calculated price should include tax</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the shipping total. Applied tax rate. Applied discounts
    /// </returns>
    public override async Task<(decimal? shippingTotal, decimal taxRate, List<Discount> appliedDiscounts)> GetShoppingCartShippingTotalAsync(IList<ShoppingCartItem> cart, bool includingTax)
    {
        var appliedDiscounts = new List<Discount>();
        var taxRate = decimal.Zero;

        //A return-derived cart is synthetic: BuildProductsListForReturnOrderAsync builds items in memory
        //that are never saved, so they carry no Id, while a real cart row always has one. Its shipping is
        //zero by definition — redelivery on a return is charged as a fee, and PrepareReturnOrderDetails
        //already writes zero onto the order. Asking nop to rate it returns null instead, which nulls the
        //whole order total and fails the placement with "Order total couldn't be calculated".
        if (cart.Any() && cart.All(item => item.Id == 0))
            return (decimal.Zero, taxRate, appliedDiscounts);

        var packName = await _packNameContext.GetCurrentPackNameAsync(await _workContext.GetCurrentCustomerAsync());

        if (packName == null)
            return await base.GetShoppingCartShippingTotalAsync(cart, includingTax);

        var packNameCart = await _shoppingCartService.GetPackNameShoppingCartItemsAsync(await _workContext.GetCurrentCustomerAsync(), 0, (await _storeContext.GetCurrentStoreAsync()).Id);

        var totals = await GetShoppingCartShippingTotalAsync(packNameCart, includingTax);

        //nop's contract: null means the shipping cannot be calculated yet (a packname with no address or no rate),
        //which its readers show as "calculated during checkout". Zero would read as free shipping.
        if (totals.shippingTotal.Any(x => x.Price == null))
            return (null, taxRate, appliedDiscounts);

        return (totals.shippingTotal.Sum(x => x.Price.Value), totals.taxRate, totals.appliedDiscounts);
    }

    #endregion

    #region Methods

    /// <summary>
    /// Gets shopping cart shipping total
    /// </summary>
    /// <param name="packNameCart">Cart</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the shipping total
    /// </returns>
    public virtual async Task<IList<PackNameShippingPrice>> GetShoppingCartShippingTotalAsync(IList<PackNameShoppingCartItem> packNameCart)
    {
        var includingTax = await _workContext.GetTaxDisplayTypeAsync() == TaxDisplayType.IncludingTax;
        return (await GetShoppingCartShippingTotalAsync(packNameCart, includingTax)).shippingTotal;
    }

    /// <summary>
    /// Gets shopping cart shipping total
    /// </summary>
    /// <param name="packNameCart">Cart</param>
    /// <param name="includingTax">A value indicating whether calculated price should include tax</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the shipping total. Applied tax rate. Applied discounts
    /// </returns>
    public virtual async Task<(List<PackNameShippingPrice> shippingTotal, decimal taxRate, List<Discount> appliedDiscounts)> GetShoppingCartShippingTotalAsync(IList<PackNameShoppingCartItem> packNameCart, bool includingTax)
    {
        var shippingTotals = new List<PackNameShippingPrice>();
        var appliedDiscounts = new List<Discount>();
        var taxRate = decimal.Zero;

        var cart = packNameCart.Cast<ShoppingCartItem>().ToList();

        var customer = await _customerService.GetShoppingCartCustomerAsync(cart);

        if (customer == null)
            return (shippingTotals, taxRate, appliedDiscounts);

        if (packNameCart.Count == 0)
            return (shippingTotals, taxRate, appliedDiscounts);

        var storeSettings = await _settingService.LoadSettingAsync<MultiStoreSettings>(packNameCart.First().StoreId);

        //We check by packName because the shipping method and address can be different for each packName
        return await GetShippingTotalsByPackNames(packNameCart, storeSettings, includingTax, false);
    }

    /// <summary>
    /// Gets shopping cart shipping total
    /// </summary>
    /// <param name="packNameCart">Cart</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the shipping total. Applied tax rate. Applied discounts
    /// </returns>
    public virtual async Task<(List<PackNameShippingPrice>, decimal taxRate, List<Discount> appliedDiscounts)> GetShoppingCartShippingTotalsAsync(IList<PackNameShoppingCartItem> packNameCart)
    {
        var shippingTotals = new List<PackNameShippingPrice>();
        var appliedDiscounts = new List<Discount>();
        var taxRate = decimal.Zero;

        var cart = packNameCart.Cast<ShoppingCartItem>().ToList();
        var customer = await _customerService.GetShoppingCartCustomerAsync(cart);

        if (customer == null)
            return (shippingTotals, taxRate, appliedDiscounts);

        if (packNameCart.Count == 0)
            return (shippingTotals, taxRate, appliedDiscounts);

        var storeSettings = await _settingService.LoadSettingAsync<MultiStoreSettings>(packNameCart.First().StoreId);

        //We check by packName because the shipping method and address can be different for each packName
        return await GetShippingTotalsByPackNames(packNameCart, storeSettings, await _workContext.GetTaxDisplayTypeAsync() == TaxDisplayType.IncludingTax, true);
    }

    public virtual async Task<(decimal discountAmountInclTax, decimal discountAmountExclTax, List<Discount> appliedDiscounts)> GetAppliedSubTotalDiscountAsync(IList<ShoppingCartItem> cart)
    {
        ArgumentNullException.ThrowIfNull(cart);

        if (!await IsPackNameCartAsync(cart))
        {
            var (discountInclTax, discountExclTax, appliedDiscounts, _, _, _, _, _) = await GetShoppingCartSubTotalsAsync(cart);

            return (discountInclTax, discountExclTax, appliedDiscounts);
        }

        var settleableAmount = await GetSettleableAmountAsync(cart);

        //Shipping cannot be rated yet, so nothing is settled yet either: the discount is not limited
        var balanceDue = settleableAmount.HasValue
            ? (await QuoteCartSettlementAsync(cart, settleableAmount.Value)).BalanceDue
            : decimal.MaxValue;

        return await GetPackNameSubTotalDiscountAsync(cart, balanceDue);
    }

    public virtual async Task<SettlementPlan> QuoteCartSettlementAsync(IList<ShoppingCartItem> cart, decimal? settleableAmount = null)
    {
        ArgumentNullException.ThrowIfNull(cart);

        var customer = await _customerService.GetShoppingCartCustomerAsync(cart);

        if (customer == null)
        {
            //Not the same thing as "nothing to settle", but it returns the same value: an empty plan has a
            //SettleableAmount of zero, and SettlementPipeline.WalkAsync treats zero as no work and returns
            //without running a single source. Unlogged, a failure here is indistinguishable from an order
            //that genuinely owed nothing.
            await _logger.WarningAsync($"[Settlement] Cart settlement skipped: no customer resolved from a cart of {cart.Count} item(s).");

            return new SettlementPlan();
        }

        // Resolve the amount BEFORE the cache key is built. The totals pass the settleable amount explicitly
        // while every display caller passes null; unless both resolve to the same number first, they key
        // differently and each walks the pipeline separately.
        var amount = settleableAmount;

        if (!amount.HasValue)
        {
            var settleable = await GetSettleableAmountAsync(cart);

            if (!settleable.HasValue)
            {
                //Same silent-zero problem as the customer check above. In practice this is nop failing to
                //rate shipping: a synthetic return cart has no selected shipping option, and nop nulls the
                //entire total when it cannot rate one.
                await _logger.WarningAsync($"[Settlement] Cart settlement skipped for customer {customer.Id}: the settleable amount could not be calculated for a cart of {cart.Count} item(s) in store {cart.FirstOrDefault()?.StoreId}.");

                return new SettlementPlan();
            }

            amount = settleable.Value;
        }

        var storeId = cart.FirstOrDefault()?.StoreId ?? (await _storeContext.GetCurrentStoreAsync()).Id;

        return await _shortTermCacheManager.GetAsync(
            async () => await QuoteCartSettlementUncachedAsync(cart, customer, storeId, amount.Value),
            OrderServicesDefaults.CartSettlementPlanCacheKey,
            customer.Id,
            storeId,
            string.Join(',', cart.OrderBy(x => x.Id).Select(x => $"{x.Id}:{x.Quantity}")),
            amount.Value);
    }

    public virtual async Task<SubsidyAllocation> AllocateCartSubsidyAsync(IList<PackNameShoppingCartItem> cart, Customer customer, int storeId, decimal? budgetInclTax = null)
    {
        ArgumentNullException.ThrowIfNull(cart);
        ArgumentNullException.ThrowIfNull(customer);

        if (cart.Count == 0)
            return SubsidyAllocation.Empty;

        var shippingPrices = (await GetShoppingCartShippingTotalAsync(cart, false)).shippingTotal;

        return await _subsidyAllocationService.AllocateAsync(new SubsidyAllocationRequest
        {
            Customer = customer,
            StoreId = storeId,
            Cart = cart,
            ShippingPrices = shippingPrices,
            BudgetInclTax = budgetInclTax,
        });
    }

    public virtual async Task<SubsidyAllocation> GetCartSubsidyBreakdownAsync(IList<PackNameShoppingCartItem> packNameCart)
    {
        ArgumentNullException.ThrowIfNull(packNameCart);

        if (packNameCart.Count == 0)
            return SubsidyAllocation.Empty;

        var cart = packNameCart.Cast<ShoppingCartItem>().ToList();
        var customer = await _customerService.GetShoppingCartCustomerAsync(cart);

        if (customer == null)
            return SubsidyAllocation.Empty;

        //Until the packname checkout has given every packname of the cart a shipping address and method, shipping
        //cannot be rated, so the cart has no total to settle and no subsidy budget. The goods' subsidy is still known:
        //it is allocated without the order-total cap, and shipping's share is left out until it can be rated
        var settleableAmount = await GetSettleableAmountAsync(cart);

        if (!settleableAmount.HasValue)
            return await AllocateCartSubsidyAsync(packNameCart, customer, packNameCart[0].StoreId, budgetInclTax: null);

        // The same plan the totals used — memoised per request, so this is free after the first call.
        var plan = await QuoteCartSettlementAsync(cart, settleableAmount.Value);

        return await AllocateCartSubsidyAsync(
            packNameCart,
            customer,
            packNameCart[0].StoreId,
            budgetInclTax: plan.BudgetFor(SettlementSourceNames.SUBSIDY));
    }

    #endregion
}
