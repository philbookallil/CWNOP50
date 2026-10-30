using Nop.Core;
using Nop.Core.Caching;
using Nop.Core.Domain.Common;
using Nop.Core.Domain.Customers;
using Nop.Core.Domain.Directory;
using Nop.Core.Domain.Localization;
using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Payments;
using Nop.Core.Domain.Shipping;
using Nop.Core.Domain.Tax;
using Nop.Core.Events;
using Nop.Data;
using Nop.Plugin.CW.Core.Constants;
using Nop.Plugin.CW.Core.Domain.Exchanges;
using Nop.Plugin.CW.Core.Domain.Orders;
using Nop.Plugin.CW.Core.Domain.Orders.Views;
using Nop.Plugin.CW.Core.Domain.Settings;
using Nop.Plugin.CW.Core.Helpers;
using Nop.Plugin.CW.Core.Services.Common;
using Nop.Plugin.CW.Core.Services.CreditNotes;
using Nop.Plugin.CW.Core.Services.Customers;
using Nop.Plugin.CW.Core.Services.Exchanges;
using Nop.Plugin.CW.Core.Services.MultiStores;
using Nop.Plugin.CW.Core.Services.PackNames;
using Nop.Plugin.CW.Core.Services.Settings;
using Nop.Plugin.CW.Core.Services.Settlement;
using Nop.Plugin.CW.Core.Services.Subsidies;
using Nop.Services.Affiliates;
using Nop.Services.Catalog;
using Nop.Services.Common;
using Nop.Services.Configuration;
using Nop.Services.Directory;
using Nop.Services.Discounts;
using Nop.Services.Helpers;
using Nop.Services.Localization;
using Nop.Services.Logging;
using Nop.Services.Messages;
using Nop.Services.Orders;
using Nop.Services.Payments;
using Nop.Services.Security;
using Nop.Services.Shipping;
using Nop.Services.Stores;
using Nop.Services.Tax;
using Nop.Services.Vendors;
using System.Globalization;

namespace Nop.Plugin.CW.Core.Services.Orders;

public class OrderProcessingOverrideService : OrderProcessingService, IOrderProcessingOverrideService
{
    #region Fields

    //How long an exchange order waits for the packname locks before it settles without them: 20 tries, half a
    //second apart. A checkout holds them for the few seconds its settlement takes.
    protected const int ReturnOrderSettleLockAttempts = 20;
    protected static readonly TimeSpan ReturnOrderSettleLockDelay = TimeSpan.FromMilliseconds(500);

    protected readonly IExchangeService _exchangeService;
    protected readonly ICreditNotesService _creditNotesService;
    protected new readonly IShoppingCartOverrideService _shoppingCartService;
    protected readonly ISettingService _settingService;
    protected readonly IPackNameService _packNameService;
    protected new readonly IOrderTotalCalculationOverrideService _orderTotalCalculationService;
    protected readonly ICWOrderService _cwOrderService;
    protected readonly ICWAddressService _cwAddressService;
    protected readonly IPackNameShippingAssignmentService _shippingAssignmentService;
    protected readonly IRepository<Address> _addressRepository;
    protected readonly IRepository<PackNameCartItem> _packNameCartItemRepository;
    protected readonly IRepository<ShoppingCartItem> _sciRepository;
    protected readonly ISubsidyCreditService _subsidyCreditService;
    protected readonly ILocker _locker;
    protected new readonly ICustomerOverrideService _customerService;
    protected readonly ISettlementPipeline _settlementPipeline;
    protected readonly ICWFeatureService _cwFeatureService;
    protected readonly IMultiStoreService _multiStoreService;

    #endregion

    #region Ctor

    public OrderProcessingOverrideService(IExchangeService exchangeService,
        ICreditNotesService creditNotesService,
        CurrencySettings currencySettings,
        IAddressService addressService,
        IAffiliateService affiliateService,
        ICheckoutAttributeFormatter checkoutAttributeFormatter,
        ICountryService countryService,
        ICurrencyService currencyService,
        ICustomerActivityService customerActivityService,
        ICustomerOverrideService customerService,
        ICustomNumberFormatter customNumberFormatter,
        IDiscountService discountService,
        IEncryptionService encryptionService,
        IEventPublisher eventPublisher,
        IGenericAttributeService genericAttributeService,
        IGiftCardService giftCardService,
        ILanguageService languageService,
        ILocalizationService localizationService,
        ILogger logger,
        IOrderService orderService,
        IOrderTotalCalculationOverrideService orderTotalCalculationService,
        IPaymentPluginManager paymentPluginManager,
        IPaymentService paymentService,
        IPdfService pdfService,
        IPriceCalculationService priceCalculationService,
        IPriceFormatter priceFormatter,
        IProductAttributeFormatter productAttributeFormatter,
        IProductAttributeParser productAttributeParser,
        IProductService productService,
        IQueuedEmailService queuedEmailService,
        IReturnRequestService returnRequestService,
        IRewardPointService rewardPointService,
        IShipmentService shipmentService,
        IShippingService shippingService,
        IShoppingCartOverrideService shoppingCartService,
        IStateProvinceService stateProvinceService,
        IStaticCacheManager staticCacheManager,
        IStoreContext storeContext,
        IStoreMappingService storeMappingService,
        IStoreService storeService,
        ITaxService taxService,
        IVendorService vendorService,
        IWebHelper webHelper,
        IWorkContext workContext,
        IWorkflowMessageService workflowMessageService,
        LocalizationSettings localizationSettings,
        OrderSettings orderSettings,
        PaymentSettings paymentSettings,
        ReturnRequestSettings returnRequestSettings,
        RewardPointsSettings rewardPointsSettings,
        ShippingSettings shippingSettings,
        TaxSettings taxSettings,
        ISettingService settingService,
        IPackNameService packNameService,
        ICWOrderService cwOrderService,
        ICWAddressService cwAddressService,
        IPackNameShippingAssignmentService shippingAssignmentService,
        IRepository<Address> addressRepository,
        IRepository<PackNameCartItem> packNameCartItemRepository,
        IRepository<ShoppingCartItem> sciRepository,
        ISubsidyCreditService subsidyCreditService,
        ILocker locker,
        ISettlementPipeline settlementPipeline,
        ICWFeatureService cwFeatureService,
        IMultiStoreService multiStoreService) : base(currencySettings,
            addressService,
            affiliateService,
            checkoutAttributeFormatter,
            countryService,
            currencyService,
            customerActivityService,
            customerService,
            customNumberFormatter,
            discountService,
            encryptionService,
            eventPublisher,
            genericAttributeService,
            giftCardService,
            languageService,
            localizationService,
            logger, orderService,
            orderTotalCalculationService,
            paymentPluginManager,
            paymentService, pdfService,
            priceCalculationService,
            priceFormatter,
            productAttributeFormatter,
            productAttributeParser,
            productService,
            queuedEmailService,
            returnRequestService,
            rewardPointService,
            shipmentService,
            shippingService,
            shoppingCartService,
            stateProvinceService,
            staticCacheManager,
            storeContext,
            storeMappingService,
            storeService,
            taxService,
            vendorService,
            webHelper,
            workContext,
            workflowMessageService,
            localizationSettings,
            orderSettings,
            paymentSettings,
            returnRequestSettings,
            rewardPointsSettings,
            shippingSettings,
            taxSettings)
    {
        _exchangeService = exchangeService;
        _creditNotesService = creditNotesService;
        _shoppingCartService = shoppingCartService;
        _settingService = settingService;
        _packNameService = packNameService;
        _orderTotalCalculationService = orderTotalCalculationService;
        _cwOrderService = cwOrderService;
        _cwAddressService = cwAddressService;
        _shippingAssignmentService = shippingAssignmentService;
        _addressRepository = addressRepository;
        _packNameCartItemRepository = packNameCartItemRepository;
        _sciRepository = sciRepository;
        _subsidyCreditService = subsidyCreditService;
        _locker = locker;
        _customerService = customerService;
        _settlementPipeline = settlementPipeline;
        _cwFeatureService = cwFeatureService;
        _multiStoreService = multiStoreService;
    }

    #endregion

    #region Utilities

    /// <summary>
    /// Special method to create a PlaceOrderDetails specifically for a Return Order
    /// </summary>
    /// <param name="returnRequest"></param>
    /// <param name="returnRequestItem"></param>
    /// <returns></returns>
    protected async Task<PlaceOrderContainer> PrepareReturnOrderDetails(ReturnRequest returnRequest, ReturnRequestItem returnRequestItem)
    {
        var storeId = returnRequest.StoreId;

        var details = new PlaceOrderContainer
        {
            //customer
            Customer = await _customerService.GetCustomerByIdAsync(returnRequest.CustomerId)
        };

        if (details.Customer == null)
            throw new ArgumentException("Customer is not set");

        // Retrieve the data for the original Order (the Order that originated this Return Request)
        var originalOrder = await _exchangeService.GetOrderForReturnRequestAsync(returnRequest);

        //affiliate
        var affiliate = await _affiliateService.GetAffiliateByIdAsync(details.Customer.AffiliateId);
        if (affiliate != null && affiliate.Active && !affiliate.Deleted)
            details.AffiliateId = affiliate.Id;

        //check whether customer is guest
        if (await _customerService.IsGuestAsync(details.Customer) && !_orderSettings.AnonymousCheckoutAllowed)
            throw new NopException("Anonymous checkout is not allowed");

        //customer currency
        var currentCurrency = await _workContext.GetWorkingCurrencyAsync();
        var currencyTmp = await _currencyService.GetCurrencyByIdAsync(details.Customer.CurrencyId ?? 0);
        var customerCurrency = currencyTmp != null && currencyTmp.Published && await _storeMappingService.AuthorizeAsync(currencyTmp) ? currencyTmp : currentCurrency;
        var primaryStoreCurrency = await _currencyService.GetCurrencyByIdAsync(_currencySettings.PrimaryStoreCurrencyId);
        details.CustomerCurrencyCode = customerCurrency.CurrencyCode;
        details.CustomerCurrencyRate = customerCurrency.Rate / primaryStoreCurrency.Rate;

        //customer language
        details.CustomerLanguage = await _languageService.GetLanguageByIdAsync(details.Customer.LanguageId ?? 0);
        if (details.CustomerLanguage == null || !details.CustomerLanguage.Published || !await _storeMappingService.AuthorizeAsync(details.CustomerLanguage))
            details.CustomerLanguage = await _workContext.GetWorkingLanguageAsync();

        //billing address (from original Order)
        var billingAddress = await _addressService.GetAddressByIdAsync(originalOrder.BillingAddressId);

        //Cloned, not shared: an order keeps its own copy of the address so that editing the customer's
        //later cannot rewrite a historical order — and the email fix-up below would otherwise write onto
        //the ORIGINAL order's row. SaveReturnOrderDetailsAsync inserts the clone.
        details.BillingAddress = _addressService.CloneAddress(billingAddress);

        // Use the customer's email if there is none under the Billing Address or if it is invalid
        if (string.IsNullOrEmpty(details.BillingAddress.Email) || !CommonHelper.IsValidEmail(details.BillingAddress.Email))
        {
            details.BillingAddress.Email = details.Customer.Email;
        }

        if (await _countryService.GetCountryByAddressAsync(billingAddress) is Country billingCountry && !billingCountry.AllowsBilling)
            throw new NopException($"Country '{billingCountry.Name}' is not allowed for billing");

        //checkout attributes
        details.CheckoutAttributesXml = await _genericAttributeService.GetAttributeAsync<string>(details.Customer, NopCustomerDefaults.CheckoutAttributes, storeId);
        details.CheckoutAttributeDescription = await _checkoutAttributeFormatter.FormatAttributesAsync(details.CheckoutAttributesXml, details.Customer);

        //load a "fake" shopping cart using the items from the return request
        details.Cart = await _exchangeService.BuildProductsListForReturnOrderAsync(returnRequest, returnRequestItem);

        if (!details.Cart.Any())
            throw new NopException("Cart is empty");

        //min totals validation
        if (!await ValidateMinOrderSubtotalAmountAsync(details.Cart))
        {
            var minOrderSubtotalAmount = await _currencyService.ConvertFromPrimaryStoreCurrencyAsync(_orderSettings.MinOrderSubtotalAmount, currentCurrency);
            throw new NopException(string.Format(await _localizationService.GetResourceAsync("Checkout.MinOrderSubtotalAmount"),
                await _priceFormatter.FormatPriceAsync(minOrderSubtotalAmount, true, false)));
        }

        if (!await ValidateMinOrderTotalAmountAsync(details.Cart))
        {
            var minOrderTotalAmount = await _currencyService.ConvertFromPrimaryStoreCurrencyAsync(_orderSettings.MinOrderTotalAmount, currentCurrency);
            throw new NopException(string.Format(await _localizationService.GetResourceAsync("Checkout.MinOrderTotalAmount"),
                await _priceFormatter.FormatPriceAsync(minOrderTotalAmount, true, false)));
        }

        //tax display type
        details.CustomerTaxDisplayType = await _customerService.GetCustomerTaxDisplayTypeAsync(details.Customer);

        //whether customers are allowed to select tax display type and the customer has previously saved one
        if (_taxSettings.AllowCustomersToSelectTaxDisplayType && details.Customer.TaxDisplayTypeId.HasValue)
            details.CustomerTaxDisplayType = (TaxDisplayType)details.Customer.TaxDisplayTypeId.Value;
        else
        {
            //details.CustomerTaxDisplayType = _taxSettings.TaxDisplayType;
            //default tax type by customer roles
            var defaultRoleTaxDisplayType = await _customerService.GetCustomerDefaultTaxDisplayTypeAsync(details.Customer);
            if (defaultRoleTaxDisplayType.HasValue)
                details.CustomerTaxDisplayType = defaultRoleTaxDisplayType.Value;
        }

        //sub total (incl tax)
        var (discountAmountInclTax, discountAmountExclTax, appliedDiscounts, subTotalWithoutDiscountInclTax,
            subTotalWithoutDiscountExclTax, _, _, _) =
        await _orderTotalCalculationService.GetShoppingCartSubTotalsAsync(details.Cart);

        //sub total (incl tax)
        details.OrderSubTotalInclTax = subTotalWithoutDiscountInclTax;
        details.OrderSubTotalDiscountInclTax = discountAmountInclTax;

        //discount history
        foreach (var disc in appliedDiscounts)
            if (!_discountService.ContainsDiscount(details.AppliedDiscounts, disc))
                details.AppliedDiscounts.Add(disc);

        //sub total (excl tax)
        details.OrderSubTotalExclTax = subTotalWithoutDiscountExclTax;
        details.OrderSubTotalDiscountExclTax = discountAmountExclTax;

        //shipping info
        if (await _shoppingCartService.ShoppingCartRequiresShippingAsync(details.Cart))
        {
            var pickupPoint = await _genericAttributeService.GetAttributeAsync<PickupPoint>(details.Customer,
                    NopCustomerDefaults.SelectedPickupPointAttribute, storeId);

            // If Pickup is allowed and the original order is set to it
            if (_shippingSettings.AllowPickupInStore && pickupPoint != null)
            {
                // Take the Pickup info from the Order from which this Return Request derivates
                details.PickupInStore = true;
                if (originalOrder.PickupAddressId.HasValue && await _addressService.GetAddressByIdAsync(originalOrder.PickupAddressId.Value) is Address pickupAddress)
                    details.PickupAddress = _addressService.CloneAddress(pickupAddress);
            }
            else
            {
                // If no Pickup options we take the Shipping address from the original order
                if (originalOrder.ShippingAddressId.HasValue && await _addressService.GetAddressByIdAsync(originalOrder.ShippingAddressId.Value) is Address shippingAddress)
                    details.ShippingAddress = _addressService.CloneAddress(shippingAddress);
            }

            details.ShippingMethodName = originalOrder.ShippingMethod;
            details.ShippingRateComputationMethodSystemName = originalOrder.ShippingRateComputationMethodSystemName;

            details.ShippingStatus = ShippingStatus.NotYetShipped;
        }
        else
            details.ShippingStatus = ShippingStatus.ShippingNotRequired;

        //shipping totals are overridden by the re-delivery fees calculated based on returned items
        details.OrderShippingTotalInclTax = decimal.Zero;
        details.OrderShippingTotalExclTax = decimal.Zero;

        //tax amount
        SortedDictionary<decimal, decimal> taxRatesDictionary;
        (details.OrderTaxTotal, taxRatesDictionary) = await _orderTotalCalculationService.GetTaxTotalAsync(details.Cart);

        //VAT number
        if (_taxSettings.EuVatEnabled && details.Customer.VatNumberStatus == VatNumberStatus.Valid)
            details.VatNumber = details.Customer.VatNumber;

        //tax rates
        details.TaxRates = taxRatesDictionary.Aggregate(string.Empty, (current, next) =>
            $"{current}{next.Key.ToString(CultureInfo.InvariantCulture)}:{next.Value.ToString(CultureInfo.InvariantCulture)};   ");

        //order total (and applied discounts, gift cards, reward points)
        var (orderTotal, orderDiscountAmount, orderAppliedDiscounts, appliedGiftCards, redeemedRewardPoints, redeemedRewardPointsAmount) = await _orderTotalCalculationService.GetShoppingCartTotalAsync(details.Cart);
        if (!orderTotal.HasValue)
            throw new NopException("Order total couldn't be calculated");

        details.OrderDiscountAmount = orderDiscountAmount;
        details.RedeemedRewardPoints = redeemedRewardPoints;
        details.RedeemedRewardPointsAmount = redeemedRewardPointsAmount;
        details.AppliedGiftCards = appliedGiftCards;
        details.OrderTotal = orderTotal.Value;

        //discount history
        foreach (var disc in orderAppliedDiscounts)
            if (!_discountService.ContainsDiscount(details.AppliedDiscounts, disc))
                details.AppliedDiscounts.Add(disc);

        return details;
    }

    protected async Task AssignReturnRequestCustomValuesAsync(Order order, ReturnRequest returnRequest)
    {
        // Create a ProcessPaymentRequest instance just to store custom values
        var customValuesContainer = new ProcessPaymentRequest();
        customValuesContainer.CustomValues.Add(new(CWOrderCustomValuesKeys.KEY_RETURN_REQUEST, string.Format("Related to Return Request ID {0}", returnRequest.Id), displayToCustomer: false));

        // Store the custom values as a Serialized string in XML format
        order.CustomValuesXml = customValuesContainer.CustomValues.SerializeToXml();//_paymentService.SerializeCustomValues(customValuesContainer);

        await _orderService.UpdateOrderAsync(order);
    }

    protected async Task AnnotateOrderWithReturnRequestAsync(Order order, ReturnRequest returnRequest)
    {
        //notes, messages
        await AddOrderNoteAsync(order, string.Format("Order created automatically to account for Return Request with ID: {0}", returnRequest.Id));
    }

    /// <summary>
    /// Move shopping cart items to order items
    /// </summary>
    /// <param name="details">Place order container</param>
    /// <param name="order">Order</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    protected virtual async Task<IReadOnlyDictionary<int, int>> MoveShoppingCartItemsToOrderItemsAsync(PlaceOrderContainer details, Order order, ReturnRequestItem returnRequestItem)
    {
        //Maps each cart item to the order item it became, for SubsidySettlementSource. A return-derived
        //cart is one line and its template carries Id 0 (which it must keep — see the price consumer), so
        //this is { 0 -> orderItemId }. Duplicate keys would mean more than one line, which would make the
        //mapping ambiguous rather than merely wrong, so fail loudly instead of overwriting.
        var orderItemIdByCartItemId = new Dictionary<int, int>();

        //This order was built from a SYNTHETIC cart — BuildProductsListForReturnOrderAsync makes items in
        //memory that were never in the customer's cart — so there is nothing of this order's to clear, and
        //clearing the cart wholesale (as this method used to) deletes whatever the customer was in the
        //middle of buying AND every other approved replacement sitting there from pathway B, each of which
        //carries the ReturnRequestItem link the price consumer and the completion flow read. That stranded
        //exchanges: flagged needs-completion with nothing left to complete.
        //
        //The only row that legitimately goes is a replacement for THIS return request left over from an
        //earlier pathway-B run, which would otherwise duplicate the order being placed here. Same scoping
        //rule as ExchangesLogic.UndoApprovedExchangedItemsAsync. Done before the loop so that nulling the
        //link below is persisted by RelateOrderItemToReturnRequest rather than racing it, and so the
        //ReturnRequestItem instance this method already holds is the only one ever written.
        var existingCart = await _shoppingCartService.GetShoppingCartAsync(details.Customer, ShoppingCartType.ShoppingCart, order.StoreId);

        foreach (var existingItem in existingCart)
        {
            var linkedItem = await _exchangeService.GetReturnRequestItemForCartItemAsync(existingItem.Id);

            if (linkedItem?.ReturnRequestId != returnRequestItem.ReturnRequestId)
                continue;

            returnRequestItem.ShoppingCartItemId = null;

            await _shoppingCartService.DeleteShoppingCartItemAsync(existingItem);
        }

        //move shopping cart items to order items
        foreach (var sc in details.Cart)
        {
            var product = await _productService.GetProductByIdAsync(sc.ProductId);

            //prices
            var scUnitPrice = (await _shoppingCartService.GetUnitPriceAsync(sc, true)).unitPrice;
            var (scSubTotal, discountAmount, scDiscounts, _) = await _shoppingCartService.GetSubTotalAsync(sc, true);
            var scUnitPriceInclTax = await _taxService.GetProductPriceAsync(product, scUnitPrice, true, details.Customer);
            var scUnitPriceExclTax = await _taxService.GetProductPriceAsync(product, scUnitPrice, false, details.Customer);
            var scSubTotalInclTax = await _taxService.GetProductPriceAsync(product, scSubTotal, true, details.Customer);
            var scSubTotalExclTax = await _taxService.GetProductPriceAsync(product, scSubTotal, false, details.Customer);
            var discountAmountInclTax = await _taxService.GetProductPriceAsync(product, discountAmount, true, details.Customer);
            var discountAmountExclTax = await _taxService.GetProductPriceAsync(product, discountAmount, false, details.Customer);
            foreach (var disc in scDiscounts)
                if (!_discountService.ContainsDiscount(details.AppliedDiscounts, disc))
                    details.AppliedDiscounts.Add(disc);

            //attributes
            var store = await _storeService.GetStoreByIdAsync(sc.StoreId);
            var attributeDescription = await _productAttributeFormatter.FormatAttributesAsync(product, sc.AttributesXml, details.Customer, store);

            var itemWeight = await _shippingService.GetShoppingCartItemWeightAsync(sc);

            //save order item
            var orderItem = new OrderItem
            {
                OrderItemGuid = Guid.NewGuid(),
                OrderId = order.Id,
                ProductId = product.Id,
                UnitPriceInclTax = scUnitPriceInclTax.price,
                UnitPriceExclTax = scUnitPriceExclTax.price,
                PriceInclTax = scSubTotalInclTax.price,
                PriceExclTax = scSubTotalExclTax.price,
                OriginalProductCost = await _priceCalculationService.GetProductCostAsync(product, sc.AttributesXml),
                AttributeDescription = attributeDescription,
                AttributesXml = sc.AttributesXml,
                Quantity = sc.Quantity,
                DiscountAmountInclTax = discountAmountInclTax.price,
                DiscountAmountExclTax = discountAmountExclTax.price,
                DownloadCount = 0,
                IsDownloadActivated = false,
                LicenseDownloadId = 0,
                ItemWeight = itemWeight,
                RentalStartDateUtc = sc.RentalStartDateUtc,
                RentalEndDateUtc = sc.RentalEndDateUtc
            };

            await _orderService.InsertOrderItemAsync(orderItem);

            if (!orderItemIdByCartItemId.TryAdd(sc.Id, orderItem.Id))
                throw new NopException($"Return order {order.Id} has more than one cart item with id {sc.Id}; subsidy settlement cannot tell them apart.");

            //gift cards
            await AddGiftCardsAsync(product, sc.AttributesXml, sc.Quantity, orderItem, scUnitPriceExclTax.price);

            // Relate the Order Item to the Return Request
            await RelateOrderItemToReturnRequest(orderItem.Id, returnRequestItem);

            // Carry the packname from the originally returned item onto the new exchange order item
            var sourcePackName = await _cwOrderService.GetPackNameOrderItemAsync(returnRequestItem.ReturnedOrderItemId);
            if (sourcePackName != null)
            {
                await _cwOrderService.InsertPackNameOrderItemAsync(new PackNameOrderItem
                {
                    OrderItemId = orderItem.Id,
                    PackNameId = sourcePackName.PackNameId,
                    PackName = sourcePackName.PackName,
                    BranchId = sourcePackName.BranchId,
                    BranchCode = sourcePackName.BranchCode,
                    Branch = sourcePackName.Branch,
                    DepartmentId = sourcePackName.DepartmentId,
                    Department = sourcePackName.Department,
                    CostCenterId = sourcePackName.CostCenterId,
                    CostCenter = sourcePackName.CostCenter,
                    PackNameCustomerId = sourcePackName.PackNameCustomerId
                });
            }

            //inventory
            await _productService.AdjustInventoryAsync(product, -sc.Quantity, sc.AttributesXml,
                string.Format(await _localizationService.GetResourceAsync("Admin.StockQuantityHistory.Messages.PlaceOrder"), order.Id));
        }

        return orderItemIdByCartItemId;
    }

    /// <summary>
    /// Projects a return-derived cart onto the packname of the item being returned, so the replacement can
    /// be settled against that employee's entitlement.
    /// </summary>
    /// <remarks>
    /// The replacement inherits the returned item's packname — the same row
    /// <see cref="MoveShoppingCartItemsToOrderItemsAsync"/> copies onto the new order item. Reading it in
    /// one place means the settlement quote and the placement cannot disagree about whose entitlement pays.
    /// </remarks>
    protected virtual async Task<IList<PackNameShoppingCartItem>> BuildReturnOrderPackNameCartAsync(IList<ShoppingCartItem> cart,
        ReturnRequestItem returnRequestItem)
    {
        ArgumentNullException.ThrowIfNull(returnRequestItem);

        if (cart == null || cart.Count == 0)
            return [];

        var sourcePackName = await _cwOrderService.GetPackNameOrderItemAsync(returnRequestItem.ReturnedOrderItemId);

        //No packname row means the original was not placed through a packname, so there is no entitlement
        //to spend. Empty makes SubsidySettlementSource.IsEnabledAsync false, which leaves the
        //credit-notes-only behaviour exactly as it was before this phase.
        if (sourcePackName == null)
            return [];

        return cart.Select(item => new PackNameShoppingCartItem
        {
            //Id stays at the template's 0, deliberately. ExchangeReplacementPriceConsumer branches on it
            //to choose between ExchangeReplacementScope and a saved cart row's link; a synthetic id would
            //send it looking for a link that does not exist and silently re-price the replacement live,
            //undoing fixes 6, 8 and 11.
            Id = item.Id,
            ShoppingCartType = item.ShoppingCartType,
            StoreId = item.StoreId,
            ProductId = item.ProductId,
            AttributesXml = item.AttributesXml,
            CustomerEnteredPrice = item.CustomerEnteredPrice,
            Quantity = item.Quantity,
            RentalStartDateUtc = item.RentalStartDateUtc,
            RentalEndDateUtc = item.RentalEndDateUtc,
            CreatedOnUtc = item.CreatedOnUtc,
            UpdatedOnUtc = item.UpdatedOnUtc,
            CustomerId = item.CustomerId,

            PackNameId = sourcePackName.PackNameId,
            Name = sourcePackName.PackName,
            PackNameCustomerId = sourcePackName.PackNameCustomerId,
            BranchId = sourcePackName.BranchId,
            BranchCode = sourcePackName.BranchCode,
            Branch = sourcePackName.Branch,
            DepartmentId = sourcePackName.DepartmentId,
            Department = sourcePackName.Department,
            CostCenterId = sourcePackName.CostCenterId,
            CostCenter = sourcePackName.CostCenter,
        }).ToList();
    }

    protected async Task RelateOrderItemToReturnRequest(int orderItemId, ReturnRequestItem returnRequestItem)
    {
        returnRequestItem.OrderItemId = orderItemId;

        await _exchangeService.UpdateReturnRequestItemAsync(returnRequestItem);
    }

    /// <summary>
    /// Saves an Order derived from a Return Request to the database
    /// </summary>
    /// <param name="returnRequest"></param>
    /// <param name="returnRequestItem"></param>
    /// <param name="details"></param>
    /// <returns></returns>
    protected virtual async Task<Order> SaveReturnOrderDetailsAsync(ReturnRequest returnRequest, ReturnRequestItem returnRequestItem, PlaceOrderContainer details)
    {
        var storeId = returnRequest.StoreId;
        var originalOrder = await _exchangeService.GetOrderForReturnRequestAsync(returnRequest);

        //The addresses on details are unsaved clones and carry no Id. An order stores its own copy, so they
        //have to be inserted before the Order references them — without this the Order goes in with Id 0 and
        //SQL rejects it on FK_Order_ShippingAddressId_Address_Id.
        //
        //nop does exactly this in SaveOrderDetailsAsync (OrderProcessingService), and so does the
        //packname path below via InsertCheckoutAddressAsync. This path was the one that never did.
        var billingAddressId = await _cwAddressService.InsertCheckoutAddressAsync(details.BillingAddress);

        //Null whenever the replacement does not ship, or the original order was collected in store.
        int? shippingAddressId = details.ShippingAddress != null
            ? await _cwAddressService.InsertCheckoutAddressAsync(details.ShippingAddress)
            : null;

        int? pickupAddressId = details.PickupInStore && details.PickupAddress != null
            ? await _cwAddressService.InsertCheckoutAddressAsync(details.PickupAddress)
            : null;

        var order = new Order
        {
            StoreId = storeId,
            OrderGuid = Guid.NewGuid(),
            CustomerId = details.Customer.Id,
            CustomerLanguageId = details.CustomerLanguage.Id,
            CustomerTaxDisplayType = details.CustomerTaxDisplayType,
            CustomerIp = _webHelper.GetCurrentIpAddress(),
            OrderSubtotalInclTax = details.OrderSubTotalInclTax,
            OrderSubtotalExclTax = details.OrderSubTotalExclTax,
            OrderSubTotalDiscountInclTax = details.OrderSubTotalDiscountInclTax,
            OrderSubTotalDiscountExclTax = details.OrderSubTotalDiscountExclTax,
            OrderShippingInclTax = details.OrderShippingTotalInclTax,
            OrderShippingExclTax = details.OrderShippingTotalExclTax,
            PaymentMethodAdditionalFeeInclTax = details.PaymentAdditionalFeeInclTax,
            PaymentMethodAdditionalFeeExclTax = details.PaymentAdditionalFeeExclTax,
            TaxRates = details.TaxRates,
            OrderTax = details.OrderTaxTotal,
            OrderTotal = details.OrderTotal,
            RefundedAmount = decimal.Zero,
            OrderDiscount = details.OrderDiscountAmount,
            CheckoutAttributeDescription = details.CheckoutAttributeDescription,
            CheckoutAttributesXml = details.CheckoutAttributesXml,
            CustomerCurrencyCode = details.CustomerCurrencyCode,
            CurrencyRate = details.CustomerCurrencyRate,
            AffiliateId = details.AffiliateId,
            OrderStatus = OrderStatus.Processing, // Order will be in "Procesing" status as shipping will still need to be completed
            PaymentMethodSystemName = string.Empty, // This will allow the payment method name to be set to "Credit Notes" or "Subsidy"
            PaidDateUtc = DateTime.UtcNow,
            PaymentStatus = PaymentStatus.Paid,
            PaymentStatusId = (int)PaymentStatus.Paid,
            BillingAddressId = billingAddressId,
            ShippingAddressId = shippingAddressId,
            ShippingStatus = details.ShippingStatus,
            ShippingMethod = details.ShippingMethodName,
            PickupInStore = details.PickupInStore,
            PickupAddressId = pickupAddressId,
            ShippingRateComputationMethodSystemName = details.ShippingRateComputationMethodSystemName,
            VatNumber = details.VatNumber,
            CreatedOnUtc = DateTime.UtcNow,
            CustomOrderNumber = string.Empty
        };

        await _orderService.InsertOrderAsync(order);

        //A return-derived order has exactly one destination — the packname the returned item shipped to.
        //The order-level ShippingAddressId above is not what the destinations block, the invoice, the
        //packing slip or the confirmation email read: they all read OrderShipping rows, so without the row
        //written below an auto-placed exchange shows "No shipping address recorded" and prints no ship-to
        //anywhere. Shipping amounts are zero because redelivery
        //on an exchange is charged as a FEE, which is also why details.OrderShippingTotal* are zero here.
        var destinationPackName = await _cwOrderService.GetPackNameOrderItemAsync(returnRequestItem.ReturnedOrderItemId);

        if (destinationPackName != null)
        {
            await _cwOrderService.InsertOrderShippingAsync(new OrderShipping
            {
                OrderId = order.Id,
                PackNameId = destinationPackName.PackNameId,
                ShippingStatus = details.ShippingStatus,
                PickupInStore = details.PickupInStore,
                PickupAddressId = pickupAddressId,
                ShippingAddressId = shippingAddressId,
                ShippingMethod = details.ShippingMethodName,
                ShippingRateComputationMethodSystemName = details.ShippingRateComputationMethodSystemName,
                ShippingInclTax = details.OrderShippingTotalInclTax,
                ShippingExclTax = details.OrderShippingTotalExclTax,
            });
        }

        //generate and set custom order number
        order.CustomOrderNumber = _customNumberFormatter.GenerateOrderCustomNumber(order);
        await _orderService.UpdateOrderAsync(order);

        //reward points history
        if (details.RedeemedRewardPointsAmount > decimal.Zero)
        {
            await _rewardPointService.AddRewardPointsHistoryEntryAsync(details.Customer, -details.RedeemedRewardPoints, order.StoreId,
                string.Format(await _localizationService.GetResourceAsync("RewardPoints.Message.ReturnedForOrder"), order.CustomOrderNumber));
        }

        return order;
    }

    protected async Task PreparePackNamesShippingInfoAsync(PlacePackNameOrderContainer details, ProcessPaymentRequest processPaymentRequest, MultiStoreSettings storeSettings)
    {
        var packNameIds = details.PackNamesCart.Select(item => item.PackNameId).Distinct().ToList();
        var assignments = await _shippingAssignmentService.LoadAsync(details.Customer, processPaymentRequest.StoreId);

        var addressIds = packNameIds
            .Select(assignments.GetAddressId)
            .Where(id => id is > 0)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();

        var addressMap = addressIds.Count > 0
            ? (await _addressRepository.Table.Where(a => addressIds.Contains(a.Id)).ToListAsync())
                .ToDictionary(a => a.Id)
            : new Dictionary<int, Address>();

        //There could be packnames with a different shipping address from the branch or same address from other branches
        //We need to check each packname and then group them by the address identifier and shipping method to make sure we group them even if they are from different branches.
        foreach (var packNameId in packNameIds)
        {
            var item = details.PackNamesCart.FirstOrDefault(item => item.PackNameId == packNameId);

            var branchId = item?.BranchId ?? 0;
            var store = item?.StoreName ?? string.Empty;

            var packName = await _packNameService.GetPackNameByIdAsync(packNameId) ?? throw new NopException($"No PackName could be loaded");

            //Selected shipping address
            var packNameAddressId = assignments.GetAddressId(packNameId);

            if (!packNameAddressId.HasValue || packNameAddressId.Value == 0 || !await _shoppingCartService.IsAddressValidForCheckoutAsync(branchId, packNameId, packNameAddressId.Value, details.Customer, processPaymentRequest.StoreId, storeSettings))
                throw new NopException($"No Address allocated for {packName.Name}");

            var shippingAddress = addressMap.GetValueOrDefault(packNameAddressId.Value) ?? throw new NopException($"No Address allocated for {packName.Name}");

            if (!CommonHelper.IsValidEmail(shippingAddress.Email))
                throw new NopException($"Email is not valid for {packName.Name}");

            if (await _countryService.GetCountryByAddressAsync(shippingAddress) is Country shippingCountry && !shippingCountry.AllowsShipping)
                throw new NopException($"Country '{shippingCountry.Name}' is not allowed for shipping");

            //selected shipping method
            var shippingOption = assignments.GetSelectedOption(packNameId) ?? throw new NopException($"No Shipping method allocated for {packName.Name}");

            var shippingData = new ShippingDataContainer
            {
                PackNameId = packNameId,
                PackName = packName?.Name ?? string.Empty,
                Store = item.StoreName,
                ShippingAddress = shippingAddress,
                ShippingMethodName = shippingOption.Name,
                ShippingRateComputationMethodSystemName = shippingOption.ShippingRateComputationMethodSystemName,
                ShippingStatus = ShippingStatus.NotYetShipped
            };

            details.ShippingData.Add(shippingData);
        }
    }

    /// <summary>
    /// Prepare and validate shipping info
    /// </summary>
    /// <param name="details">PlaceOrder container</param>
    /// <param name="processPaymentRequest">payment info holder</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    /// <exception cref="NopException">Validation problems</exception>
    protected async Task PrepareAndValidatePackNameShippingInfoAsync(PlacePackNameOrderContainer details, ProcessPaymentRequest processPaymentRequest)
    {
        var storeSettings = await _settingService.LoadSettingAsync<MultiStoreSettings>(details.Cart.First().StoreId);

        //shipping info
        if (await _shoppingCartService.ShoppingCartRequiresShippingAsync(details.Cart))
        {
            await PreparePackNamesShippingInfoAsync(details, processPaymentRequest, storeSettings);
            details.ShippingStatus = ShippingStatus.NotYetShipped;
        }
        else
            details.ShippingStatus = ShippingStatus.ShippingNotRequired;
    }

    protected virtual async Task CalculateShippingTotalsAsync(PlacePackNameOrderContainer details)
    {
        var (shippingTotals, _, shippingTotalDiscounts) = await _orderTotalCalculationService.GetShoppingCartShippingTotalsAsync(details.PackNamesCart);

        if (shippingTotals == null)
            throw new NopException("Shipping total couldn't be calculated");

        details.OrderShippingTotalInclTax = shippingTotals.Sum(x => x.PriceInclTaxt ?? 0m);
        details.OrderShippingTotalExclTax = shippingTotals.Sum(x => x.PriceExclTaxt ?? 0m);

        foreach (var shippingData in details.ShippingData)
        {
            var shippingTotal = shippingTotals.FirstOrDefault(x => x.PackNameIds.Contains(shippingData.PackNameId));

            if (shippingTotal == null)
                continue;

            shippingData.ShippingInclTax = shippingTotal.PriceInclTaxt ?? 0m;
            shippingData.ShippingExclTax = shippingTotal.PriceExclTaxt ?? 0m;
        }

        foreach (var disc in shippingTotalDiscounts)
            if (!_discountService.ContainsDiscount(details.AppliedDiscounts, disc))
                details.AppliedDiscounts.Add(disc);
    }

    /// <summary>
    /// Refuses the order when a line takes more of a product than the packname's subsidy allocation still
    /// allows. Each line was checked as the cart was built; this is the check against the allocation as it
    /// stands at placement, which another customer's order for the same packname may have drawn on since.
    /// </summary>
    /// <param name="details">PlaceOrder container</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    protected virtual async Task PrepareAndValidateSubsidyQuantityCapAsync(PlacePackNameOrderContainer details)
    {
        var warnings = await _shoppingCartService.GetSubsidyQuantityCapWarningsAsync(details.Customer, details.PackNamesCart);

        if (warnings.Count > 0)
            throw new NopException(string.Join(" ", warnings));
    }

    /// <summary>
    /// Prepare and validate all totals
    ///
    /// sub total, shipping total, payment total, tax amount etc.
    /// </summary>
    /// <param name="details">PlaceOrder container</param>
    /// <param name="processPaymentRequest">payment info holder</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    /// <exception cref="NopException">Validation problems</exception>
    protected async Task PrepareAndValidatePackNameTotalsAsync(PlacePackNameOrderContainer details, ProcessPaymentRequest processPaymentRequest)
    {
        var (_, _, _, subTotalWithoutDiscountInclTax, subTotalWithoutDiscountExclTax, _, _, _) =
            await _orderTotalCalculationService.GetShoppingCartSubTotalsAsync(details.Cart);

        //the subtotal discount the order actually applies: never more than subsidy and store credit left to pay
        var (discountAmountInclTax, discountAmountExclTax, appliedDiscounts) =
            await _orderTotalCalculationService.GetAppliedSubTotalDiscountAsync(details.Cart);

        //sub total (incl tax)
        details.OrderSubTotalInclTax = subTotalWithoutDiscountInclTax;
        details.OrderSubTotalDiscountInclTax = discountAmountInclTax;

        //discount history
        foreach (var disc in appliedDiscounts)
            if (!_discountService.ContainsDiscount(details.AppliedDiscounts, disc))
                details.AppliedDiscounts.Add(disc);

        //sub total (excl tax)
        details.OrderSubTotalExclTax = subTotalWithoutDiscountExclTax;
        details.OrderSubTotalDiscountExclTax = discountAmountExclTax;

        //shipping total
        await CalculateShippingTotalsAsync(details);

        //payment total
        var paymentAdditionalFee = await _paymentService.GetAdditionalHandlingFeeAsync(details.Cart, processPaymentRequest.PaymentMethodSystemName);
        details.PaymentAdditionalFeeInclTax = (await _taxService.GetPaymentMethodAdditionalFeeAsync(paymentAdditionalFee, true, details.Customer)).price;
        details.PaymentAdditionalFeeExclTax = (await _taxService.GetPaymentMethodAdditionalFeeAsync(paymentAdditionalFee, false, details.Customer)).price;

        //tax amount
        SortedDictionary<decimal, decimal> taxRatesDictionary;
        (details.OrderTaxTotal, taxRatesDictionary) = await _orderTotalCalculationService.GetTaxTotalAsync(details.Cart);

        //VAT number
        if (_taxSettings.EuVatEnabled && details.Customer.VatNumberStatus == VatNumberStatus.Valid)
            details.VatNumber = details.Customer.VatNumber;

        //tax rates
        details.TaxRates = taxRatesDictionary.Aggregate(string.Empty, (current, next) =>
            $"{current}{next.Key.ToString(CultureInfo.InvariantCulture)}:{next.Value.ToString(CultureInfo.InvariantCulture)};   ");

        //order total (and applied discounts, gift cards, reward points)
        var (orderTotal, orderDiscountAmount, orderAppliedDiscounts, appliedGiftCards, redeemedRewardPoints, redeemedRewardPointsAmount) = await _orderTotalCalculationService.GetShoppingCartTotalAsync(details.Cart);
        if (!orderTotal.HasValue)
            throw new NopException("Order total couldn't be calculated");

        details.OrderDiscountAmount = orderDiscountAmount;
        details.RedeemedRewardPoints = redeemedRewardPoints;
        details.RedeemedRewardPointsAmount = redeemedRewardPointsAmount;
        details.AppliedGiftCards = appliedGiftCards;
        details.OrderTotal = orderTotal.Value;

        // OrderTotal is the payable balance — GetShoppingCartTotalAsync has already netted credit notes
        // and subsidy off it. SettleOrderAsync needs the figure the pipeline was handed, before any of
        // that was subtracted, or it would settle only the residual and under-consume both pools.
        details.SettleableTotal = (await _orderTotalCalculationService.QuoteCartSettlementAsync(details.Cart)).SettleableAmount;

        //discount history
        foreach (var disc in orderAppliedDiscounts)
            if (!_discountService.ContainsDiscount(details.AppliedDiscounts, disc))
                details.AppliedDiscounts.Add(disc);

        processPaymentRequest.OrderTotal = details.OrderTotal;
    }

    /// <summary>
    /// Prepare details to place an order. It also sets some properties to "processPaymentRequest"
    /// </summary>
    /// <param name="processPaymentRequest">Process payment request</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the details
    /// </returns>
    protected async Task<PlacePackNameOrderContainer> PreparePlacePackNameOrderDetailsAsync(ProcessPaymentRequest processPaymentRequest)
    {
        var details = new PlacePackNameOrderContainer();

        var currentCurrency = await _workContext.GetWorkingCurrencyAsync();
        await PrepareAndValidateCustomerAsync(details, processPaymentRequest, currentCurrency);
        await PrepareAndValidateShoppingCartAndCheckoutAttributesAsync(details, processPaymentRequest, currentCurrency);

        //We need to use the packName shopping cart items
        details.PackNamesCart = await _shoppingCartService.GetPackNameShoppingCartItemsAsync(details.Customer, 0, processPaymentRequest.StoreId);
        details.Cart = details.PackNamesCart.Cast<ShoppingCartItem>().ToList();

        await PrepareAndValidateBillingAddressAsync(details);
        await PrepareAndValidatePackNameShippingInfoAsync(details, processPaymentRequest);
        await PrepareAndValidateSubsidyQuantityCapAsync(details);
        await PrepareAndValidatePackNameTotalsAsync(details, processPaymentRequest);

        //affiliate
        var affiliate = await _affiliateService.GetAffiliateByIdAsync(details.Customer.AffiliateId);
        if (affiliate != null && affiliate.Active && !affiliate.Deleted)
            details.AffiliateId = affiliate.Id;

        //tax display type
        details.CustomerTaxDisplayType = await _customerService.GetCustomerTaxDisplayTypeAsync(details.Customer);

        //recurring or standard shopping cart?
        var cart = details.Cart.Cast<ShoppingCartItem>().ToList();
        details.IsRecurringShoppingCart = await _shoppingCartService.ShoppingCartIsRecurringAsync(cart);
        if (!details.IsRecurringShoppingCart)
            return details;

        await PrepareAndValidateRecurringShoppingAsync(details, processPaymentRequest);

        return details;
    }

    protected async Task<Order> SavePackNameOrderDetailsAsync(ProcessPaymentRequest processPaymentRequest,
        ProcessPaymentResult processPaymentResult, PlacePackNameOrderContainer details)
    {
        var order = new Order
        {
            StoreId = processPaymentRequest.StoreId,
            OrderGuid = processPaymentRequest.OrderGuid,
            CustomerId = details.Customer.Id,
            CustomerLanguageId = details.CustomerLanguage.Id,
            CustomerTaxDisplayType = details.CustomerTaxDisplayType,
            CustomerIp = _webHelper.GetCurrentIpAddress(),
            OrderSubtotalInclTax = details.OrderSubTotalInclTax,
            OrderSubtotalExclTax = details.OrderSubTotalExclTax,
            OrderSubTotalDiscountInclTax = details.OrderSubTotalDiscountInclTax,
            OrderSubTotalDiscountExclTax = details.OrderSubTotalDiscountExclTax,
            OrderShippingInclTax = details.OrderShippingTotalInclTax,
            OrderShippingExclTax = details.OrderShippingTotalExclTax,
            PaymentMethodAdditionalFeeInclTax = details.PaymentAdditionalFeeInclTax,
            PaymentMethodAdditionalFeeExclTax = details.PaymentAdditionalFeeExclTax,
            TaxRates = details.TaxRates,
            OrderTax = details.OrderTaxTotal,
            OrderTotal = details.OrderTotal,
            RefundedAmount = decimal.Zero,
            OrderDiscount = details.OrderDiscountAmount,
            CheckoutAttributeDescription = details.CheckoutAttributeDescription,
            CheckoutAttributesXml = details.CheckoutAttributesXml,
            CustomerCurrencyCode = details.CustomerCurrencyCode,
            CurrencyRate = details.CustomerCurrencyRate,
            AffiliateId = details.AffiliateId,
            OrderStatus = OrderStatus.Pending,
            PaymentMethodSystemName = processPaymentRequest.PaymentMethodSystemName,
            AuthorizationTransactionId = processPaymentResult.AuthorizationTransactionId,
            AuthorizationTransactionCode = processPaymentResult.AuthorizationTransactionCode,
            AuthorizationTransactionResult = processPaymentResult.AuthorizationTransactionResult,
            CaptureTransactionId = processPaymentResult.CaptureTransactionId,
            CaptureTransactionResult = processPaymentResult.CaptureTransactionResult,
            SubscriptionTransactionId = processPaymentResult.SubscriptionTransactionId,
            PaymentStatus = processPaymentResult.NewPaymentStatus,
            PaidDateUtc = null,
            PickupInStore = details.PickupInStore,
            ShippingStatus = details.ShippingStatus,
            ShippingMethod = string.Join(", ", details.ShippingData.Select(x => x.ShippingMethodName)),
            ShippingRateComputationMethodSystemName = details.ShippingData.FirstOrDefault()?.ShippingRateComputationMethodSystemName ?? string.Empty,
            CustomValuesXml = processPaymentRequest.CustomValues.SerializeToXml(),
            VatNumber = details.VatNumber,
            CreatedOnUtc = DateTime.UtcNow,
            CustomOrderNumber = string.Empty
        };

        if (details.BillingAddress is null)
            throw new NopException("Billing address is not provided");

        order.BillingAddressId = await _cwAddressService.InsertCheckoutAddressAsync(details.BillingAddress);

        var orderShippingList = new List<OrderShipping>();

        // Track original address ID → inserted order-address ID so shared addresses
        // produce only one inserted record rather than one per packname.
        var insertedShippingAddressMap = new Dictionary<int, int>(); // original Id → new Id
        var insertedPickupAddressMap = new Dictionary<int, int>();

        foreach (var shippingData in details.ShippingData)
        {
            var orderShipping = new OrderShipping
            {
                PackNameId = shippingData.PackNameId,
                ShippingStatus = ShippingStatus.NotYetShipped,
                PickupInStore = false,
                ShippingMethod = shippingData.ShippingMethodName,
                ShippingRateComputationMethodSystemName = shippingData.ShippingRateComputationMethodSystemName,
                ShippingInclTax = shippingData.ShippingInclTax,
                ShippingExclTax = shippingData.ShippingExclTax,
            };

            if (shippingData.PickupAddress != null)
            {
                var originalId = shippingData.PickupAddress.Id;
                if (originalId > 0 && insertedPickupAddressMap.TryGetValue(originalId, out var existingId))
                {
                    orderShipping.PickupAddressId = existingId;
                }
                else
                {
                    var newId = await _cwAddressService.InsertCheckoutAddressAsync(shippingData.PickupAddress);
                    orderShipping.PickupAddressId = newId;
                    if (originalId > 0)
                        insertedPickupAddressMap[originalId] = newId;
                }
            }

            if (shippingData.ShippingAddress != null)
            {
                var originalId = shippingData.ShippingAddress.Id;
                if (originalId > 0 && insertedShippingAddressMap.TryGetValue(originalId, out var existingId))
                {
                    // Same source address already inserted — reuse it
                    orderShipping.ShippingAddressId = existingId;
                }
                else
                {
                    var newId = await _cwAddressService.InsertCheckoutAddressAsync(shippingData.ShippingAddress);
                    orderShipping.ShippingAddressId = newId;
                    if (originalId > 0)
                        insertedShippingAddressMap[originalId] = newId;
                }
            }

            orderShippingList.Add(orderShipping);
        }

        //We need to add a Shipping Address Identifier to the order so it doesn't throw an exception when it tries to get the order details for the admin area
        order.ShippingAddressId = orderShippingList.FirstOrDefault()?.ShippingAddressId;

        await _orderService.InsertOrderAsync(order);

        foreach (var orderShipping in orderShippingList)
        {
            orderShipping.OrderId = order.Id;
            await _cwOrderService.InsertOrderShippingAsync(orderShipping);
        }

        //generate and set custom order number
        order.CustomOrderNumber = _customNumberFormatter.GenerateOrderCustomNumber(order);
        await _orderService.UpdateOrderAsync(order);

        //reward points history
        if (details.RedeemedRewardPointsAmount <= decimal.Zero)
            return order;

        order.RedeemedRewardPointsEntryId = await _rewardPointService.AddRewardPointsHistoryEntryAsync(details.Customer, -details.RedeemedRewardPoints, order.StoreId,
            string.Format(await _localizationService.GetResourceAsync("RewardPoints.Message.RedeemedForOrder", order.CustomerLanguageId), order.CustomOrderNumber),
            order, details.RedeemedRewardPointsAmount);

        await _orderService.UpdateOrderAsync(order);

        return order;
    }

    /// <summary>
    /// Acquires an ILocker lock for every given PackName, in a fixed ascending order (so that any two
    /// callers touching an overlapping set of PackNames always request their locks in the same order
    /// and can't deadlock each other), then runs <paramref name="action"/> once every lock is held.
    /// Used to serialize the read-available-subsidy-credit → decide-consumption →
    /// insert-order-items sequence, the same way Credit Notes are serialized.
    /// </summary>
    /// <returns>False if any lock in the chain could not be acquired (caller should treat this as
    /// "try again" — nothing in <paramref name="action"/> ran).</returns>
    protected virtual async Task<bool> RunWithPackNameLocksAsync(IEnumerable<int> packNameIds, Func<Task> action)
    {
        var orderedIds = packNameIds.Distinct().OrderBy(id => id).ToList();

        if (orderedIds.Count == 0)
        {
            await action();
            return true;
        }

        async Task<bool> LockNextAsync(int index)
        {
            if (index >= orderedIds.Count)
            {
                await action();
                return true;
            }

            //The locker takes a Func<Task>, so the inner result is kept here: a lock refused further down the chain
            //means the action did not run, and the whole call has to say so.
            var innerAcquired = false;

            var acquired = await _locker.PerformActionWithLockAsync(
                $"subsidy_apply_packname_{orderedIds[index]}",
                TimeSpan.FromSeconds(30),
                async () => innerAcquired = await LockNextAsync(index + 1));

            return acquired && innerAcquired;
        }

        return await LockNextAsync(0);
    }

    /// <summary>
    /// Move shopping cart items to order items
    /// </summary>
    /// <param name="details">Place order container</param>
    /// <param name="order">Order</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    protected virtual async Task MovePackNameShoppingCartItemsToOrderItemsAsync(PlacePackNameOrderContainer details, Order order)
    {
        var packNameIds = details.PackNamesCart.Select(item => item.PackNameId);

        var lockAcquired = await RunWithPackNameLocksAsync(packNameIds, async () =>
        {
            // Subsidy rows are keyed by OrderItemId, so nothing settles until every order item exists.
            var orderItemIdByCartItemId = new Dictionary<int, int>();

            foreach (var sc in details.PackNamesCart)
            {
                var product = await _productService.GetProductByIdAsync(sc.ProductId);

                //prices
                var scUnitPrice = (await _shoppingCartService.GetUnitPriceAsync(sc, true)).unitPrice;
                var (scSubTotal, discountAmount, scDiscounts, _) = await _shoppingCartService.GetSubTotalAsync(sc, true);
                var scUnitPriceInclTax = await _taxService.GetProductPriceAsync(product, scUnitPrice, true, details.Customer);
                var scUnitPriceExclTax = await _taxService.GetProductPriceAsync(product, scUnitPrice, false, details.Customer);
                var scSubTotalInclTax = await _taxService.GetProductPriceAsync(product, scSubTotal, true, details.Customer);
                var scSubTotalExclTax = await _taxService.GetProductPriceAsync(product, scSubTotal, false, details.Customer);
                var discountAmountInclTax = await _taxService.GetProductPriceAsync(product, discountAmount, true, details.Customer);
                var discountAmountExclTax = await _taxService.GetProductPriceAsync(product, discountAmount, false, details.Customer);
                foreach (var disc in scDiscounts)
                    if (!_discountService.ContainsDiscount(details.AppliedDiscounts, disc))
                        details.AppliedDiscounts.Add(disc);

                //attributes
                var store = await _storeService.GetStoreByIdAsync(sc.StoreId);
                var attributeDescription = await _productAttributeFormatter.FormatAttributesAsync(product, sc.AttributesXml, details.Customer, store);

                var itemWeight = await _shippingService.GetShoppingCartItemWeightAsync(sc);

                //save order item
                var orderItem = new OrderItem
                {
                    OrderItemGuid = Guid.NewGuid(),
                    OrderId = order.Id,
                    ProductId = product.Id,
                    UnitPriceInclTax = scUnitPriceInclTax.price,
                    UnitPriceExclTax = scUnitPriceExclTax.price,
                    PriceInclTax = scSubTotalInclTax.price,
                    PriceExclTax = scSubTotalExclTax.price,
                    OriginalProductCost = await _priceCalculationService.GetProductCostAsync(product, sc.AttributesXml),
                    AttributeDescription = attributeDescription,
                    AttributesXml = sc.AttributesXml,
                    Quantity = sc.Quantity,
                    DiscountAmountInclTax = discountAmountInclTax.price,
                    DiscountAmountExclTax = discountAmountExclTax.price,
                    DownloadCount = 0,
                    IsDownloadActivated = false,
                    LicenseDownloadId = 0,
                    ItemWeight = itemWeight,
                    RentalStartDateUtc = sc.RentalStartDateUtc,
                    RentalEndDateUtc = sc.RentalEndDateUtc
                };

                await _orderService.InsertOrderItemAsync(orderItem);

                var packNameOrderItem = new PackNameOrderItem
                {
                    OrderItemId = orderItem.Id,
                    PackNameId = sc.PackNameId,
                    PackName = sc.Name,
                    BranchId = sc.BranchId,
                    BranchCode = sc.BranchCode,
                    Branch = sc.Branch,
                    DepartmentId = sc.DepartmentId,
                    Department = sc.Department,
                    CostCenterId = sc.CostCenterId,
                    CostCenter = sc.CostCenter,
                    PackNameCustomerId = sc.PackNameCustomerId
                };

                await _cwOrderService.InsertPackNameOrderItemAsync(packNameOrderItem);

                orderItemIdByCartItemId[sc.Id] = orderItem.Id;

                //gift cards
                await AddGiftCardsAsync(product, sc.AttributesXml, sc.Quantity, orderItem, scUnitPriceExclTax.price);

                //inventory
                await _productService.AdjustInventoryAsync(product, -sc.Quantity, sc.AttributesXml,
                    string.Format(await _localizationService.GetResourceAsync("Admin.StockQuantityHistory.Messages.PlaceOrder"), order.Id));

                await _eventPublisher.PublishAsync(new ShoppingCartItemMovedToOrderItemEvent(sc, orderItem));
            }

            // Settle: subsidy first, then credit notes on the remainder — the same pipeline, in the same
            // order, that priced the cart this customer just checked out. Inside the PackName locks, so
            // the read-available-credit → decide → insert sequence stays serialized.
            await SettleOrderAsync(order, details.SettleableTotal, details.PackNamesCart, orderItemIdByCartItemId);
        });

        if (!lockAcquired)
            throw new NopException($"Could not place order: another operation is already applying subsidy credit for one of these PackNames ({string.Join(",", packNameIds.Distinct())}). Please try again.");

        // Delete only the rows that became order items. details.PackNamesCart comes from the
        // PackNameShoppingCartItem VIEW, which inner-joins the packname cart link, the packname, its
        // list, its branch and SubsidyPackNameCanOrder, and filters CanOrder = 1. A cart row with no
        // packname link, or one whose packname can no longer order, is therefore NOT in this order -
        // and ClearShoppingCartAsync deleted it anyway, silently losing a line the customer never
        // bought. Deleting exactly what was ordered is the same scoping rule the return-order path uses.
        //
        // The id overload is deliberate: sc is a PackNameShoppingCartItem, a view entity, so passing
        // it to the entity overload would aim the delete at the view rather than at ShoppingCartItem.
        // resetCheckoutData is false because placeOrder calls ResetCheckoutDataAsync straight after,
        // with clearCouponCodes and clearCheckoutAttributes.
        foreach (var sc in details.PackNamesCart)
            await _shoppingCartService.DeleteShoppingCartItemAsync(sc.Id, resetCheckoutData: false);
    }

    /// <summary>
    /// Clears the selected shipping address attributes for the specified order, only if the store 
    /// settings indicate that pre-selection of shipping addresses is disabled.
    /// </summary>
    /// <remarks>This method checks the store settings associated with the packNames in the
    /// provided order. If the setting to disable pre-selection of shipping addresses is enabled,
    /// the selected shipping address attributes for the corresponding packNames are cleared for the specified
    /// customer.</remarks>
    /// <param name="order">Order to process.</param>
    /// <returns></returns>
    protected async Task ClearOrderSelectedShippingAddresses(Order order)
    {
        //We need to check if there is shipping data for packnames
        var orderShippings = await _cwOrderService.GetOrderShippingAsync(order.Id);

        //If there is shipping data it is an order for packanems, then, we need to check if we have to clear the selected shipping address attribute
        if (orderShippings.Any())
        {
            var customer = await _customerService.GetCustomerByIdAsync(order.CustomerId);

            //Get the packNames
            var packNameIds = orderShippings.Where(x => x.PackNameId > 0).Select(x => x.PackNameId.Value).Distinct().ToList();
            if (packNameIds.Any())
            {
                var storeSettings = await _settingService.LoadSettingAsync<MultiStoreSettings>(order.StoreId);

                //We clear the selected shipping address if the setting is enabled
                if (storeSettings.DoNotPreSelectShippingAddress)
                {
                    var assignments = await _shippingAssignmentService.LoadAsync(customer, order.StoreId);

                    foreach (var packNameId in packNameIds)
                        assignments.SetAddressId(packNameId, null);

                    await _shippingAssignmentService.SaveAsync(customer, order.StoreId, assignments);
                }
            }
        }
    }

    /// <param name="packNameCart">
    /// The packname cart being settled, or empty to settle credit notes only. An auto-placed exchange
    /// order (PlaceReturnOrderAsync) passes the returned item's packname, so the restored entitlement pays
    /// for the replacement.
    /// </param>
    /// <param name="shippingPrices">
    /// Per-address shipping prices, or null to compute them from <paramref name="packNameCart"/>. Pass an
    /// empty list for a return-derived cart: it has no selected shipping option to rate, and an exchange's
    /// delivery is charged as the redelivery FEE, which settlement never pays.
    /// </param>
    protected virtual async Task<SettlementPlan> SettleOrderAsync(Order order,
        decimal settleableAmount,
        IList<PackNameShoppingCartItem> packNameCart,
        IReadOnlyDictionary<int, int> orderItemIdByCartItemId,
        IList<PackNameShippingPrice> shippingPrices = null)
    {
        ArgumentNullException.ThrowIfNull(order);

        // Cheap idempotency guard: settlement writes insert-only rows, so their presence is the fact.
        if (await _cwOrderService.GetOrderSettlementAsync(order.Id) != null
            || (await _creditNotesService.GetCreditNoteHistoriesByOrderIdAsync(order.Id)).Any()
            || (await _subsidyCreditService.GetSubsidyOrderItemsByOrderIdAsync(order.Id)).Any())
            return new SettlementPlan();

        //The credit is read again here, inside the packname lock. What the quote and the checks read before the
        //lock was taken may already be spent by another order for the same packname.
        _subsidyCreditService.ClearPackNameCredits();

        var customer = await _customerService.GetCustomerByIdAsync(order.CustomerId);

        if (customer == null)
            return new SettlementPlan();

        shippingPrices ??= packNameCart.Count == 0
            ? []
            : (await _orderTotalCalculationService.GetShoppingCartShippingTotalAsync(packNameCart, false)).shippingTotal;

        var context = new SettlementContext
        {
            Customer = customer,
            StoreId = order.StoreId,
            PackNameCart = packNameCart,
            ShippingPrices = shippingPrices,
            Order = order,
            OrderItemIdByCartItemId = orderItemIdByCartItemId,
        };

        var plan = await _settlementPipeline.ApplyAsync(context, settleableAmount);

        // The label is a projection of what the pipeline settled — derived here, where the plan is, so
        // there is exactly one place that decides it and nothing has to read the ledgers back.
        await ApplySettlementPaymentMethodAsync(order, plan);

        // The order's settlement row: what the pipeline was offered and what each pool covered, kept once
        // so that no screen, e-mail or document has to derive it again from the ledgers. Written only when
        // something was covered; an order nothing paid for has nothing to show.
        var settlement = OrderSettlementHelper.FromPlan(order.Id, plan, DateTime.UtcNow);

        if (settlement != null)
            await _cwOrderService.InsertOrderSettlementAsync(settlement);

        return plan;
    }

    /// <summary>
    /// Records how an order was settled on the order itself, so the admin grid and reports can show
    /// "paid by credit note / subsidy" without re-deriving it from the ledgers.
    /// </summary>
    /// <remarks>
    /// Only ever fills in a BLANK payment method. If the customer chose a real payment method, that is
    /// what the order was paid with and it wins — settlement merely reduced what they had to pay.
    /// </remarks>
    protected virtual async Task ApplySettlementPaymentMethodAsync(Order order, SettlementPlan plan)
    {
        ArgumentNullException.ThrowIfNull(order);
        ArgumentNullException.ThrowIfNull(plan);

        if (!string.IsNullOrEmpty(order.PaymentMethodSystemName))
            return;

        var paidByCreditNotes = plan.AmountFor(SettlementSourceNames.CREDIT_NOTES) > decimal.Zero;
        var paidBySubsidy = plan.AmountFor(SettlementSourceNames.SUBSIDY) > decimal.Zero;

        var paymentMethodSystemName = (paidByCreditNotes, paidBySubsidy) switch
        {
            (true, true) => CWPaymentMethodSystemNames.SUBSIDY_AND_CREDIT_NOTE,
            (true, false) => CWPaymentMethodSystemNames.CREDIT_NOTE,
            (false, true) => CWPaymentMethodSystemNames.SUBSIDY,
            _ => null,
        };

        if (paymentMethodSystemName == null)
            return;

        order.PaymentMethodSystemName = paymentMethodSystemName;
        await _orderService.UpdateOrderAsync(order);
    }

    /// <summary>
    /// Picks the address to bill a customer that has no billing address of record: an address whose email,
    /// then first name, then last name matches the customer, otherwise the most recently created address,
    /// otherwise their shipping address. Mirrors PackNameCheckoutModelFactory.GetCustomerDefaultAddress so the
    /// order is billed to the address the checkout screen actually displayed.
    /// </summary>
    /// <param name="customer">Customer</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the address to bill, or null when the customer has none
    /// </returns>
    protected virtual async Task<Address> ResolveCustomerBillingAddressAsync(Customer customer)
    {
        var addresses = (await _customerService.GetAddressesByCustomerIdAsync(customer.Id))
            .OrderByDescending(address => address.CreatedOnUtc)
            .ThenByDescending(address => address.Id)
            .ToList();

        if (addresses.Count == 0)
        {
            return customer.ShippingAddressId.HasValue
                ? await _addressService.GetAddressByIdAsync(customer.ShippingAddressId.Value)
                : null;
        }

        var candidates = addresses
            .Where(address => string.Equals(address.Email, customer.Email, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (candidates.Count == 0)
            candidates = addresses;

        if (candidates.Count > 1)
        {
            var byFirstName = candidates
                .Where(address => string.Equals(address.FirstName, customer.FirstName, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (byFirstName.Count > 0)
                candidates = byFirstName;
        }

        if (candidates.Count > 1)
        {
            var byLastName = candidates
                .Where(address => string.Equals(address.LastName, customer.LastName, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (byLastName.Count > 0)
                candidates = byLastName;
        }

        return candidates.First();
    }

    /// <summary>
    /// Whether this customer's shopping cart holds any line associated with a packname, in the raw
    /// <see cref="PackNameCartItem"/> rows — unfiltered by ordering period, CanOrder or store
    /// settings.
    /// </summary>
    /// <remarks>
    /// Kept separate from the cart readers on <c>IShoppingCartOverrideService</c> because the question
    /// is different: those answer "what is in the cart", which is a display question and is the one
    /// the cart readers are meant to converge on. This answers "is this a packname cart at all", which is a safety
    /// question and must stay true even when every line is currently unorderable.
    /// </remarks>
    protected virtual async Task<bool> HasPackNameCartItemsAsync(int customerId, int storeId)
    {
        return await (from pnci in _packNameCartItemRepository.Table
                      join sci in _sciRepository.Table on pnci.ShoppingCartItemId equals sci.Id
                      where sci.CustomerId == customerId
                            && sci.ShoppingCartTypeId == (int)ShoppingCartType.ShoppingCart
                            && (storeId == 0 || sci.StoreId == storeId)
                      select pnci.ShoppingCartItemId).AnyAsync();
    }

    /// <summary>
    /// Whether any item on an order is a packname item.
    /// </summary>
    protected virtual async Task<bool> HasPackNameOrderItemsAsync(int orderId)
    {
        foreach (var orderItem in await _orderService.GetOrderItemsAsync(orderId))
        {
            if (await _cwOrderService.GetPackNameOrderItemAsync(orderItem.Id) != null)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Places a packname cart that reached <see cref="PlaceOrderAsync"/> — a payment method placing its own
    /// order — as a packname order.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Such a method has taken, or is about to take, the payment for the cart nop's readers see
    /// (<c>GetShoppingCartAsync</c>), while the packname order holds the packname cart. The two must be the same
    /// lines, or the customer pays for lines the order does not contain. So the placement is refused, before any
    /// payment is captured, when a line of the cart is not in the packname cart (a line of no packname, or one
    /// the packname checkout cannot order), and when no packname line can be ordered at all.
    /// </para>
    /// <para>
    /// A method that states what it charged (<c>ProcessPaymentRequest.OrderTotal</c> set before placement) must
    /// have charged the cart's total, payment fee excluded. PayPal Commerce does not state it; it compares its
    /// approved amount with the same cart total itself before calling here.
    /// </para>
    /// </remarks>
    /// <param name="processPaymentRequest">Process payment request</param>
    /// <param name="customer">Customer</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the place order result
    /// </returns>
    protected virtual async Task<PlaceOrderResult> PlaceDelegatedPackNameOrderAsync(ProcessPaymentRequest processPaymentRequest, Customer customer)
    {
        async Task<PlaceOrderResult> refuseAsync(string resourceKey) => new PlaceOrderResult
        {
            Errors = [await _localizationService.GetResourceAsync(resourceKey)]
        };

        var packNameCart = await _shoppingCartService.GetPackNameShoppingCartItemsAsync(customer, 0, processPaymentRequest.StoreId);

        if (packNameCart.Count == 0)
            return await refuseAsync("Plugins.CW.Core.Order.PackNameCartNotOrderable");

        var packNameItemIds = packNameCart.Select(item => item.Id).ToHashSet();
        var cart = await _shoppingCartService.GetShoppingCartAsync(customer, ShoppingCartType.ShoppingCart, processPaymentRequest.StoreId);

        if (cart.Any(item => !packNameItemIds.Contains(item.Id)))
            return await refuseAsync("Plugins.CW.Core.Order.PackNameCartHasOtherItems");

        if (processPaymentRequest.OrderTotal > decimal.Zero)
        {
            var (cartTotal, _, _, _, _, _) = await _orderTotalCalculationService.GetShoppingCartTotalAsync(cart, usePaymentMethodAdditionalFee: false);

            if (!cartTotal.HasValue || Math.Round(cartTotal.Value, 2) != Math.Round(processPaymentRequest.OrderTotal, 2))
                return await refuseAsync("Plugins.CW.Core.Order.PaymentAmountDiffers");
        }

        return await PlacePackNameOrderAsync(processPaymentRequest);
    }

    #endregion

    #region Overrides

    /// <summary>
    /// Resolves the billing address for the order. nopCommerce only looks at <see cref="Customer.BillingAddressId"/>,
    /// which is never set for a customer who has not been through the stock one-page checkout, so a first-time
    /// packName customer could not place an order even with a valid address on file and shown on the checkout
    /// screen. Fall back to the customer's own addresses — the same precedence the checkout screen uses to pick
    /// the address it displays — and persist the resolved address on the customer so the state self-heals.
    /// </summary>
    /// <param name="details">PlaceOrder container</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    protected override async Task PrepareAndValidateBillingAddressAsync(PlaceOrderContainer details)
    {
        if (details.Customer.BillingAddressId is null)
        {
            var resolvedAddress = await ResolveCustomerBillingAddressAsync(details.Customer);

            if (resolvedAddress is null)
                throw new NopException("Billing address is not provided");

            details.Customer.BillingAddressId = resolvedAddress.Id;
            await _customerService.UpdateCustomerAsync(details.Customer);
        }

        await base.PrepareAndValidateBillingAddressAsync(details);
    }

    /// <summary>
    /// Places an order and settles it: a packname order when the cart carries packname lines, otherwise a plain
    /// nop order. For a plain order the pre-settlement total has to be captured before the base call, because
    /// the base clears the cart.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>This method is the single chokepoint for every placement that is not the packname
    /// checkout's own.</b> <c>NopStartup</c> registers <c>IOrderProcessingService</c> as this class
    /// application-wide, so nop's <c>CheckoutController</c>, PayPal Commerce's own public controller
    /// (<c>PayPalCommerceServiceManager.PlaceOrderAsync</c> calls <c>_orderProcessingService</c>) and
    /// anything added later all arrive here.
    /// </para>
    /// <para>
    /// <b>A cart carrying packname lines is placed as a packname order</b>
    /// (<see cref="PlaceDelegatedPackNameOrderAsync"/>), whoever asked for it. nop's placement would settle it
    /// with <c>packNameCart: []</c> — no <c>PackNameOrderItem</c> rows, no per-packname <c>OrderShipping</c>
    /// destinations, no subsidy consumed. So any payment method that places its own order, today's or a future
    /// one, gets a correct packname order without being modified.
    /// </para>
    /// <para>
    /// <b>The check reads the raw PackNameCartItem rows on purpose.</b>
    /// <c>GetPackNameShoppingCartItemsAsync</c> reads the CanOrder-filtered view, so a packname whose ordering
    /// period has closed returns zero rows and its lines would fall through to a plain nop order. Read raw, such
    /// a cart still goes to the packname placement, which refuses it.
    /// </para>
    /// </remarks>
    public override async Task<PlaceOrderResult> PlaceOrderAsync(ProcessPaymentRequest processPaymentRequest)
    {
        ArgumentNullException.ThrowIfNull(processPaymentRequest);

        var settleableTotal = decimal.Zero;

        var customer = await _customerService.GetCustomerByIdAsync(processPaymentRequest.CustomerId);

        if (customer != null)
        {
            //Gated on the feature: with packnames off for this store the lines are plain nop lines.
            if (await _cwFeatureService.IsEnabledAsync(CWFeature.PackName, processPaymentRequest.StoreId)
                && await HasPackNameCartItemsAsync(customer.Id, processPaymentRequest.StoreId))
            {
                return await PlaceDelegatedPackNameOrderAsync(processPaymentRequest, customer);
            }

            var cart = await _shoppingCartService.GetShoppingCartAsync(customer, ShoppingCartType.ShoppingCart, processPaymentRequest.StoreId);
            settleableTotal = (await _orderTotalCalculationService.QuoteCartSettlementAsync(cart)).SettleableAmount;
        }

        var result = await base.PlaceOrderAsync(processPaymentRequest);

        if (!result.Success || result.PlacedOrder is null)
            return result;

        // No live cart means no quote was possible — a plugin that places its own order can reach
        // PlaceOrderAsync with an empty cart. In that case nothing has been netted off, so the order's
        // own total IS the settleable amount. Without this fallback such an order would settle nothing
        // at all, where the old OrderPlacedEvent handler would have applied credit notes.
        if (settleableTotal <= decimal.Zero)
            settleableTotal = result.PlacedOrder.OrderTotal;

        if (settleableTotal > decimal.Zero)
            await SettleOrderAsync(result.PlacedOrder, settleableTotal, packNameCart: [], orderItemIdByCartItemId: new Dictionary<int, int>());

        return result;
    }

    /// <summary>
    /// Refuses to process the next recurring payment of an order that carries packname items.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the third door into order creation and the only one that opens without
    /// <see cref="PlaceOrderAsync"/>: nop's implementation writes the order with
    /// <c>SaveOrderDetailsAsync</c> and clones the initial order's items, so neither the checkout
    /// filter (it lives on <c>OrderController</c>) nor the placement guard sees it. The customer can
    /// reach it themselves with <c>POST /customer/recurring-payments</c> and a
    /// <c>retryLastPayment_{id}</c> key, and the scheduled task reaches it unattended.
    /// </para>
    /// <para>
    /// It matters because the packname checkout creates recurring payments of its own
    /// (<see cref="PlacePackNameOrderAsync"/> calls <c>CreateFirstRecurringPaymentAsync</c>). Without
    /// this, the second cycle of a packname recurring order is a paid order with no
    /// <c>PackNameOrderItem</c>, no per-packname destinations and no subsidy consumed.
    /// Recurring packname orders are not supported yet; until then they are refused
    /// rather than placed wrong.
    /// </para>
    /// </remarks>
    public override async Task<IEnumerable<string>> ProcessNextRecurringPaymentAsync(RecurringPayment recurringPayment, ProcessPaymentResult paymentResult = null)
    {
        ArgumentNullException.ThrowIfNull(recurringPayment);

        var initialOrder = await _orderService.GetOrderByIdAsync(recurringPayment.InitialOrderId);

        if (initialOrder != null
            && await _cwFeatureService.IsEnabledAsync(CWFeature.PackName, initialOrder.StoreId)
            && await HasPackNameOrderItemsAsync(initialOrder.Id))
        {
            return [await _localizationService.GetResourceAsync("Plugins.CW.Core.Order.PackNameOrderMustUsePackNameCheckout")];
        }

        return await base.ProcessNextRecurringPaymentAsync(recurringPayment, paymentResult);
    }

    /// <summary>
    /// Place order items in current user shopping cart.
    /// </summary>
    /// <param name="order">The order</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    public override async Task<IList<string>> ReOrderAsync(Order order)
    {
        ArgumentNullException.ThrowIfNull(order);

        var customer = await _customerService.GetCustomerByIdAsync(order.CustomerId);

        //With packnames switched off for the order's store, a re-order is an ordinary nop re-order: the old
        //packname lines go back as plain lines, and no packname rule may skip or redirect them.
        var packNamesEnabled = await _cwFeatureService.IsEnabledAsync(CWFeature.PackName, order.StoreId);

        //Who this customer may still order for, asked of the same ownership rule every guard uses.
        var ownPackName = await _packNameService.GetPackNameByCustomerIdAsync(customer.Id);

        var warnings = new List<string>();

        //move shopping cart items (if possible)
        foreach (var orderItem in await _orderService.GetOrderItemsAsync(order.Id))
        {
            var product = await _productService.GetProductByIdAsync(orderItem.ProductId);
            var packNameOrderItem = await _cwOrderService.GetPackNameOrderItemAsync(orderItem.Id);

            if (packNamesEnabled && packNameOrderItem != null)
            {
                //A line for a packname this customer no longer orders for is skipped, never moved: another
                //packname would pay for someone else's items out of its own subsidy.
                if (!await mayOrderForAsync(packNameOrderItem.PackNameId))
                {
                    warnings.Add(string.Format(
                        await _localizationService.GetResourceAsync("Plugins.CW.Core.ReOrder.PackNameNoLongerManaged"),
                        packNameOrderItem.PackName));

                    continue;
                }

                warnings.AddRange(await _shoppingCartService.AddToCartAsync(customer, product,
                    ShoppingCartType.ShoppingCart, order.StoreId, packNameId: packNameOrderItem.PackNameId,
                    orderItem.AttributesXml,
                    _taxSettings.PricesIncludeTax ? orderItem.UnitPriceInclTax : orderItem.UnitPriceExclTax,
                    orderItem.RentalStartDateUtc, orderItem.RentalEndDateUtc,
                    orderItem.Quantity, false));
            }
            else
            {
                warnings.AddRange(await _shoppingCartService.AddToCartAsync(customer, product,
                    ShoppingCartType.ShoppingCart, order.StoreId,
                    orderItem.AttributesXml,
                    _taxSettings.PricesIncludeTax ? orderItem.UnitPriceInclTax : orderItem.UnitPriceExclTax,
                    orderItem.RentalStartDateUtc, orderItem.RentalEndDateUtc,
                    orderItem.Quantity, false));
            }
        }

        //set checkout attributes
        //comment the code below if you want to disable this functionality
        await _genericAttributeService.SaveAttributeAsync(customer, NopCustomerDefaults.CheckoutAttributes, order.CheckoutAttributesXml, order.StoreId);

        return warnings;

        //Their own packname, or one they still manage.
        async Task<bool> mayOrderForAsync(int packNameId)
        {
            if (ownPackName == null)
                return false;

            if (ownPackName.Id == packNameId)
                return true;

            return ownPackName.Manager && await _multiStoreService.IsPackNameOwnedByManagerId(ownPackName.Id, packNameId);
        }
    }

    #endregion

    #region Methods

    #region Exchange/Return Requests

    /// <summary>
    /// Quotes what would settle a return-derived exchange order, without placing anything.
    /// </summary>
    /// <remarks>
    /// This is what decides the pathway. It walks the same pipeline, against the same packname cart and
    /// the same detached template, that <see cref="PlaceReturnOrderAsync"/> would settle — so a plan with
    /// <c>BalanceDue == 0</c> is a promise that placing the order will cost the customer nothing.
    /// Goods only: fees are added after settlement and are the caller's to test.
    /// </remarks>
    public virtual async Task<SettlementPlan> QuoteReturnOrderSettlementAsync(ReturnRequest returnRequest, ReturnRequestItem returnRequestItem)
    {
        ArgumentNullException.ThrowIfNull(returnRequest);
        ArgumentNullException.ThrowIfNull(returnRequestItem);

        //Same reason PlaceReturnOrderAsync opens one: everything below prices a DETACHED template, so the
        //price consumer has no cart row to follow and reads the scope instead. Without it an Exact
        //exchange quotes at today's qty-1 price and the pathway decision is made on the wrong number.
        using var replacementScope = ExchangeReplacementScope.Begin(returnRequestItem);

        var customer = await _customerService.GetCustomerByIdAsync(returnRequest.CustomerId);

        if (customer == null)
            return new SettlementPlan();

        var cart = await _exchangeService.BuildProductsListForReturnOrderAsync(returnRequest, returnRequestItem);

        if (cart.Count == 0)
            return new SettlementPlan();

        var settleableTotal = (await _orderTotalCalculationService.QuoteCartSettlementAsync(cart)).SettleableAmount;

        if (settleableTotal <= decimal.Zero)
            return new SettlementPlan();

        var context = new SettlementContext
        {
            Customer = customer,
            StoreId = returnRequest.StoreId,
            PackNameCart = await BuildReturnOrderPackNameCartAsync(cart, returnRequestItem),

            //Empty, not computed: a return-derived cart has no selected shipping option to rate, and an
            //exchange's delivery is charged as the redelivery fee, which settlement never pays.
            ShippingPrices = [],

            //No Order: this is the quote path. SubsidySettlementSource falls back to the store id.
        };

        return await _settlementPipeline.QuoteAsync(context, settleableTotal);
    }

    /// <summary>
    /// Places an Order which derivates from a Return Request
    /// This method was taken from the base class and customized a bit to adjust it for Exchange
    /// </summary>
    /// <param name="returnRequest"></param>
    /// <param name="returnRequestItem"></param>
    /// <returns></returns>
    public virtual async Task<PlaceOrderResult> PlaceReturnOrderAsync(ReturnRequest returnRequest, ReturnRequestItem returnRequestItem)
    {
        var result = new PlaceOrderResult();

        var customer = await _customerService.GetCustomerByIdAsync(returnRequest.CustomerId);

        try
        {
            //Everything in here prices a DETACHED cart (BuildProductsListForReturnOrderAsync returns
            //templates with Id 0), so the price consumer has no cart row to follow and reads the scope
            //instead. Covers the settlement quote, the order totals and MoveShoppingCartItemsToOrderItemsAsync,
            //which is what freezes UnitPriceInclTax onto the new order item.
            using var replacementScope = ExchangeReplacementScope.Begin(returnRequestItem);

            //prepare order details
            var details = await PrepareReturnOrderDetails(returnRequest, returnRequestItem);

            // OrderTotal is the payable balance; the pipeline needs the pre-settlement figure, and the
            // cart is still intact at this point (MoveShoppingCartItemsToOrderItemsAsync clears it).
            var settleableTotal = (await _orderTotalCalculationService.QuoteCartSettlementAsync(details.Cart)).SettleableAmount;

            var order = await SaveReturnOrderDetailsAsync(returnRequest, returnRequestItem, details);
            result.PlacedOrder = order;

            //move shopping cart items to order items
            var orderItemIdByCartItemId = await MoveShoppingCartItemsToOrderItemsAsync(details, order, returnRequestItem);

            //discount usage history
            await SaveDiscountUsageHistoryAsync(details, order);

            //gift card usage history
            await SaveGiftCardUsageHistoryAsync(details, order);

            // Settle with the returned item's packname: the entitlement restored by this very approval is
            // the natural payer for the replacement, and ExecuteExchangeWorkflowAsync has
            // already quoted this exact combination to decide that the order could be placed at all.
            // Empty for a non-packname original, which keeps credit notes as the only payer there.
            //
            // shippingPrices is explicitly empty for the reason given on SettleOrderAsync.
            //
            // Under the same packname locks as a checkout, so this order and a checkout for the same packname
            // cannot both spend the credit that is left. The lock is refused, not waited for, while another order
            // holds it, so it is tried again for a while. The order is already placed and marked paid, so if the
            // lock never comes it is settled anyway and the race is logged, which is what happened before.
            var returnPackNameCart = await BuildReturnOrderPackNameCartAsync(details.Cart, returnRequestItem);
            var settlementPlan = new SettlementPlan();
            var settled = false;

            for (var attempt = 1; attempt <= ReturnOrderSettleLockAttempts && !settled; attempt++)
            {
                //Set only when the settlement returned: nop's memory locker swallows a cancellation and reports the
                //action as run.
                var settlementReturned = false;

                settled = await RunWithPackNameLocksAsync(returnPackNameCart.Select(item => item.PackNameId), async () =>
                {
                    settlementPlan = await SettleOrderAsync(order, settleableTotal, returnPackNameCart, orderItemIdByCartItemId, shippingPrices: []);
                    settlementReturned = true;
                });

                //The order is already placed and paid, so it is settled below without the lock rather than left unsettled
                if (settled && !settlementReturned)
                {
                    await _logger.ErrorAsync($"[Settlement] Exchange order {order.Id} (return request {returnRequest.Id}): the settlement under the packname lock was cancelled; it is settled again without the lock.");
                    settled = false;
                    break;
                }

                if (!settled && attempt < ReturnOrderSettleLockAttempts)
                    await Task.Delay(ReturnOrderSettleLockDelay);
            }

            if (!settled)
            {
                await _logger.WarningAsync($"[Settlement] Exchange order {order.Id} (return request {returnRequest.Id}) settled without the packname lock (held by another order for about {ReturnOrderSettleLockAttempts * ReturnOrderSettleLockDelay.TotalSeconds:0} seconds, or cancelled).");

                settlementPlan = await SettleOrderAsync(order, settleableTotal, returnPackNameCart, orderItemIdByCartItemId, shippingPrices: []);
            }

            // PrepareReturnOrderDetails took the order total from GetShoppingCartTotalAsync, which nets off
            // a settlement quote — and that quote cannot see this packname, because it is built from the
            // customer's SAVED cart and this replacement was never in one. So the figure it produced is the
            // pre-subsidy one. Correct it from the plan that was actually applied, which is the only thing
            // that knows what this order really cost.
            //
            // Guarded on SettleableAmount: SettleOrderAsync returns an EMPTY plan when it declines to settle
            // — the idempotency guard on a re-entered order, or a customer that could not be loaded — and an
            // empty plan reports BalanceDue 0, which would zero a total that is genuinely owed.
            if (settlementPlan.SettleableAmount > decimal.Zero && order.OrderTotal != settlementPlan.BalanceDue)
            {
                order.OrderTotal = settlementPlan.BalanceDue;

                await _orderService.UpdateOrderAsync(order);
            }

            // This order is only ever placed because ExecuteExchangeWorkflowAsync quoted it as fully
            // covered, and SaveReturnOrderDetailsAsync marks it Paid unconditionally. So a balance here
            // means the quote and the apply disagreed — entitlement spent by another order in between is
            // the way that happens — and the customer will never be asked for it. Not recoverable from
            // here without cancelling an order that is already placed, so record it loudly instead of
            // letting it pass as a paid order that nothing paid for.
            if (settlementPlan.SettleableAmount > decimal.Zero && settlementPlan.BalanceDue > decimal.Zero)
            {
                await _logger.ErrorAsync($"[Settlement] Auto-placed exchange order {order.Id} (return request {returnRequest.Id}) was quoted as fully covered but settled with {settlementPlan.BalanceDue:0.00} still due. The order is marked paid and the balance will not be collected.");
            }

            //notifications
            await SendNotificationsAndSaveNotesAsync(order);

            // check order status
            await CheckOrderStatusAsync(order);

            // add notes about the return request
            await AnnotateOrderWithReturnRequestAsync(order, returnRequest);

            // Assign custom values related to the Return Request
            await AssignReturnRequestCustomValuesAsync(order, returnRequest);

            //raise event       
            await _eventPublisher.PublishAsync(new OrderPlacedEvent(order));

            if (order.PaymentStatus == PaymentStatus.Paid)
                await ProcessOrderPaidAsync(order);
        }
        catch (Exception exc)
        {
            await _logger.ErrorAsync(exc.Message, exc);
            result.AddError(exc.Message);
        }

        #region Process errors

        if (!result.Success)
        {
            //log errors
            var logError = result.Errors.Aggregate("Error while placing order for Return Request. ",
                (current, next) => string.Format("{0}Error {1}: {2}. ", current, result.Errors.IndexOf(next) + 1, next));
            await _logger.ErrorAsync(logError, customer: customer);
        }

        #endregion

        return result;
    }

    #endregion

    #region PackNames

    /// <summary>
    /// Places the customer's packname order: every orderable packname line of the store, with a destination per
    /// packname and the subsidy and credit notes settled.
    /// </summary>
    /// <remarks>
    /// The cart is read here, from the packname cart view, whoever calls: the packname checkout, or
    /// <see cref="PlaceOrderAsync"/> when a payment method places its own order.
    /// </remarks>
    /// <param name="processPaymentRequest">Process payment request</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the place order result
    /// </returns>
    public virtual async Task<PlaceOrderResult> PlacePackNameOrderAsync(ProcessPaymentRequest processPaymentRequest)
    {
        ArgumentNullException.ThrowIfNull(processPaymentRequest);

        if (processPaymentRequest.OrderGuid == Guid.Empty)
            throw new Exception("Order GUID is not generated");

        //prepare order details
        var details = await PreparePlacePackNameOrderDetailsAsync(processPaymentRequest);

        async Task<PlaceOrderResult> placeOrder(PlacePackNameOrderContainer placeOrderContainer)
        {
            var result = new PlaceOrderResult();
            try
            {
                var processPaymentResult =
                    await GetProcessPaymentResultAsync(processPaymentRequest, placeOrderContainer)
                    ?? throw new NopException("processPaymentResult is not available");

                if (processPaymentResult.Success)
                {
                    var order = await SavePackNameOrderDetailsAsync(processPaymentRequest, processPaymentResult, placeOrderContainer);
                    result.PlacedOrder = order;

                    //move shopping cart items to order items
                    await MovePackNameShoppingCartItemsToOrderItemsAsync(placeOrderContainer, order);

                    //discount usage history
                    await SaveDiscountUsageHistoryAsync(placeOrderContainer, order);

                    //gift card usage history
                    await SaveGiftCardUsageHistoryAsync(placeOrderContainer, order);

                    //recurring orders
                    if (placeOrderContainer.IsRecurringShoppingCart)
                        await CreateFirstRecurringPaymentAsync(processPaymentRequest, order);

                    //If there is shipping data for packnames, we need to check if we have to clear the selected shipping address attribute
                    await ClearOrderSelectedShippingAddresses(order);

                    //notifications
                    await SendNotificationsAndSaveNotesAsync(order);

                    //reset checkout data
                    await _shoppingCartService.ResetCheckoutDataAsync(placeOrderContainer.Customer,
                        processPaymentRequest.StoreId, clearCouponCodes: true, clearCheckoutAttributes: true);
                    await _shoppingCartService.SetShoppingCartVendorAsync(placeOrderContainer.Customer, null, processPaymentRequest.StoreId);

                    await _customerActivityService.InsertActivityAsync("PublicStore.PlaceOrder",
                        string.Format(await _localizationService.GetResourceAsync("ActivityLog.PublicStore.PlaceOrder"),
                            order.Id), order);

                    //raise event       
                    await _eventPublisher.PublishAsync(new OrderPlacedEvent(order));

                    //check order status
                    await CheckOrderStatusAsync(order);

                    if (order.PaymentStatus == PaymentStatus.Paid)
                        await ProcessOrderPaidAsync(order);
                }
                else
                    foreach (var paymentError in processPaymentResult.Errors)
                        result.AddError(string.Format(await _localizationService.GetResourceAsync("Checkout.PaymentError"), paymentError));
            }
            catch (Exception exc)
            {
                await _logger.ErrorAsync(exc.Message, exc);
                result.AddError(exc.Message);
            }

            if (result.Success)
                return result;

            //log errors
            var logError = result.Errors.Aggregate("Error while placing order. ",
                (current, next) => $"{current}Error {result.Errors.IndexOf(next) + 1}: {next}. ");
            var customer = await _customerService.GetCustomerByIdAsync(processPaymentRequest.CustomerId);
            await _logger.ErrorAsync(logError, customer: customer);

            return result;
        }

        if (!_orderSettings.PlaceOrderWithLock)
            return await placeOrder(details);

        PlaceOrderResult result;
        var resource = details.Customer.Id.ToString();

        //the named mutex helps to avoid creating the same order in different threads,
        //and does not decrease performance significantly, because the code is blocked only for the specific cart.
        //you should be very careful, mutexes cannot be used in with the await operation
        //we can't use semaphore here, because it produces PlatformNotSupportedException exception on UNIX based systems
        using var mutex = new Mutex(false, resource);

        mutex.WaitOne();

        try
        {
            var cacheKey = _staticCacheManager.PrepareKey(NopOrderDefaults.OrderWithLockCacheKey, resource);
            cacheKey.CacheTime = _orderSettings.MinimumOrderPlacementInterval;

            var exist = _staticCacheManager.GetAsync(cacheKey, () => false).Result;

            if (exist)
            {
                result = new PlaceOrderResult();
                result.Errors.Add(_localizationService.GetResourceAsync("Checkout.MinOrderPlacementInterval").Result);
            }
            else
            {
                result = placeOrder(details).Result;

                if (result.Success)
                    _staticCacheManager.SetAsync(cacheKey, true).Wait();
            }
        }
        finally
        {
            mutex.ReleaseMutex();
        }

        return result;
    }

    /// <summary>
    /// Gives the customer the billing and shipping address of record that a payment method taking the payment
    /// in its own pages reads from the customer, before the customer is handed to it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// PayPal Commerce, at the payment step, refuses to start without <c>Customer.BillingAddressId</c> and
    /// <c>Customer.ShippingAddressId</c>, and shows that one shipping address to the buyer. A packname customer
    /// usually has neither: the packname checkout ships each packname to its own destination and bills the
    /// address the placement resolves.
    /// </para>
    /// <para>
    /// The billing address is the one placement would resolve (<see cref="ResolveCustomerBillingAddressAsync"/>),
    /// set only when there is none. The shipping address is the first packname's destination in cart order —
    /// the same destination the placed order records as its own shipping address. The order still ships every
    /// packname to its own destination.
    /// </para>
    /// </remarks>
    /// <param name="customer">Customer</param>
    /// <param name="storeId">Store identifier</param>
    /// <param name="packNameCart">The packname cart being paid</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    public virtual async Task PrepareCustomerPaymentAddressesAsync(Customer customer, int storeId, IList<PackNameShoppingCartItem> packNameCart)
    {
        ArgumentNullException.ThrowIfNull(customer);
        ArgumentNullException.ThrowIfNull(packNameCart);

        var changed = false;

        if (customer.BillingAddressId is null && await ResolveCustomerBillingAddressAsync(customer) is Address billingAddress)
        {
            customer.BillingAddressId = billingAddress.Id;
            changed = true;
        }

        var firstPackNameId = packNameCart.Select(item => item.PackNameId).FirstOrDefault();

        if (firstPackNameId > 0)
        {
            var assignments = await _shippingAssignmentService.LoadAsync(customer, storeId);

            if (assignments.GetAddressId(firstPackNameId) is int shippingAddressId && shippingAddressId > 0
                && customer.ShippingAddressId != shippingAddressId)
            {
                customer.ShippingAddressId = shippingAddressId;
                changed = true;
            }
        }

        if (changed)
            await _customerService.UpdateCustomerAsync(customer);
    }

    #endregion

    #endregion

    #region Nested classes

    /// <summary>
    /// PlaceOrder container
    /// </summary>
    protected partial class PlacePackNameOrderContainer : PlaceOrderContainer
    {
        public PlacePackNameOrderContainer()
        {
            PackNamesCart = new List<PackNameShoppingCartItem>();
            ShippingData = new List<ShippingDataContainer>();
        }

        public IList<PackNameShoppingCartItem> PackNamesCart { get; set; }

        public IList<ShippingDataContainer> ShippingData { get; set; }

        /// <summary>
        /// The order value before credit notes and subsidy are netted off, incl tax. OrderTotal is the
        /// payable balance *after* settlement, so it cannot be used to settle with.
        /// </summary>
        public decimal SettleableTotal { get; set; }
    }

    protected partial class ShippingDataContainer
    {
        /// <summary>
        /// Gets or sets the packName identifier
        /// </summary>
        public int PackNameId { get; set; }

        /// <summary>
        /// Gets or sets the packName
        /// </summary>
        public string PackName { get; set; }

        /// <summary>
        /// Gets or sets the company
        /// </summary>
        public string Store { get; set; }

        /// <summary>
        /// Shipping address
        /// </summary>
        public Address ShippingAddress { get; set; }

        /// <summary>
        /// Selected pickup address
        /// </summary>
        public Address PickupAddress { get; set; }

        /// <summary>
        /// Shipping status
        /// </summary>
        public ShippingStatus ShippingStatus { get; set; }

        /// <summary>
        /// Selected shipping method
        /// </summary>
        public string ShippingMethodName { get; set; }

        /// <summary>
        /// Shipping rate computation method system name
        /// </summary>
        public string ShippingRateComputationMethodSystemName { get; set; }

        /// <summary>
        /// Gets or sets the shipping (include tax)
        /// </summary>
        public decimal ShippingInclTax { get; set; }

        /// <summary>
        /// Gets or sets the shipping (exclude tax)
        /// </summary>
        public decimal ShippingExclTax { get; set; }
    }

    #endregion
}
