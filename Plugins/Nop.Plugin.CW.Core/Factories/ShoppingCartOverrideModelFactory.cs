using CW.Common.Helpers;
using Microsoft.AspNetCore.Http;
using Nop.Core;
using Nop.Core.Caching;
using Nop.Core.Domain.Catalog;
using Nop.Core.Domain.Common;
using Nop.Core.Domain.Customers;
using Nop.Core.Domain.Media;
using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Security;
using Nop.Core.Domain.Shipping;
using Nop.Core.Domain.Tax;
using Nop.Core.Domain.Vendors;
using Nop.Plugin.CW.Core.Domain.Orders.Views;
using Nop.Plugin.CW.Core.Domain.Settings;
using Nop.Plugin.CW.Core.Helpers;
using Nop.Plugin.CW.Core.Models.PackNameCheckout;
using Nop.Plugin.CW.Core.Models.PackNames;
using Nop.Plugin.CW.Core.Models.ShoppingCart;
using Nop.Plugin.CW.Core.Services.Exchanges;
using Nop.Plugin.CW.Core.Services.Orders;
using Nop.Plugin.CW.Core.Services.PackNames;
using Nop.Plugin.CW.Core.Services.Settlement;
using Nop.Services.Attributes;
using Nop.Services.Catalog;
using Nop.Services.Common;
using Nop.Services.Configuration;
using Nop.Services.Customers;
using Nop.Services.Directory;
using Nop.Services.Discounts;
using Nop.Services.Helpers;
using Nop.Services.Localization;
using Nop.Services.Media;
using Nop.Services.Orders;
using Nop.Services.Payments;
using Nop.Services.Security;
using Nop.Services.Seo;
using Nop.Services.Shipping;
using Nop.Services.Stores;
using Nop.Services.Tax;
using Nop.Services.Vendors;
using Nop.Web.Factories;
using Nop.Web.Models.ShoppingCart;
using static Nop.Plugin.CW.Core.Models.PackNameCheckout.PackNameShoppingCartModel;

namespace Nop.Plugin.CW.Core.Factories;

public class ShoppingCartOverrideModelFactory : ShoppingCartModelFactory, IShoppingCartOverrideModelFactory
{
    #region Fields

    protected readonly IPackNameContext _packNameContext;
    protected new readonly IShoppingCartOverrideService _shoppingCartService;
    protected readonly IExchangeService _exchangeService;
    protected readonly IPackNameService _packNameService;
    protected readonly ISettingService _settingService;
    protected new readonly IOrderTotalCalculationOverrideService _orderTotalCalculationService;

    #endregion

    #region Ctor

    public ShoppingCartOverrideModelFactory(AddressSettings addressSettings,
        CaptchaSettings captchaSettings,
        CatalogSettings catalogSettings,
        CommonSettings commonSettings,
        CustomerSettings customerSettings,
        IAddressModelFactory addressModelFactory,
        IAddressService addressService,
        IAttributeParser<CheckoutAttribute, CheckoutAttributeValue> checkoutAttributeParser,
        IAttributeService<CheckoutAttribute, CheckoutAttributeValue> checkoutAttributeService,
        ICheckoutAttributeFormatter checkoutAttributeFormatter,
        ICountryService countryService,
        ICurrencyService currencyService,
        ICustomerService customerService,
        ICustomWishlistService customWishlistService,
        IDateTimeHelper dateTimeHelper,
        IDiscountService discountService,
        IDownloadService downloadService,
        IGenericAttributeService genericAttributeService,
        IGiftCardService giftCardService,
        IHttpContextAccessor httpContextAccessor,
        ILocalizationService localizationService,
        IOrderProcessingService orderProcessingService,
        IOrderTotalCalculationOverrideService orderTotalCalculationService,
        IPaymentPluginManager paymentPluginManager,
        IPaymentService paymentService,
        IPermissionService permissionService,
        IPictureService pictureService,
        IPriceFormatter priceFormatter,
        IProductAttributeFormatter productAttributeFormatter,
        IProductService productService,
        IShippingService shippingService,
        IShoppingCartOverrideService shoppingCartService,
        IShortTermCacheManager shortTermCacheManager,
        IStateProvinceService stateProvinceService,
        IStaticCacheManager staticCacheManager,
        IStoreContext storeContext,
        IStoreMappingService storeMappingService,
        ITaxService taxService,
        IUrlRecordService urlRecordService,
        IVendorService vendorService,
        IWebHelper webHelper,
        IWorkContext workContext,
        MediaSettings mediaSettings,
        OrderSettings orderSettings,
        ReturnRequestSettings returnRequestSettings,
        RewardPointsSettings rewardPointsSettings,
        ShippingSettings shippingSettings,
        ShoppingCartSettings shoppingCartSettings,
        TaxSettings taxSettings,
        VendorSettings vendorSettings,
        IPackNameContext packNameContext,
        IExchangeService exchangeService,
        IPackNameService packNameService,
        ISettingService settingService) : base(addressSettings,
                                                captchaSettings,
                                                catalogSettings,
                                                commonSettings,
                                                customerSettings,
                                                addressModelFactory,
                                                addressService,
                                                checkoutAttributeParser,
                                                checkoutAttributeService,
                                                checkoutAttributeFormatter,
                                                countryService,
                                                currencyService,
                                                customerService,
                                                customWishlistService,
                                                dateTimeHelper,
                                                discountService,
                                                downloadService,
                                                genericAttributeService,
                                                giftCardService,
                                                httpContextAccessor,
                                                localizationService,
                                                orderProcessingService,
                                                orderTotalCalculationService,
                                                paymentPluginManager,
                                                paymentService,
                                                permissionService,
                                                pictureService,
                                                priceFormatter,
                                                productAttributeFormatter,
                                                productService,
                                                shippingService,
                                                shoppingCartService,
                                                shortTermCacheManager,
                                                stateProvinceService,
                                                staticCacheManager,
                                                storeContext,
                                                storeMappingService,
                                                taxService,
                                                urlRecordService,
                                                vendorService,
                                                webHelper,
                                                workContext,
                                                mediaSettings,
                                                orderSettings,
                                                returnRequestSettings,
                                                rewardPointsSettings,
                                                shippingSettings,
                                                shoppingCartSettings,
                                                taxSettings,
                                                vendorSettings)
    {
        _packNameContext = packNameContext;
        _shoppingCartService = shoppingCartService;
        _exchangeService = exchangeService;
        _packNameService = packNameService;
        _settingService = settingService;
        _orderTotalCalculationService = orderTotalCalculationService;
    }

    #endregion

    #region Utilities

    protected static bool ShouldInject()
    {
        var actionContext = HttpContextHelper.GetCurrentActionContext();
        var controllerName = HttpContextHelper.GetControllerName(actionContext);
        var actionName = HttpContextHelper.GetActionName(actionContext);

        return !string.IsNullOrEmpty(controllerName)
            && !string.IsNullOrEmpty(actionName)
            && controllerName == "checkout"
            && actionName == "CWOpcSavePaymentMethod";
    }

    protected async Task CWOrderTotalsAsync(IList<ShoppingCartItem> cart, OrderTotalsModel model)
    {
        if (cart.Any())
        {
            //subtotal
            var subTotalIncludingTax = await _workContext.GetTaxDisplayTypeAsync() == TaxDisplayType.IncludingTax && !_taxSettings.ForceTaxExclusionFromOrderSubtotal;
            decimal subtotalBase = 0;
            decimal orderSubTotalDiscountAmountBase = 0;

            var currentCurrency = await _workContext.GetWorkingCurrencyAsync();
            var currentLanguage = await _workContext.GetWorkingLanguageAsync();

            var (orderSubTotalDiscountAmountBaseNop, _, subTotalWithoutDiscountBase, _, _) =
                await _orderTotalCalculationService.GetShoppingCartSubTotalAsync(cart, subTotalIncludingTax);
            subtotalBase = subTotalWithoutDiscountBase;
            orderSubTotalDiscountAmountBase = orderSubTotalDiscountAmountBaseNop;

            var subtotal = await _currencyService.ConvertFromPrimaryStoreCurrencyAsync(subtotalBase, currentCurrency);
            model.SubTotal = await _priceFormatter.FormatPriceAsync(subtotal, true, currentCurrency, currentLanguage.Id, subTotalIncludingTax);

            if (orderSubTotalDiscountAmountBase > decimal.Zero)
            {
                var orderSubTotalDiscountAmount = await _currencyService.ConvertFromPrimaryStoreCurrencyAsync(orderSubTotalDiscountAmountBase, currentCurrency);
                model.SubTotalDiscount = await _priceFormatter.FormatPriceAsync(-orderSubTotalDiscountAmount, true, currentCurrency, currentLanguage.Id, subTotalIncludingTax);
            }

            //shipping info
            model.RequiresShipping = await _shoppingCartService.ShoppingCartRequiresShippingAsync(cart);
            var customer = await _workContext.GetCurrentCustomerAsync();
            var store = await _storeContext.GetCurrentStoreAsync();
            if (model.RequiresShipping)
            {
                var shoppingCartShippingBase = await _orderTotalCalculationService.GetShoppingCartShippingTotalAsync(cart);
                if (shoppingCartShippingBase.HasValue)
                {
                    var shoppingCartShipping = await _currencyService.ConvertFromPrimaryStoreCurrencyAsync(shoppingCartShippingBase.Value, currentCurrency);
                    model.Shipping = await _priceFormatter.FormatShippingPriceAsync(shoppingCartShipping, true);

                    //selected shipping method
                    var shippingOption = await _genericAttributeService.GetAttributeAsync<ShippingOption>(customer,
                        NopCustomerDefaults.SelectedShippingOptionAttribute, store.Id);
                    if (shippingOption != null)
                        model.SelectedShippingMethod = shippingOption.Name;
                }
            }
            else
            {
                model.HideShippingTotal = _shippingSettings.HideShippingTotal;
            }

            //payment method fee
            var paymentMethodSystemName = await _genericAttributeService.GetAttributeAsync<string>(customer, NopCustomerDefaults.SelectedPaymentMethodAttribute, store.Id);
            var paymentMethodAdditionalFee = await _paymentService.GetAdditionalHandlingFeeAsync(cart, paymentMethodSystemName);
            var (paymentMethodAdditionalFeeWithTaxBase, _) = await _taxService.GetPaymentMethodAdditionalFeeAsync(paymentMethodAdditionalFee, customer);
            if (paymentMethodAdditionalFeeWithTaxBase > decimal.Zero)
            {
                var paymentMethodAdditionalFeeWithTax = await _currencyService.ConvertFromPrimaryStoreCurrencyAsync(paymentMethodAdditionalFeeWithTaxBase, currentCurrency);
                model.PaymentMethodAdditionalFee = await _priceFormatter.FormatPaymentMethodAdditionalFeeAsync(paymentMethodAdditionalFeeWithTax, true);
            }

            //tax
            bool displayTax;
            bool displayTaxRates;
            if (_taxSettings.HideTaxInOrderSummary && await _workContext.GetTaxDisplayTypeAsync() == TaxDisplayType.IncludingTax)
            {
                displayTax = false;
                displayTaxRates = false;
            }
            else
            {
                var (shoppingCartTaxBase, taxRates) = await _orderTotalCalculationService.GetTaxTotalAsync(cart);
                var shoppingCartTax = await _currencyService.ConvertFromPrimaryStoreCurrencyAsync(shoppingCartTaxBase, currentCurrency);

                if (shoppingCartTaxBase == 0 && _taxSettings.HideZeroTax)
                {
                    displayTax = false;
                    displayTaxRates = false;
                }
                else
                {
                    displayTaxRates = _taxSettings.DisplayTaxRates && taxRates.Any();
                    displayTax = !displayTaxRates;

                    model.Tax = await _priceFormatter.FormatPriceAsync(shoppingCartTax, true, false);
                    foreach (var tr in taxRates)
                    {
                        model.TaxRates.Add(new OrderTotalsModel.TaxRate
                        {
                            Rate = _priceFormatter.FormatTaxRate(tr.Key),
                            Value = await _priceFormatter.FormatPriceAsync(await _currencyService.ConvertFromPrimaryStoreCurrencyAsync(tr.Value, currentCurrency), true, false),
                        });
                    }
                }
            }

            model.DisplayTaxRates = displayTaxRates;
            model.DisplayTax = displayTax;

            //total

            #region CW code

            decimal orderTotalDiscountAmountBase;
            List<AppliedGiftCard> appliedGiftCards;
            int redeemedRewardPoints;
            decimal redeemedRewardPointsAmount;
            decimal? shoppingCartTotalBase = null;

            var (shoppingCartTotalBaseNop, orderTotalDiscountAmountBaseNop, _, appliedGiftCardsNop, redeemedRewardPointsNop, redeemedRewardPointsAmountNop)
                = await _orderTotalCalculationService.GetShoppingCartTotalAsync(cart);

            shoppingCartTotalBase = shoppingCartTotalBaseNop;
            orderTotalDiscountAmountBase = orderTotalDiscountAmountBaseNop;
            appliedGiftCards = appliedGiftCardsNop;
            redeemedRewardPoints = redeemedRewardPointsNop;
            redeemedRewardPointsAmount = redeemedRewardPointsAmountNop;

            if (shoppingCartTotalBase.HasValue)
            {
                var shoppingCartTotal = await _currencyService.ConvertFromPrimaryStoreCurrencyAsync(shoppingCartTotalBase.Value, currentCurrency);
                model.OrderTotal = await _priceFormatter.FormatPriceAsync(shoppingCartTotal, true, false);
            }

            #endregion

            //discount
            if (orderTotalDiscountAmountBase > decimal.Zero)
            {
                var orderTotalDiscountAmount = await _currencyService.ConvertFromPrimaryStoreCurrencyAsync(orderTotalDiscountAmountBase, currentCurrency);
                model.OrderTotalDiscount = await _priceFormatter.FormatPriceAsync(-orderTotalDiscountAmount, true, false);
            }

            //gift cards
            if (appliedGiftCards != null && appliedGiftCards.Any())
            {
                foreach (var appliedGiftCard in appliedGiftCards)
                {
                    var gcModel = new OrderTotalsModel.GiftCard
                    {
                        Id = appliedGiftCard.GiftCard.Id,
                        CouponCode = appliedGiftCard.GiftCard.GiftCardCouponCode,
                    };
                    var amountCanBeUsed = await _currencyService.ConvertFromPrimaryStoreCurrencyAsync(appliedGiftCard.AmountCanBeUsed, currentCurrency);
                    gcModel.Amount = await _priceFormatter.FormatPriceAsync(-amountCanBeUsed, true, false);

                    var remainingAmountBase = await _giftCardService.GetGiftCardRemainingAmountAsync(appliedGiftCard.GiftCard) - appliedGiftCard.AmountCanBeUsed;
                    var remainingAmount = await _currencyService.ConvertFromPrimaryStoreCurrencyAsync(remainingAmountBase, currentCurrency);
                    gcModel.Remaining = await _priceFormatter.FormatPriceAsync(remainingAmount, true, false);

                    model.GiftCards.Add(gcModel);
                }
            }

            //reward points to be spent (redeemed)
            if (redeemedRewardPointsAmount > decimal.Zero)
            {
                var redeemedRewardPointsAmountInCustomerCurrency = await _currencyService.ConvertFromPrimaryStoreCurrencyAsync(redeemedRewardPointsAmount, currentCurrency);
                model.RedeemedRewardPoints = redeemedRewardPoints;
                model.RedeemedRewardPointsAmount = await _priceFormatter.FormatPriceAsync(-redeemedRewardPointsAmountInCustomerCurrency, true, false);
            }

            //reward points to be earned
            if (_rewardPointsSettings.Enabled && _rewardPointsSettings.DisplayHowMuchWillBeEarned && shoppingCartTotalBase.HasValue)
            {
                //get shipping total
                var shippingBaseInclTax = !model.RequiresShipping ? 0 : (await _orderTotalCalculationService.GetShoppingCartShippingTotalAsync(cart, true)).shippingTotal ?? 0;

                //get total for reward points
                var totalForRewardPoints = _orderTotalCalculationService
                    .CalculateApplicableOrderTotalForRewardPoints(shippingBaseInclTax, shoppingCartTotalBase.Value);
                if (totalForRewardPoints > decimal.Zero)
                    model.WillEarnRewardPoints = await _orderTotalCalculationService.CalculateRewardPointsAsync(customer, totalForRewardPoints);
            }
        }
    }

    protected async Task InjectCWShoppingCartDataAsync(ShoppingCartModel model)
    {
        var returnedItems = await _exchangeService.GetReturnRequestItemsForCartItemsAsync(model.Items.Select(i => i.Id).ToArray());

        var injectedItems = new List<ShoppingCartModel.ShoppingCartItemModel>();

        foreach (var item in model.Items)
        {
            var returnData = returnedItems.FirstOrDefault(r => r.ShoppingCartItemId == item.Id);

            if (returnData != null)
            {
                var cwItem = new CWCartItemModel(item)
                {
                    ReturnRequestItemId = returnData.Id,
                    DisableRemoval = true // Do not allow customer to remove Returned Items from the cart
                };
                injectedItems.Add(cwItem);
            }
            else
            {
                injectedItems.Add(item);
            }
        }

        /*
         * At this point we should have a list of shopping cart items that can be a mix of NOP
         * cart items and our custom cart items, we use this list to overwrite the one that
         * came by default
         */
        model.Items = injectedItems;
    }

    protected async Task InjectCWShoppingCartDataAsync(MiniShoppingCartModel model)
    {
        var returnedItems = await _exchangeService.GetReturnRequestItemsForCartItemsAsync(model.Items.Select(i => i.Id).ToArray());

        var injectedItems = new List<MiniShoppingCartModel.ShoppingCartItemModel>();

        foreach (var item in model.Items)
        {
            var returnData = returnedItems.FirstOrDefault(r => r.ShoppingCartItemId == item.Id);

            if (returnData != null)
            {
                var cwItem = new CWMiniCartItemModel(item)
                {
                    ReturnRequestItemId = returnData.Id
                };

                injectedItems.Add(cwItem);
            }
            else
            {
                injectedItems.Add(item);
            }
        }

        model.Items = injectedItems;
    }

    protected async Task InjectPackNameShoppingCartDataAsync(PackNameShoppingCartModel model, IList<PackNameShoppingCartItem> packNameCart)
    {
        var returnedItems = await _exchangeService.GetReturnRequestItemsForCartItemsAsync(model.Items.Select(i => i.Id).ToArray());
        var storeSettings = await _settingService.LoadSettingAsync<MultiStoreSettings>(packNameCart.FirstOrDefault()?.StoreId ?? 0);

        foreach (var item in model.Items)
        {
            var sci = packNameCart.FirstOrDefault(i => i.Id == item.Id);

            if (sci == null)
                continue;

            var returnData = returnedItems.FirstOrDefault(r => r.ShoppingCartItemId == item.Id);

            //black or white text, whichever reads on the packname's colour
            var foreColor = PackNameColourHelper.ForegroundFor(sci.Colour);

            var packName = await _packNameService.GetPackNameByIdAsync(sci.PackNameId);

            //packName
            item.PackNameModel = new PackNameModel
            {
                Id = sci.PackNameId,
                Name = sci.Name,
                Branch = sci.Branch ?? string.Empty,
                BranchId = sci.BranchId,
                Group = sci.Group,
                IsCustomer = sci.PackNameCustomerId.HasValue && sci.PackNameCustomerId.Value > 0,
                Colour = sci.Colour,
                ForeColor = foreColor,
                POEntry = storeSettings.POEntryEnabled ? packName?.POEntry ?? string.Empty : string.Empty,
            };

            if (returnData != null)
            {
                item.ReturnRequestItemId = returnData.Id;
                item.DisableRemoval = true; // Do not allow customer to remove Returned Items from the cart
            }
        }
    }

    protected virtual async Task PreparePackNameShipping(PackNameOrderTotalsModel model, IList<ShoppingCartItem> cart, IList<PackNameShoppingCartItem> packNameCart)
    {
        //shipping info
        model.RequiresShipping = await _shoppingCartService.ShoppingCartRequiresShippingAsync(cart);

        if (model.RequiresShipping)
        {
            var customer = await _workContext.GetCurrentCustomerAsync();
            var store = await _storeContext.GetCurrentStoreAsync();
            var currentCurrency = await _workContext.GetWorkingCurrencyAsync();
            var shoppingCartShippingBase = await _orderTotalCalculationService.GetShoppingCartShippingTotalAsync(packNameCart);

            foreach (var shipping in shoppingCartShippingBase)
            {
                if (!shipping.Price.HasValue)
                    continue;

                //Get entity name for the shipping
                var entityName = string.Empty;

                //If the shipping is for a packName, we will use the packName name, if many, then use all of them comma separated
                foreach (var pacKameId in shipping.PackNameIds)
                {
                    var packNameName = (await _packNameService.GetPackNameByIdAsync(pacKameId))?.Name ?? string.Empty;

                    if (string.IsNullOrEmpty(entityName))
                        entityName = packNameName;
                    else
                        entityName += $", {packNameName}";
                }

                //convert to the current currency
                var shoppingCartShipping = await _currencyService.ConvertFromPrimaryStoreCurrencyAsync(shipping.Price.Value, currentCurrency);
                var shippingTotal = new PackNameShippingTotalsModel
                {
                    Shipping = await _priceFormatter.FormatShippingPriceAsync(shoppingCartShipping, true),
                    SelectedShippingMethod = shipping.ShippingOption?.Name ?? string.Empty,
                    EntityName = entityName
                };

                model.ShippingTotals.Add(shippingTotal);
            }

            //no shipping priced yet (or any packname still unpriced) leaves the total to be calculated during checkout
            if (shoppingCartShippingBase.Any() && !shoppingCartShippingBase.Any(x => x.Price == null))
            {
                var shippingTotalBase = shoppingCartShippingBase.Sum(x => x.Price.Value);
                var shippingTotal = await _currencyService.ConvertFromPrimaryStoreCurrencyAsync(shippingTotalBase, currentCurrency);
                model.Shipping = await _priceFormatter.FormatShippingPriceAsync(shippingTotal, true);
            }
        }
        else
        {
            model.HideShippingTotal = _shippingSettings.HideShippingTotal;
        }
    }

    #endregion

    #region Overrides

    /// <summary>
    /// Prepare the mini shopping cart model
    /// </summary>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the mini shopping cart model
    /// </returns>
    public override async Task<MiniShoppingCartModel> PrepareMiniShoppingCartModelAsync()
    {
        var customer = await _workContext.GetCurrentCustomerAsync();
        var currentPackName = await _packNameContext.GetCurrentPackNameAsync(customer);

        MiniShoppingCartModel model = null;

        // No packname context
        if (currentPackName == null || currentPackName.Id == 0)
            model = await base.PrepareMiniShoppingCartModelAsync();
        else
        {
            // Packname context active — build model from scratch using the right cart
            var store = await _storeContext.GetCurrentStoreAsync();

            //The full cart is the one definition of the cart (it leaves out the lines of packnames that cannot
            //order), and the current packname's lines are taken from it, so the flyout never lists a line the
            //header does not count or the checkout does not show.
            var fullCart = await _shoppingCartService.GetShoppingCartAsync(customer, ShoppingCartType.ShoppingCart, store.Id);

            var packNameCartItemIds = (await _shoppingCartService.GetPackNameCartAsync(customer, currentPackName.Id, store.Id))
                .Select(item => item.Id)
                .ToHashSet();

            var packNameCart = fullCart.Where(item => packNameCartItemIds.Contains(item.Id)).ToList();

            model = new PackNameMiniShoppingCartModel
            {
                ShowProductImages = _shoppingCartSettings.ShowProductImagesInMiniShoppingCart,
                //A packname cart is seen at the packname checkout; nop's cart page only redirects there.
                DisplayShoppingCartButton = false,
                CurrentCustomerIsGuest = await _customerService.IsGuestAsync(customer),
                AnonymousCheckoutAllowed = _orderSettings.AnonymousCheckoutAllowed,

                // based on current packname's items only
                TotalProducts = packNameCart.Sum(i => i.Quantity),

                // Manager total — all packnames combined
                ManagerTotalProducts = fullCart.Sum(i => i.Quantity),
                PackNameTotalProducts = packNameCart.Sum(i => i.Quantity),
                CurrentPackNameName = currentPackName.Name,
                HasOtherPackNameItems = fullCart.Count > packNameCart.Count,
            };

            if (!packNameCart.Any())
                return model;

            // Subtotal for current packname only
            var subTotalIncludingTax = await _workContext.GetTaxDisplayTypeAsync() == TaxDisplayType.IncludingTax && !_taxSettings.ForceTaxExclusionFromOrderSubtotal;

            var (_, _, subTotalBase, _, _) = await _orderTotalCalculationService.GetShoppingCartSubTotalAsync(packNameCart, subTotalIncludingTax);

            var currentCurrency = await _workContext.GetWorkingCurrencyAsync();
            var subtotal = await _currencyService.ConvertFromPrimaryStoreCurrencyAsync(subTotalBase, currentCurrency);

            model.SubTotal = await _priceFormatter.FormatPriceAsync(subtotal, false, currentCurrency, (await _workContext.GetWorkingLanguageAsync()).Id, subTotalIncludingTax);
            model.SubTotalValue = subtotal;

            var requiresShipping = await _shoppingCartService.ShoppingCartRequiresShippingAsync(fullCart);
            //a customer should visit the shopping cart page (hide checkout button) before going to checkout if:
            //1. "terms of service" are enabled
            //2. min order sub-total is OK
            //3. we have at least one checkout attribute
            var checkoutAttributesExist = (await _checkoutAttributeService
                    .GetAllAttributesAsync(_staticCacheManager, _storeMappingService, store.Id, !requiresShipping))
                .Any();

            var minOrderSubtotalAmountOk = await _orderProcessingService.ValidateMinOrderSubtotalAmountAsync(fullCart);

            var cartProductIds = fullCart.Select(ci => ci.ProductId).ToArray();

            var downloadableProductsRequireRegistration =
                _customerSettings.RequireRegistrationForDownloadableProducts && await _productService.HasAnyDownloadableProductAsync(cartProductIds);

            model.DisplayCheckoutButton = !_orderSettings.TermsOfServiceOnShoppingCartPage &&
                                  minOrderSubtotalAmountOk &&
                                  !checkoutAttributesExist &&
                                  !(downloadableProductsRequireRegistration
                                    && await _customerService.IsGuestAsync(customer));

            // Build items from current packname's cart only
            foreach (var sci in packNameCart.OrderByDescending(x => x.Id).Take(_shoppingCartSettings.MiniShoppingCartProductNumber))
            {
                var product = await _productService.GetProductByIdAsync(sci.ProductId);

                var cartItemModel = new MiniShoppingCartModel.ShoppingCartItemModel
                {
                    Id = sci.Id,
                    ProductId = sci.ProductId,
                    ProductName = await _localizationService.GetLocalizedAsync(product, x => x.Name),
                    ProductSeName = await _urlRecordService.GetSeNameAsync(product),
                    Quantity = sci.Quantity,
                    AttributeInfo = await _productAttributeFormatter.FormatAttributesAsync(product, sci.AttributesXml)
                };

                if (product.CallForPrice && (!_orderSettings.AllowAdminsToBuyCallForPriceProducts || _workContext.OriginalCustomerIfImpersonated == null))
                {
                    cartItemModel.UnitPrice = await _localizationService.GetResourceAsync("Products.CallForPrice");
                    cartItemModel.UnitPriceValue = 0;
                }
                else
                {
                    var (priceBase, _) = await _taxService.GetProductPriceAsync(product, (await _shoppingCartService.GetUnitPriceAsync(sci, true)).unitPrice);
                    var price = await _currencyService.ConvertFromPrimaryStoreCurrencyAsync(priceBase, currentCurrency);
                    cartItemModel.UnitPrice = await _priceFormatter.FormatPriceAsync(price);
                    cartItemModel.UnitPriceValue = price;
                }

                if (_shoppingCartSettings.ShowProductImagesInMiniShoppingCart)
                {
                    cartItemModel.Picture = await PrepareCartItemPictureModelAsync(sci, _mediaSettings.MiniCartThumbPictureSize, true, cartItemModel.ProductName);
                }

                model.Items.Add(cartItemModel);
            }
        }

        // Inject custom CW data into the Mini Shopping cart model
        await InjectCWShoppingCartDataAsync(model);

        return model;
    }

    public override async Task<ShoppingCartModel> PrepareShoppingCartModelAsync(
        ShoppingCartModel model,
        IList<ShoppingCartItem> cart,
        bool isEditable = true,
        bool validateCheckoutAttributes = false,
        bool prepareAndDisplayOrderReviewData = false)
    {
        var modelResult = await base.PrepareShoppingCartModelAsync(model, cart, isEditable, validateCheckoutAttributes, prepareAndDisplayOrderReviewData);

        if (!prepareAndDisplayOrderReviewData && !ShouldInject())
            return modelResult;

        await InjectCWShoppingCartDataAsync(modelResult);

        return modelResult;
    }

    public override async Task<OrderTotalsModel> PrepareOrderTotalsModelAsync(
        IList<ShoppingCartItem> cart, bool isEditable)
    {
        var model = new OrderTotalsModel
        {
            IsEditable = isEditable
        };

        if (!cart.Any())
            return model;

        await CWOrderTotalsAsync(cart, model);

        //NopOrderTotals(cart, model);

        return model;
    }

    #endregion

    #region Methods

    #region PackName

    /// <summary>
    /// Prepare the shopping cart model
    /// </summary>
    /// <param name="model">Shopping cart model</param>
    /// <param name="cart">List of the shopping cart item</param>
    /// <param name="loadItems">Whether to load items</param>
    /// <param name="isEditable">Whether model is editable</param>
    /// <param name="validateCheckoutAttributes">Whether to validate checkout attributes</param>
    /// <param name="prepareAndDisplayOrderReviewData">Whether to prepare and display order review data</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the shopping cart model
    /// </returns>
    public virtual async Task<PackNameShoppingCartModel> PreparePackNameShoppingCartModelAsync(PackNameShoppingCartModel model,
        IList<PackNameShoppingCartItem> packNameCart, bool loadItems = true, bool isEditable = true,
        bool validateCheckoutAttributes = false,
        bool prepareAndDisplayOrderReviewData = false)
    {
        ArgumentNullException.ThrowIfNull(packNameCart);
        ArgumentNullException.ThrowIfNull(model);

        var cart = packNameCart.Cast<ShoppingCartItem>().ToList();

        await base.PrepareShoppingCartModelAsync(model, cart, isEditable, validateCheckoutAttributes, prepareAndDisplayOrderReviewData);
        model.HasItems = cart.Count != 0;

        if (loadItems)
        {
            // PackNameShoppingCartModel.Items uses `new` (member hiding), the base method
            // populated the HIDDEN base-class Items list, not model.Items.
            var baseItems = ((ShoppingCartModel)model).Items;
            model.Items = baseItems.Select(item => new PackNameShoppingCartItemModel(item)).ToList();

            // Injects both exchange data and PackName data
            await InjectPackNameShoppingCartDataAsync(model, packNameCart);
        }
        else
        {
            // Items were loaded by base into the hidden base.Items list but are not needed
            // (they will be fetched via AJAX). Clear them to avoid holding stale references.
            ((ShoppingCartModel)model).Items.Clear();
        }

        return model;
    }

    /// <summary>
    /// Prepare the checkout attribute models
    /// </summary>
    /// <param name="cart">List of the shopping cart item</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the list of the checkout attribute model
    /// </returns>
    public virtual async Task<IList<ShoppingCartModel.CheckoutAttributeModel>> PreparePackNameCheckoutAttributeModelsAsync(
        IList<ShoppingCartItem> cart)
    {
        return await PrepareCheckoutAttributeModelsAsync(cart);
    }

    /// <summary>
    /// Prepare the order totals model
    /// </summary>
    /// <param name="packNameCart">List of the shopping cart item</param>
    /// <param name="isEditable">Whether model is editable</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the order totals model
    /// </returns>
    public virtual async Task<PackNameOrderTotalsModel> PreparePackNameOrderTotalsModelAsync(IList<PackNameShoppingCartItem> packNameCart, bool isEditable)
    {
        var model = new PackNameOrderTotalsModel
        {
            IsEditable = isEditable
        };

        var store = await _storeContext.GetCurrentStoreAsync();
        var customer = await _workContext.GetCurrentCustomerAsync();

        var cart = packNameCart.Cast<ShoppingCartItem>().ToList();

        if (cart.Any())
        {
            bool hasNoTaxExempt = false;

            foreach (var shoppingCartItem in cart)
            {
                var product = await _productService.GetProductByIdAsync(shoppingCartItem.ProductId);
                if (!product.IsTaxExempt)
                {
                    hasNoTaxExempt = true;
                    break;
                }
            }

            //subtotal
            var subTotalIncludingTax = await _workContext.GetTaxDisplayTypeAsync() == TaxDisplayType.IncludingTax && !_taxSettings.ForceTaxExclusionFromOrderSubtotal;
            var (_, _, subTotalWithoutDiscountBase, _, _) = await _orderTotalCalculationService.GetShoppingCartSubTotalAsync(cart, subTotalIncludingTax);

            //the subtotal discount actually applied: never more than subsidy and store credit left to pay
            var (subTotalDiscountInclTax, subTotalDiscountExclTax, _) = await _orderTotalCalculationService.GetAppliedSubTotalDiscountAsync(cart);
            var orderSubTotalDiscountAmountBase = subTotalIncludingTax ? subTotalDiscountInclTax : subTotalDiscountExclTax;
            var subtotalBase = subTotalWithoutDiscountBase;
            var currentCurrency = await _workContext.GetWorkingCurrencyAsync();
            var subTotal = await _currencyService.ConvertFromPrimaryStoreCurrencyAsync(subtotalBase, currentCurrency);
            var currentLanguage = await _workContext.GetWorkingLanguageAsync();
            model.SubTotal = await _priceFormatter.FormatPriceAsync(subTotal, true, currentCurrency, currentLanguage.Id, subTotalIncludingTax, !hasNoTaxExempt);

            if (orderSubTotalDiscountAmountBase > decimal.Zero)
            {
                var orderSubTotalDiscountAmount = await _currencyService.ConvertFromPrimaryStoreCurrencyAsync(orderSubTotalDiscountAmountBase, currentCurrency);
                model.SubTotalDiscount = await _priceFormatter.FormatPriceAsync(-orderSubTotalDiscountAmount, true, currentCurrency, currentLanguage.Id, subTotalIncludingTax, !hasNoTaxExempt);
            }

            //shipping info
            await PreparePackNameShipping(model, cart, packNameCart);

            //payment method fee
            var paymentMethodSystemName = await _genericAttributeService.GetAttributeAsync<string>(customer, NopCustomerDefaults.SelectedPaymentMethodAttribute, store.Id);
            var paymentMethodAdditionalFee = await _paymentService.GetAdditionalHandlingFeeAsync(cart, paymentMethodSystemName);
            var (paymentMethodAdditionalFeeWithTaxBase, _) = await _taxService.GetPaymentMethodAdditionalFeeAsync(paymentMethodAdditionalFee, customer);
            if (paymentMethodAdditionalFeeWithTaxBase > decimal.Zero)
            {
                var paymentMethodAdditionalFeeWithTax = await _currencyService.ConvertFromPrimaryStoreCurrencyAsync(paymentMethodAdditionalFeeWithTaxBase, currentCurrency);
                model.PaymentMethodAdditionalFee = await _priceFormatter.FormatPaymentMethodAdditionalFeeAsync(paymentMethodAdditionalFeeWithTax, true);

                var (paymentMethodAdditionalFeeIncTaxBase, _) = await _taxService.GetPaymentMethodAdditionalFeeAsync(paymentMethodAdditionalFee, true, customer);
                var (paymentMethodAdditionalFeeExclTaxBase, _) = await _taxService.GetPaymentMethodAdditionalFeeAsync(paymentMethodAdditionalFee, false, customer);
                var paymentMethodAdditionalFeeTaxBase = paymentMethodAdditionalFeeIncTaxBase - paymentMethodAdditionalFeeExclTaxBase;
                var paymentMethodAdditionalFeeTax = await _currencyService.ConvertFromPrimaryStoreCurrencyAsync(paymentMethodAdditionalFeeTaxBase, currentCurrency);
                model.PaymentMethodAdditionalFeeTaxValue = paymentMethodAdditionalFeeTax;
            }

            //tax
            bool displayTax;
            bool displayTaxRates;
            if (_taxSettings.HideTaxInOrderSummary && await _workContext.GetTaxDisplayTypeAsync() == TaxDisplayType.IncludingTax)
            {
                displayTax = false;
                displayTaxRates = false;
            }
            else
            {
                var (shoppingCartTaxBase, taxRates) = await _orderTotalCalculationService.GetTaxTotalAsync(cart);
                var shoppingCartTax = await _currencyService.ConvertFromPrimaryStoreCurrencyAsync(shoppingCartTaxBase, currentCurrency);

                if (shoppingCartTaxBase == 0 && _taxSettings.HideZeroTax)
                {
                    displayTax = false;
                    displayTaxRates = false;
                }
                else
                {
                    displayTaxRates = _taxSettings.DisplayTaxRates && taxRates.Any();
                    displayTax = !displayTaxRates;

                    model.Tax = await _priceFormatter.FormatPriceAsync(shoppingCartTax, true, false);
                    model.TaxValue = shoppingCartTax;
                    foreach (var tr in taxRates)
                    {
                        model.TaxRates.Add(new OrderTotalsModel.TaxRate
                        {
                            Rate = _priceFormatter.FormatTaxRate(tr.Key),
                            Value = await _priceFormatter.FormatPriceAsync(await _currencyService.ConvertFromPrimaryStoreCurrencyAsync(tr.Value, currentCurrency), true, false),
                        });
                    }
                }
            }

            model.DisplayTaxRates = displayTaxRates;
            model.DisplayTax = displayTax;

            //settlement — one pipeline quote drives every line below
            var plan = await _orderTotalCalculationService.QuoteCartSettlementAsync(cart);

            var subsidyForShippingInclTax = plan.ShippingAmountFor(SettlementSourceNames.SUBSIDY);
            var subsidyForStockInclTax = plan.AmountFor(SettlementSourceNames.SUBSIDY) - subsidyForShippingInclTax;

            if (subsidyForStockInclTax > 0 || subsidyForShippingInclTax > 0)
            {
                var subsidyForStockInCustomerCurrency = await _currencyService.ConvertFromPrimaryStoreCurrencyAsync(subsidyForStockInclTax, currentCurrency);
                var subsidyForShippingInCustomerCurrency = await _currencyService.ConvertFromPrimaryStoreCurrencyAsync(subsidyForShippingInclTax, currentCurrency);
                model.SubsidyTotal = await _priceFormatter.FormatPriceAsync(-(subsidyForStockInCustomerCurrency + subsidyForShippingInCustomerCurrency), true, false);

                if (subsidyForShippingInCustomerCurrency > 0)
                {
                    model.SubsidyForStock = await _priceFormatter.FormatPriceAsync(-subsidyForStockInCustomerCurrency, true, false);
                    model.SubsidyForShipping = await _priceFormatter.FormatPriceAsync(-subsidyForShippingInCustomerCurrency, true, false);
                }
            }

            // Credit notes need their own line or the panel does not add up: it reads
            // Total − Subsidy − Credit notes + Fees = Payable Balance.
            var creditNotesInclTax = plan.AmountFor(SettlementSourceNames.CREDIT_NOTES);

            if (creditNotesInclTax > decimal.Zero)
            {
                var creditNotesInCustomerCurrency = await _currencyService.ConvertFromPrimaryStoreCurrencyAsync(creditNotesInclTax, currentCurrency);
                model.CreditNoteTotal = await _priceFormatter.FormatPriceAsync(-creditNotesInCustomerCurrency, true, false);
            }

            var exchangeFeeAmounts = await _exchangeService.GetExchangeFeesAmountsAsync(cart);
            var exchangeFeesInclTax = exchangeFeeAmounts.Values.Sum();

            if (exchangeFeesInclTax > decimal.Zero)
            {
                var exchangeFeesInCustomerCurrency = await _currencyService.ConvertFromPrimaryStoreCurrencyAsync(exchangeFeesInclTax, currentCurrency);
                model.ExchangeFeesTotal = await _priceFormatter.FormatPriceAsync(exchangeFeesInCustomerCurrency, true, false);

                foreach (var feeAmount in exchangeFeeAmounts.Where(x => x.Value > decimal.Zero))
                {
                    var feeInCustomerCurrency = await _currencyService.ConvertFromPrimaryStoreCurrencyAsync(feeAmount.Value, currentCurrency);

                    model.ExchangeFeeDetails.Add((
                        await _localizationService.GetLocalizedEnumAsync(feeAmount.Key),
                        await _priceFormatter.FormatPriceAsync(feeInCustomerCurrency, true, false)));
                }
            }

            //total
            var (shoppingCartTotalBase, orderTotalDiscountAmountBase, _, appliedGiftCards, redeemedRewardPoints, redeemedRewardPointsAmount) = await _orderTotalCalculationService.GetShoppingCartTotalAsync(cart);

            if (shoppingCartTotalBase.HasValue)
            {
                // OrderTotal is the pre-settlement figure the pipeline was handed. PayableBalance is what
                // GetShoppingCartTotalAsync returned — balance due plus fees. With the subsidy, credit-note
                // and fee lines all present, the column now reconciles.
                if (plan.TotalSettled > decimal.Zero || exchangeFeesInclTax > decimal.Zero)
                {
                    var payableBalance = await _currencyService.ConvertFromPrimaryStoreCurrencyAsync(shoppingCartTotalBase.Value, currentCurrency);
                    var orderTotal = await _currencyService.ConvertFromPrimaryStoreCurrencyAsync(plan.SettleableAmount, currentCurrency);
                    model.PayableBalance = await _priceFormatter.FormatPriceAsync(payableBalance, true, false);
                    model.OrderTotal = await _priceFormatter.FormatPriceAsync(orderTotal, true, false);
                }
                else
                {
                    var shoppingCartTotal = await _currencyService.ConvertFromPrimaryStoreCurrencyAsync(shoppingCartTotalBase.Value, currentCurrency);
                    model.OrderTotal = await _priceFormatter.FormatPriceAsync(shoppingCartTotal, true, false);
                }
            }

            //discount
            if (orderTotalDiscountAmountBase > decimal.Zero)
            {
                var orderTotalDiscountAmount = await _currencyService.ConvertFromPrimaryStoreCurrencyAsync(orderTotalDiscountAmountBase, currentCurrency);
                model.OrderTotalDiscount = await _priceFormatter.FormatPriceAsync(-orderTotalDiscountAmount, true, false);
            }

            //gift cards
            if (appliedGiftCards != null && appliedGiftCards.Any())
            {
                foreach (var appliedGiftCard in appliedGiftCards)
                {
                    var gcModel = new OrderTotalsModel.GiftCard
                    {
                        Id = appliedGiftCard.GiftCard.Id,
                        CouponCode = appliedGiftCard.GiftCard.GiftCardCouponCode,
                    };
                    var amountCanBeUsed = await _currencyService.ConvertFromPrimaryStoreCurrencyAsync(appliedGiftCard.AmountCanBeUsed, currentCurrency);
                    gcModel.Amount = await _priceFormatter.FormatPriceAsync(-amountCanBeUsed, true, false);

                    var remainingAmountBase = await _giftCardService.GetGiftCardRemainingAmountAsync(appliedGiftCard.GiftCard) - appliedGiftCard.AmountCanBeUsed;
                    var remainingAmount = await _currencyService.ConvertFromPrimaryStoreCurrencyAsync(remainingAmountBase, currentCurrency);
                    gcModel.Remaining = await _priceFormatter.FormatPriceAsync(remainingAmount, true, false);

                    model.GiftCards.Add(gcModel);
                }
            }

            //reward points to be spent (redeemed)
            if (redeemedRewardPointsAmount > decimal.Zero)
            {
                var redeemedRewardPointsAmountInCustomerCurrency = await _currencyService.ConvertFromPrimaryStoreCurrencyAsync(redeemedRewardPointsAmount, currentCurrency);
                model.RedeemedRewardPoints = redeemedRewardPoints;
                model.RedeemedRewardPointsAmount = await _priceFormatter.FormatPriceAsync(-redeemedRewardPointsAmountInCustomerCurrency, true, false);
            }

            //reward points to be earned
            if (_rewardPointsSettings.Enabled && _rewardPointsSettings.DisplayHowMuchWillBeEarned && shoppingCartTotalBase.HasValue)
            {
                //get shipping total
                var shippingBaseInclTax = !model.RequiresShipping ? 0 : (await _orderTotalCalculationService.GetShoppingCartShippingTotalAsync(cart, true)).shippingTotal ?? 0;

                //get total for reward points
                var totalForRewardPoints = _orderTotalCalculationService
                    .CalculateApplicableOrderTotalForRewardPoints(shippingBaseInclTax, shoppingCartTotalBase.Value);
                if (totalForRewardPoints > decimal.Zero)
                    model.WillEarnRewardPoints = await _orderTotalCalculationService.CalculateRewardPointsAsync(customer, totalForRewardPoints);
            }
        }

        return model;
    }

    #endregion

    #endregion
}
