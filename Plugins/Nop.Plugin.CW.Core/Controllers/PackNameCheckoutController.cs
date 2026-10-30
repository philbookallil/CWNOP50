using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Primitives;
using Nop.Core;
using Nop.Core.Caching;
using Nop.Core.Domain.Catalog;
using Nop.Core.Domain.Common;
using Nop.Core.Domain.Customers;
using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Payments;
using Nop.Core.Domain.Shipping;
using Nop.Core.Http;
using Nop.Plugin.CW.Core.Components;
using Nop.Plugin.CW.Core.Domain.Orders.Views;
using Nop.Plugin.CW.Core.Domain.Settings;
using Nop.Plugin.CW.Core.Factories;
using Nop.Plugin.CW.Core.Filters;
using Nop.Plugin.CW.Core.Models.PackNameCheckout;
using Nop.Plugin.CW.Core.Services.Branches;
using Nop.Plugin.CW.Core.Services.Exchanges;
using Nop.Plugin.CW.Core.Services.MultiStores;
using Nop.Plugin.CW.Core.Services.Orders;
using Nop.Plugin.CW.Core.Services.PackNames;
using Nop.Services.Attributes;
using Nop.Services.Catalog;
using Nop.Services.Common;
using Nop.Services.Configuration;
using Nop.Services.Customers;
using Nop.Services.Directory;
using Nop.Services.Helpers;
using Nop.Services.Localization;
using Nop.Services.Logging;
using Nop.Services.Media;
using Nop.Services.Messages;
using Nop.Services.Orders;
using Nop.Services.Payments;
using Nop.Services.Security;
using Nop.Services.Stores;
using Nop.Web.Components;
using Nop.Web.Controllers;
using Nop.Web.Factories;

namespace Nop.Plugin.CW.Core.Controllers;

[CheckCWFeature(CWFeature.PackName, StoreSource = CWFeatureStoreSource.CurrentStore, RedirectRouteName = NopRouteNames.Standard.CHECKOUT)]
[AutoValidateAntiforgeryToken]
public class PackNameCheckoutController : BasePublicController
{
    #region Fields

    protected readonly IWorkContext _workContext;
    protected readonly IStoreContext _storeContext;
    protected readonly IShoppingCartOverrideService _shoppingCartService;
    protected readonly CustomerSettings _customerSettings;
    protected readonly ICustomerService _customerService;
    protected readonly IPackNameCheckoutModelFactory _packNameCheckoutModelFactory;
    protected readonly IOrderProcessingOverrideService _orderProcessingService;
    protected readonly IGenericAttributeService _genericAttributeService;
    protected readonly AddressSettings _addressSettings;
    protected readonly ICheckoutModelFactory _checkoutModelFactory;
    protected readonly PaymentSettings _paymentSettings;
    protected readonly OrderSettings _orderSettings;
    protected readonly INotificationService _notificationService;
    protected readonly ILocalizationService _localizationService;
    protected readonly ISettingService _settingService;
    protected readonly IAttributeParser<CheckoutAttribute, CheckoutAttributeValue> _checkoutAttributeParser;
    protected readonly IAttributeService<CheckoutAttribute, CheckoutAttributeValue> _checkoutAttributeService;
    protected readonly IStaticCacheManager _staticCacheManager;
    protected readonly IStoreMappingService _storeMappingService;
    protected readonly IDownloadService _downloadService;
    protected readonly IPaymentPluginManager _paymentPluginManager;
    protected readonly IPaymentService _paymentService;
    protected readonly IOrderService _orderService;
    protected readonly IWebHelper _webHelper;
    protected readonly IPermissionService _permissionService;
    protected readonly IPackNameService _packNameService;
    protected readonly IBranchService _branchService;
    protected readonly IAttributeParser<AddressAttribute, AddressAttributeValue> _addressAttributeParser;
    protected readonly IAddressService _addressService;
    protected readonly ILogger _logger;
    protected readonly IShoppingCartOverrideModelFactory _shoppingCartModelFactory;
    protected readonly IPackNameShippingAssignmentService _shippingAssignmentService;
    protected readonly IExchangeService _exchangeService;
    protected readonly IReturnRequestService _returnRequestService;
    protected readonly IMultiStoreService _multiStoreService;
    protected readonly ICWOrderService _cwOrderService;
    protected readonly IPackNameOrderModelFactory _packNameOrderModelFactory;
    protected readonly IPriceFormatter _priceFormatter;
    protected readonly ICurrencyService _currencyService;
    protected readonly ShoppingCartSettings _shoppingCartSettings;

    #endregion

    #region Ctor

    public PackNameCheckoutController(IWorkContext workContext,
        IStoreContext storeContext,
        IShoppingCartOverrideService shoppingCartService,
        CustomerSettings customerSettings,
        ICustomerService customerService,
        IPackNameCheckoutModelFactory packNameCheckoutModelFactory,
        IOrderProcessingOverrideService orderProcessingService,
        IGenericAttributeService genericAttributeService,
        AddressSettings addressSettings,
        ICheckoutModelFactory checkoutModelFactory,
        PaymentSettings paymentSettings,
        OrderSettings orderSettings,
        INotificationService notificationService,
        ILocalizationService localizationService,
        ISettingService settingService,
        IAttributeParser<CheckoutAttribute, CheckoutAttributeValue> checkoutAttributeParser,
        IAttributeService<CheckoutAttribute, CheckoutAttributeValue> checkoutAttributeService,
        IStaticCacheManager staticCacheManager,
        IStoreMappingService storeMappingService,
        IDownloadService downloadService,
        IPaymentPluginManager paymentPluginManager,
        IPaymentService paymentService,
        IOrderService orderService,
        IWebHelper webHelper,
        IPermissionService permissionService,
        IPackNameService packNameService,
        IBranchService branchService,
        IAttributeParser<AddressAttribute, AddressAttributeValue> addressAttributeParser,
        IAddressService addressService,
        ILogger logger,
        IShoppingCartOverrideModelFactory shoppingCartModelFactory,
        IPackNameShippingAssignmentService shippingAssignmentService,
        IExchangeService exchangeService,
        IReturnRequestService returnRequestService,
        IMultiStoreService multiStoreService,
        ICWOrderService cwOrderService,
        IPackNameOrderModelFactory packNameOrderModelFactory,
        IPriceFormatter priceFormatter,
        ICurrencyService currencyService,
        ShoppingCartSettings shoppingCartSettings)
    {
        _workContext = workContext;
        _storeContext = storeContext;
        _shoppingCartService = shoppingCartService;
        _customerSettings = customerSettings;
        _customerService = customerService;
        _packNameCheckoutModelFactory = packNameCheckoutModelFactory;
        _orderProcessingService = orderProcessingService;
        _genericAttributeService = genericAttributeService;
        _addressSettings = addressSettings;
        _checkoutModelFactory = checkoutModelFactory;
        _paymentSettings = paymentSettings;
        _orderSettings = orderSettings;
        _notificationService = notificationService;
        _localizationService = localizationService;
        _settingService = settingService;
        _checkoutAttributeParser = checkoutAttributeParser;
        _checkoutAttributeService = checkoutAttributeService;
        _staticCacheManager = staticCacheManager;
        _storeMappingService = storeMappingService;
        _downloadService = downloadService;
        _paymentPluginManager = paymentPluginManager;
        _paymentService = paymentService;
        _orderService = orderService;
        _webHelper = webHelper;
        _permissionService = permissionService;
        _packNameService = packNameService;
        _branchService = branchService;
        _addressAttributeParser = addressAttributeParser;
        _addressService = addressService;
        _logger = logger;
        _shoppingCartModelFactory = shoppingCartModelFactory;
        _shippingAssignmentService = shippingAssignmentService;
        _exchangeService = exchangeService;
        _returnRequestService = returnRequestService;
        _multiStoreService = multiStoreService;
        _cwOrderService = cwOrderService;
        _packNameOrderModelFactory = packNameOrderModelFactory;
        _priceFormatter = priceFormatter;
        _currencyService = currencyService;
        _shoppingCartSettings = shoppingCartSettings;
    }

    #endregion

    #region Utilities

    protected async Task ParseAndSaveCheckoutAttributesAsync(IList<ShoppingCartItem> cart, IFormCollection form)
    {
        ArgumentNullException.ThrowIfNull(cart);

        ArgumentNullException.ThrowIfNull(form);

        var attributesXml = string.Empty;
        var excludeShippableAttributes = !await _shoppingCartService.ShoppingCartRequiresShippingAsync(cart);
        var store = await _storeContext.GetCurrentStoreAsync();
        var checkoutAttributes = await _checkoutAttributeService.GetAllAttributesAsync(_staticCacheManager, _storeMappingService, store.Id, excludeShippableAttributes);
        foreach (var attribute in checkoutAttributes)
        {
            var controlId = $"checkout_attribute_{attribute.Id}";
            switch (attribute.AttributeControlType)
            {
                case AttributeControlType.DropdownList:
                case AttributeControlType.RadioList:
                case AttributeControlType.ColorSquares:
                case AttributeControlType.ImageSquares:
                    {
                        var ctrlAttributes = form[controlId];
                        if (!StringValues.IsNullOrEmpty(ctrlAttributes))
                        {
                            var selectedAttributeId = int.Parse(ctrlAttributes);
                            if (selectedAttributeId > 0)
                                attributesXml = _checkoutAttributeParser.AddAttribute(attributesXml,
                                    attribute, selectedAttributeId.ToString());
                        }
                    }

                    break;
                case AttributeControlType.Checkboxes:
                    {
                        var cblAttributes = form[controlId];
                        if (!StringValues.IsNullOrEmpty(cblAttributes))
                        {
                            foreach (var item in cblAttributes.ToString().Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
                            {
                                var selectedAttributeId = int.Parse(item);
                                if (selectedAttributeId > 0)
                                    attributesXml = _checkoutAttributeParser.AddAttribute(attributesXml,
                                        attribute, selectedAttributeId.ToString());
                            }
                        }
                    }

                    break;
                case AttributeControlType.ReadonlyCheckboxes:
                    {
                        //load read-only (already server-side selected) values
                        var attributeValues = await _checkoutAttributeService.GetAttributeValuesAsync(attribute.Id);
                        foreach (var selectedAttributeId in attributeValues
                            .Where(v => v.IsPreSelected)
                            .Select(v => v.Id)
                            .ToList())
                        {
                            attributesXml = _checkoutAttributeParser.AddAttribute(attributesXml,
                                        attribute, selectedAttributeId.ToString());
                        }
                    }

                    break;
                case AttributeControlType.TextBox:
                case AttributeControlType.MultilineTextbox:
                    {
                        var ctrlAttributes = form[controlId];
                        if (!StringValues.IsNullOrEmpty(ctrlAttributes))
                        {
                            var enteredText = ctrlAttributes.ToString().Trim();
                            attributesXml = _checkoutAttributeParser.AddAttribute(attributesXml,
                                attribute, enteredText);
                        }
                    }

                    break;
                case AttributeControlType.Datepicker:
                    {
                        var date = form[controlId + "_day"];
                        var month = form[controlId + "_month"];
                        var year = form[controlId + "_year"];
                        DateTime? selectedDate = null;
                        try
                        {
                            selectedDate = new DateTime(int.Parse(year), int.Parse(month), int.Parse(date));
                        }
                        catch
                        {
                            // ignored
                        }

                        if (selectedDate.HasValue)
                            attributesXml = _checkoutAttributeParser.AddAttribute(attributesXml,
                                attribute, selectedDate.Value.ToString("D"));
                    }

                    break;
                case AttributeControlType.FileUpload:
                    {
                        _ = Guid.TryParse(form[controlId], out var downloadGuid);
                        var download = await _downloadService.GetDownloadByGuidAsync(downloadGuid);
                        if (download != null)
                        {
                            attributesXml = _checkoutAttributeParser.AddAttribute(attributesXml,
                                       attribute, download.DownloadGuid.ToString());
                        }
                    }

                    break;
                default:
                    break;
            }
        }

        //validate conditional attributes (if specified)
        foreach (var attribute in checkoutAttributes)
        {
            var conditionMet = await _checkoutAttributeParser.IsConditionMetAsync(attribute.ConditionAttributeXml, attributesXml);
            if (conditionMet.HasValue && !conditionMet.Value)
                attributesXml = _checkoutAttributeParser.RemoveAttribute(attributesXml, attribute.Id);
        }

        //save checkout attributes
        await _genericAttributeService.SaveAttributeAsync(await _workContext.GetCurrentCustomerAsync(), NopCustomerDefaults.CheckoutAttributes, attributesXml, store.Id);
    }

    protected async Task<bool> ValidateShippingData(Customer customer, int storeId, IList<PackNameShoppingCartItem> packNameCart, MultiStoreSettings storeSettings)
    {
        var packNameIds = packNameCart.Select(item => item.PackNameId).Distinct().ToList();
        var assignments = await _shippingAssignmentService.LoadAsync(customer, storeId);

        //There could be packnames with a different shipping address from the branch or same address from other branches
        foreach (var packNameId in packNameIds)
        {
            var branchId = packNameCart.First(x => x.PackNameId == packNameId).BranchId;
            var addressId = assignments.GetAddressId(packNameId);

            var addressValid = addressId is > 0
                && await _shoppingCartService.IsAddressValidForCheckoutAsync(
                    branchId, packNameId, addressId.Value, customer, storeId, storeSettings);

            if (!addressValid)
            {
                _notificationService.ErrorNotification(
                    await _localizationService.GetResourceAsync("Plugins.CW.Core.PackNameCheckout.EnterShippingAddressPackName"));
                return false;
            }

            if (assignments.GetSelectedOption(packNameId) == null)
            {
                _notificationService.ErrorNotification(
                    await _localizationService.GetResourceAsync("Plugins.CW.Core.PackNameCheckout.EnterShippingMethodPackName"));
                return false;
            }
        }

        return true;
    }

    protected virtual async Task<bool> IsMinimumOrderPlacementIntervalValidAsync(Customer customer)
    {
        //prevent 2 orders being placed within an X seconds time frame
        if (_orderSettings.MinimumOrderPlacementInterval == 0)
            return true;

        var store = await _storeContext.GetCurrentStoreAsync();

        var lastOrder = (await _orderService.SearchOrdersAsync(storeId: store.Id,
                customerId: customer.Id, pageSize: 1))
            .FirstOrDefault();
        if (lastOrder == null)
            return true;

        var interval = DateTime.UtcNow - lastOrder.CreatedOnUtc;
        return interval.TotalMinutes > _orderSettings.MinimumOrderPlacementInterval;
    }

    /// <summary>
    /// Whether the store lists the payment method as one that takes the payment in its own pages and places
    /// the order itself (<c>MultiStoreSettings.OwnPlacementPaymentMethods</c>).
    /// </summary>
    /// <param name="paymentMethodSystemName">Payment method system name</param>
    /// <param name="storeId">Store identifier</param>
    /// <returns>A task that represents the asynchronous operation. The task result is true for such a method</returns>
    protected virtual async Task<bool> IsOwnPlacementPaymentMethodAsync(string paymentMethodSystemName, int storeId)
    {
        if (string.IsNullOrEmpty(paymentMethodSystemName))
            return false;

        var storeSettings = await _settingService.LoadSettingAsync<MultiStoreSettings>(storeId);

        return storeSettings.OwnPlacementPaymentMethods?.Any(name =>
            string.Equals(name?.Trim(), paymentMethodSystemName, StringComparison.InvariantCultureIgnoreCase)) == true;
    }

    protected async Task<(List<string> warnings, int orderId, bool redirect)> ConfirmOrder(IList<PackNameShoppingCartItem> packNameCart)
    {
        var customer = await _workContext.GetCurrentCustomerAsync();
        var store = await _storeContext.GetCurrentStoreAsync();
        var cart = packNameCart.Cast<ShoppingCartItem>().ToList();

        try
        {
            //prevent 2 orders being placed within an X seconds time frame
            if (!await IsMinimumOrderPlacementIntervalValidAsync(customer))
                return (new List<string> { await _localizationService.GetResourceAsync("Checkout.MinOrderPlacementInterval") }, 0, false);

            //place order
            var processPaymentRequest = await _orderProcessingService.GetProcessPaymentRequestAsync();
            if (processPaymentRequest == null)
            {
                //Check whether payment workflow is required
                if (await _orderProcessingService.IsPaymentWorkflowRequiredAsync(cart))
                {
                    return (new List<string> { await _localizationService.GetResourceAsync("Plugins.CW.Core.PackNameCheckout.EnterPaymnetMethod") }, 0, false);
                }
                processPaymentRequest = new ProcessPaymentRequest();
            }

            processPaymentRequest.StoreId = store.Id;
            processPaymentRequest.CustomerId = customer.Id;
            processPaymentRequest.PaymentMethodSystemName = await _genericAttributeService.GetAttributeAsync<string>(customer,
                NopCustomerDefaults.SelectedPaymentMethodAttribute, store.Id);

            await _orderProcessingService.SetProcessPaymentRequestAsync(processPaymentRequest);

            var placeOrderResult = await _orderProcessingService.PlacePackNameOrderAsync(processPaymentRequest);
            if (placeOrderResult.Success)
            {
                await _orderProcessingService.SetProcessPaymentRequestAsync(null);

                var postProcessPaymentRequest = new PostProcessPaymentRequest
                {
                    Order = placeOrderResult.PlacedOrder
                };

                await _paymentService.PostProcessPaymentAsync(postProcessPaymentRequest);

                if (_webHelper.IsRequestBeingRedirected || _webHelper.IsPostBeingDone)
                {
                    //redirection or POST has been done in PostProcessPayment
                    return (new List<string>(), 0, true);
                }

                return (new List<string>(), placeOrderResult.PlacedOrder.Id, false);
            }

            return (placeOrderResult.Errors.ToList(), 0, false);
        }
        catch (Exception exc)
        {
            //Logged as nop's own checkout does: the customer only ever saw the message.
            await _logger.WarningAsync(exc.Message, exc, customer);

            return (new List<string> { exc.Message }, 0, false);
        }
    }

    #endregion

    #region Methods

    public virtual async Task<IActionResult> Checkout()
    {
        var customer = await _workContext.GetCurrentCustomerAsync();
        var store = await _storeContext.GetCurrentStoreAsync();
        var packNamesCart = await _shoppingCartService.GetPackNameShoppingCartItemsAsync(customer, 0, store.Id);
        var cart = packNamesCart.Cast<ShoppingCartItem>().ToList();

        if (await _customerService.IsGuestAsync(customer))
            return Challenge();

        //Set the shipping options based on the pack names in the cart before any calculations
        await _packNameCheckoutModelFactory.SetShippingOptionsByPackNamesAsync(customer, store.Id, packNamesCart);

        var model = await _packNameCheckoutModelFactory.PreparePackNameCheckoutModelAsync(packNamesCart, customer);

        if (!packNamesCart.Any())
        {
            return View(model);
        }

        //a line over a subsidy's quantity cap is refused at Confirm; say so before the customer gets there
        foreach (var capWarning in await _shoppingCartService.GetSubsidyQuantityCapWarningsAsync(customer, packNamesCart))
            model.Warnings.Add(capWarning);

        //Check whether payment workflow is required
        //we ignore reward points during cart total calculation
        var isPaymentWorkflowRequired = await _orderProcessingService.IsPaymentWorkflowRequiredAsync(cart, false);

        if (isPaymentWorkflowRequired)
        {
            //filter by country
            var filterByCountryId = 0;
            if (_addressSettings.CountryEnabled)
            {
                filterByCountryId = (await _customerService.GetCustomerBillingAddressAsync(customer))?.CountryId ?? 0;
            }

            //model
            var paymentMethodModel = await _checkoutModelFactory.PreparePaymentMethodModelAsync(cart, filterByCountryId);

            //The factory marks the saved method as selected, or the first one when nothing is saved, but it never
            //saves that choice. The totals rendered below read the saved method for its fee, so the method the page
            //shows as selected is saved here; otherwise the first load prints a total without the fee until the
            //page posts the selection back.
            var selectedPaymentMethod = paymentMethodModel.PaymentMethods.FirstOrDefault(paymentMethod => paymentMethod.Selected);
            var savedPaymentMethodSystemName = await _genericAttributeService.GetAttributeAsync<string>(customer, NopCustomerDefaults.SelectedPaymentMethodAttribute, store.Id);

            if (selectedPaymentMethod != null && !string.Equals(savedPaymentMethodSystemName, selectedPaymentMethod.PaymentMethodSystemName, StringComparison.InvariantCultureIgnoreCase))
                await _genericAttributeService.SaveAttributeAsync(customer, NopCustomerDefaults.SelectedPaymentMethodAttribute, selectedPaymentMethod.PaymentMethodSystemName, store.Id);

            model.PaymentMethod = new PackNamePaymentMethodModel(paymentMethodModel);
        }
        else
        {
            await _genericAttributeService.SaveAttributeAsync<string>(customer, NopCustomerDefaults.SelectedPaymentMethodAttribute, null, store.Id);

            // nothing to pay — tell the view so it renders nothing instead of "no payment methods available"
            model.PaymentMethod = new PackNamePaymentMethodModel { IsPaymentWorkflowRequired = false };
        }

        return View(model);
    }

    [HttpPost]
    public virtual async Task<IActionResult> Checkout(IFormCollection form)
    {
        var customer = await _workContext.GetCurrentCustomerAsync();
        var store = await _storeContext.GetCurrentStoreAsync();

        var warnings = new List<string>();
        IList<string> paymentWarnings = new List<string>();
        var hasErrors = false;

        if (await _customerService.IsGuestAsync(customer))
            return Challenge();

        //validation
        if (_orderSettings.CheckoutDisabled)
        {
            _notificationService.ErrorNotification(await _localizationService.GetResourceAsync("Checkout.Disabled"));
            return RedirectToRoute("PackNameCheckout");
        }

        var packNameCart = await _shoppingCartService.GetPackNameShoppingCartItemsAsync(customer, 0, store.Id);

        if (!packNameCart.Any())
            return RedirectToRoute("PackNameCheckout");

        var cart = packNameCart.Cast<ShoppingCartItem>().ToList();

        var storeSettings = await _settingService.LoadSettingAsync<MultiStoreSettings>(packNameCart.First().StoreId);

        //parse and save checkout attributes
        await ParseAndSaveCheckoutAttributesAsync(cart, form);

        if (!await _shoppingCartService.ShoppingCartRequiresShippingAsync(cart))
        {
            await _genericAttributeService.SaveAttributeAsync<ShippingOption>(customer, NopCustomerDefaults.SelectedShippingOptionAttribute, null, store.Id);
        }
        else
        {
            if (!await ValidateShippingData(customer, store.Id, packNameCart, storeSettings))
                return RedirectToRoute("PackNameCheckout");
        }

        //Check whether payment workflow is required
        var isPaymentWorkflowRequired = await _orderProcessingService.IsPaymentWorkflowRequiredAsync(cart);

        if (!isPaymentWorkflowRequired)
        {
            (warnings, var orderId, var redirect) = await ConfirmOrder(packNameCart);
            if (orderId > 0)
                return RedirectToRoute(CWCoreDefaults.PackNameCheckoutCompletedRouteName, new { orderId });
            if (redirect)
                return Empty;

            //There are warnings, redisplay form
            hasErrors = true;
        }
        else
        {
            //load payment method
            var paymentMethodSystemName = await _genericAttributeService.GetAttributeAsync<string>(customer, NopCustomerDefaults.SelectedPaymentMethodAttribute, store.Id);

            var paymentMethod = await _paymentPluginManager.LoadPluginBySystemNameAsync(paymentMethodSystemName, customer, store.Id);
            if (paymentMethod == null)
            {
                _notificationService.ErrorNotification(await _localizationService.GetResourceAsync("Plugins.CW.Core.PackNameCheckout.EnterPaymnetMethod"));
                return RedirectToRoute("PackNameCheckout");
            }

            //A method that takes the payment in its own pages never has an order placed here: it would be placed
            //UNPAID. The packname order has been validated above; the customer is handed to the method's own
            //payment step, and its placement comes back through PlaceOrderAsync as a packname order.
            if (await IsOwnPlacementPaymentMethodAsync(paymentMethodSystemName, store.Id))
            {
                //The placement that comes back from the payment method refuses a line over the subsidy cap, but only
                //after the customer has approved the payment there. Refuse here instead, while nothing is paid.
                var capWarnings = await _shoppingCartService.GetSubsidyQuantityCapWarningsAsync(customer, packNameCart);

                if (capWarnings.Count > 0)
                {
                    foreach (var capWarning in capWarnings)
                        _notificationService.ErrorNotification(capWarning);

                    return RedirectToRoute("PackNameCheckout");
                }

                await _orderProcessingService.PrepareCustomerPaymentAddressesAsync(customer, store.Id, packNameCart);

                var paymentInfoModel = await _checkoutModelFactory.PreparePaymentInfoModelAsync(paymentMethod);

                return View("~/Plugins/CW.Core/Views/PackNameCheckout/PaymentHandoff.cshtml", paymentInfoModel);
            }

            paymentWarnings = await paymentMethod.ValidatePaymentFormAsync(form);

            foreach (var warning in paymentWarnings)
                ModelState.AddModelError("", warning);

            if (ModelState.IsValid)
            {
                await _orderProcessingService.SetProcessPaymentRequestAsync(await paymentMethod.GetPaymentInfoAsync(form));

                (warnings, var orderId, var redirect) = await ConfirmOrder(packNameCart);

                if (orderId > 0)
                    return RedirectToRoute(CWCoreDefaults.PackNameCheckoutCompletedRouteName, new { orderId });
                if (redirect)
                    return Empty;

                //There are warnings, redisplay form
                hasErrors = true;
            }
        }

        //If we got this far, something failed, redisplay form
        //model
        await _packNameCheckoutModelFactory.SetShippingOptionsByPackNamesAsync(customer, store.Id, packNameCart);
        var model = await _packNameCheckoutModelFactory.PreparePackNameCheckoutModelAsync(packNameCart, customer);

        if (isPaymentWorkflowRequired)
        {
            //filter by country
            var filterByCountryId = 0;
            if (_addressSettings.CountryEnabled)
            {
                filterByCountryId = (await _customerService.GetCustomerBillingAddressAsync(customer))?.CountryId ?? 0;
            }

            //model
            var paymentMethodModel = await _checkoutModelFactory.PreparePaymentMethodModelAsync(cart, filterByCountryId);
            model.PaymentMethod = new PackNamePaymentMethodModel(paymentMethodModel);
        }
        else
        {
            model.PaymentMethod = new PackNamePaymentMethodModel { IsPaymentWorkflowRequired = false };
        }

        if (hasErrors && warnings != null && warnings.Any())
            model.Warnings = warnings;

        return View("~/Plugins/CW.Core/Views/PackNameCheckout/Checkout.cshtml", model);
    }

    /// <summary>
    /// The page a packname order lands on once it is placed: the order's packnames, where each parcel goes, and
    /// what the subsidy and store credit paid.
    /// </summary>
    /// <remarks>
    /// Takes the place of nop's completed page for packname orders only. A plain order of the same customer goes
    /// to nop's page, and nop's own checks come first: the order must exist and be the customer's, and a store that
    /// switched the completed page off goes straight to the order details, as nop does.
    /// </remarks>
    public virtual async Task<IActionResult> Completed(int? orderId)
    {
        var customer = await _workContext.GetCurrentCustomerAsync();

        if (await _customerService.IsGuestAsync(customer))
            return Challenge();

        var order = orderId > 0 ? await _orderService.GetOrderByIdAsync(orderId.Value) : null;

        if (order == null || order.Deleted || order.CustomerId != customer.Id)
            return RedirectToRoute(NopRouteNames.General.HOMEPAGE);

        if (!await _cwOrderService.IsPackNameOrderAsync(order.Id))
            return RedirectToRoute(NopRouteNames.Standard.CHECKOUT_COMPLETED, new { orderId = order.Id });

        if (_orderSettings.DisableOrderCompletedPage)
            return RedirectToRoute(NopRouteNames.Standard.ORDER_DETAILS, new { orderId = order.Id });

        var model = new PackNameCheckoutCompletedModel
        {
            Completed = await _checkoutModelFactory.PrepareCheckoutCompletedModelAsync(order),
            PackNameDetails = await _packNameOrderModelFactory.PreparePackNameOrderDetailsModelAsync(order.Id)
        };

        var languageId = (await _workContext.GetWorkingLanguageAsync()).Id;

        if (order.PaymentMethodAdditionalFeeInclTax > decimal.Zero)
        {
            var fee = _currencyService.ConvertCurrency(order.PaymentMethodAdditionalFeeInclTax, order.CurrencyRate);
            model.PaymentMethodAdditionalFee = await _priceFormatter.FormatPaymentMethodAdditionalFeeAsync(fee, true, order.CustomerCurrencyCode, languageId, true);
        }

        if (order.OrderDiscount > decimal.Zero)
        {
            var discount = _currencyService.ConvertCurrency(order.OrderDiscount, order.CurrencyRate);
            model.OrderTotalDiscount = await _priceFormatter.FormatPriceAsync(-discount, true, order.CustomerCurrencyCode, false, languageId);
        }

        return View("~/Plugins/CW.Core/Views/PackNameCheckout/Completed.cshtml", model);
    }

    public virtual async Task<IActionResult> PackNameCartItems(string viewMode)
    {
        if (!await _permissionService.AuthorizeAsync(StandardPermission.PublicStore.ENABLE_SHOPPING_CART))
            return NotFound();

        var customer = await _workContext.GetCurrentCustomerAsync();
        var store = await _storeContext.GetCurrentStoreAsync();

        var cart = await _shoppingCartService.GetPackNameShoppingCartItemsAsync(customer, 0, store.Id);

        var model = await _packNameCheckoutModelFactory.PreparePackNameCheckoutModelItemsAsync(cart, customer, viewMode);

        if (viewMode == "list")
            return PartialView("~/Plugins/CW.Core/Views/PackNameCheckout/_CartByProduct.cshtml", model);
        else
            return PartialView("~/Plugins/CW.Core/Views/PackNameCheckout/_CartByPackName.cshtml", model);
    }

    public virtual async Task<IActionResult> AddAddress(int id)
    {
        var customer = await _workContext.GetCurrentCustomerAsync();

        if (await _customerService.IsGuestAsync(customer))
            return Challenge();

        var packName = await _packNameService.GetPackNameByIdAsync(id);

        if (packName == null || packName.Deleted)
            return NotFound();

        //Same ownership question as the POST. Without it this rendered any packname's model by id -
        //the same shape that was already closed on EditPackName GET.
        var ownPackName = await _packNameService.GetPackNameByCustomerIdAsync(customer.Id);

        var mayAddress = ownPackName != null && !ownPackName.Deleted
            && (ownPackName.Id == packName.Id
                || (ownPackName.Manager && await _multiStoreService.IsPackNameOwnedByManagerId(ownPackName.Id, packName.Id)));

        if (!mayAddress)
            return AccessDeniedView();

        var model = new PackNameShippingAddressModel();
        model = await _packNameCheckoutModelFactory.PreparePackNameShippingAddressModelAsync(model, packName);

        return PartialView("~/Plugins/CW.Core/Views/PackNameCheckout/AddAddress.cshtml", model);
    }

    [HttpPost]
    public virtual async Task<IActionResult> AddAddress(PackNameShippingAddressModel model, IFormCollection form)
    {
        var customer = await _workContext.GetCurrentCustomerAsync();

        if (await _customerService.IsGuestAsync(customer))
            return Challenge();

        var packName = await _packNameService.GetPackNameByIdAsync(model.PackNameId);

        //RETURN, do not merely record: the next line dereferenced this and a forged PackNameId was a 500
        //from an anonymous request.
        if (packName == null || packName.Deleted)
            return Json(new { success = false });

        //AND IT MUST BE THIS CUSTOMER'S TO ADDRESS. Nothing here resolved the caller at all, so an
        //address could be created and mapped to any packname in any store - and it then passes
        //IsAddressValidForCheckoutAsync for that packname, so it is selectable at their checkout and
        //prints on their packing slip. Ownership is asked of the one authority, on this request.
        var ownPackName = await _packNameService.GetPackNameByCustomerIdAsync(customer.Id);

        var mayAddress = ownPackName != null && !ownPackName.Deleted
            && (ownPackName.Id == packName.Id
                || (ownPackName.Manager && await _multiStoreService.IsPackNameOwnedByManagerId(ownPackName.Id, packName.Id)));

        if (!mayAddress)
            return Json(new { success = false });

        var packNameList = await _packNameService.GetPackNameListByIdAsync(packName.PackNameListId);
        var branch = packNameList?.BranchId > 0
            ? await _branchService.GetBranchByIdAsync(packNameList.BranchId.Value)
            : null;

        //The store's own gate, from the PACKNAME'S store - never the request's. PackNameCanAddAddress
        //(AllowHomeDelivery && AllowCustomersToAddAddress) was consulted only to set a view flag, so
        //the setting hid the button and nothing refused the post.
        if (packNameList == null)
            return Json(new { success = false });

        var addressStoreSettings = await _settingService.LoadSettingAsync<MultiStoreSettings>(packNameList.StoreId);

        if (!_shoppingCartService.PackNameCanAddAddress(addressStoreSettings))
            return Json(new { success = false });

        //If the company field is enabled, we set the name as the value
        if (_customerSettings.CompanyEnabled && string.IsNullOrEmpty(model.Address.Company))
            model.Address.Company = branch?.Name ?? string.Empty;

        //custom address attributes
        string customAttributes = await _addressAttributeParser.ParseCustomAttributesAsync(form, NopCommonDefaults.AddressAttributeControlName);
        var customAttributeWarnings = await _addressAttributeParser.GetAttributeWarningsAsync(customAttributes);
        foreach (var error in customAttributeWarnings)
        {
            ModelState.AddModelError("", error);
        }

        if (ModelState.IsValid)
        {
            //Add the corresponding PackName
            var address = model.Address.ToEntity();
            address.CustomAttributes = customAttributes;
            address.CreatedOnUtc = DateTime.UtcNow;

            //some validation
            if (address.CountryId == 0)
                address.CountryId = null;
            if (address.StateProvinceId == 0)
                address.StateProvinceId = null;

            await _addressService.InsertAddressAsync(address);
            await _packNameService.InsertPackNameAddressAsync(packName, address);

            return Json(new
            {
                success = true,
                addressId = address.Id
            });
        }

        //If we got this far, something failed, redisplay form
        model = await _packNameCheckoutModelFactory.PreparePackNameShippingAddressModelAsync(model, packName, customAttributes);

        return Json(new
        {
            success = false,
            addressinfosectionhtml = await RenderPartialViewToStringAsync("~/Plugins/CW.Core/Views/PackNameCheckout/AddAddress.cshtml", model)
        });
    }

    /// <summary>
    /// Check out one vendor's lines: the others wait in nop's stash until the order is placed or "All" is chosen
    /// again. nop offers this on its own cart page, which a packname customer never sees.
    /// </summary>
    /// <param name="vendorId">The vendor to check out; 0 for all</param>
    [HttpPost]
    public virtual async Task<IActionResult> ChangeVendor(int vendorId)
    {
        if (!_shoppingCartSettings.VendorEnabled)
            return Json(new { success = false });

        var customer = await _workContext.GetCurrentCustomerAsync();
        var store = await _storeContext.GetCurrentStoreAsync();

        await _shoppingCartService.SetShoppingCartVendorAsync(customer, vendorId, store.Id);

        return Json(new { success = true });
    }

    [HttpPost]
    public virtual async Task<IActionResult> UpdateShippingAddress(int packNameId, int addressId)
    {
        var customer = await _workContext.GetCurrentCustomerAsync();
        var store = await _storeContext.GetCurrentStoreAsync();
        var packNamesCart = await _shoppingCartService.GetPackNameShoppingCartItemsAsync(customer, 0, store.Id);

        try
        {
            if (packNamesCart?.Count == 0)
                throw new Exception("Your cart is empty");

            var storeSettings = await _settingService.LoadSettingAsync<MultiStoreSettings>(packNamesCart.First().StoreId);

            //The packname has to be in this cart. Without it the branch resolved to 0 and the address was
            //checked against the packname's own addresses only.
            var packNameItem = packNamesCart.FirstOrDefault(x => x.PackNameId == packNameId);

            //A refused address is answered as a refusal: this used to return success and leave the
            //dropdown showing an address that was never saved.
            if (packNameItem == null
                || !await _shoppingCartService.IsAddressValidForCheckoutAsync(packNameItem.BranchId, packNameId, addressId, customer, store.Id, storeSettings))
            {
                return Json(new
                {
                    success = false,
                    message = await _localizationService.GetResourceAsync("Plugins.CW.Core.PackNameCheckout.InvalidShippingAddress")
                });
            }

            var assignments = await _shippingAssignmentService.LoadAsync(customer, store.Id);
            assignments.SetAddressId(packNameId, addressId);
            await _shippingAssignmentService.SaveAsync(customer, store.Id, assignments);

            //Only this packname's address changed, so only its rate is asked for again; the others keep the
            //options they were offered.
            await _packNameCheckoutModelFactory.SetShippingOptionsByPackNamesAsync(customer, store.Id, packNamesCart, packNameId);

            //update blocks
            var totalssectionhtml = await RenderViewComponentToStringAsync(typeof(PackNameOrderTotalsViewComponent), new { isEditable = true });

            return Json(new
            {
                success = true,
                totalssectionhtml
            });
        }
        catch (Exception exc)
        {
            return Json(new { success = false, message = exc.Message });
        }
    }

    public virtual async Task<IActionResult> GetPaymentMethods()
    {
        var customer = await _workContext.GetCurrentCustomerAsync();
        var store = await _storeContext.GetCurrentStoreAsync();

        var cart = await _shoppingCartService.GetShoppingCartAsync(customer, ShoppingCartType.ShoppingCart, store.Id);

        //Check whether payment workflow is required
        var isPaymentWorkflowRequired = await _orderProcessingService.IsPaymentWorkflowRequiredAsync(cart, false);

        var model = new PackNamePaymentMethodModel { IsPaymentWorkflowRequired = isPaymentWorkflowRequired };

        if (isPaymentWorkflowRequired)
        {
            //filter by country
            var filterByCountryId = 0;
            if (_addressSettings.CountryEnabled)
            {
                filterByCountryId = (await _customerService.GetCustomerBillingAddressAsync(customer))?.CountryId ?? 0;
            }

            //model
            model = new PackNamePaymentMethodModel(await _checkoutModelFactory.PreparePaymentMethodModelAsync(cart, filterByCountryId));

            if (_paymentSettings.BypassPaymentMethodSelectionIfOnlyOne &&
                model.PaymentMethods.Count == 1 && !model.DisplayRewardPoints)
            {
                //if we have only one payment method and reward points are disabled or the current customer doesn't have any reward points
                //so customer doesn't have to choose a payment method
                await _genericAttributeService.SaveAttributeAsync(customer, NopCustomerDefaults.SelectedPaymentMethodAttribute, model.PaymentMethods[0].PaymentMethodSystemName, store.Id);
            }
        }

        return PartialView("~/Plugins/CW.Core/Views/PackNameCheckout/_PaymentMethods.cshtml", model);
    }

    [HttpPost]
    public async Task<JsonResult> UpdatePaymentMethod(string paymentoption)
    {
        try
        {
            //validation
            if (_orderSettings.CheckoutDisabled)
                throw new Exception(await _localizationService.GetResourceAsync("Checkout.Disabled"));

            var customer = await _workContext.GetCurrentCustomerAsync();
            var store = await _storeContext.GetCurrentStoreAsync();

            var cart = await _shoppingCartService.GetShoppingCartAsync(customer, ShoppingCartType.ShoppingCart, store.Id);

            if (!cart.Any())
                throw new Exception("Your cart is empty");

            if (await _customerService.IsGuestAsync(customer) && !_orderSettings.AnonymousCheckoutAllowed)
                throw new Exception("Anonymous checkout is not allowed");

            //payment method 
            if (string.IsNullOrEmpty(paymentoption))
                throw new Exception("Selected payment method can't be parsed");

            ////reward points
            //if (_rewardPointsSettings.Enabled)
            //{
            //    await _genericAttributeService.SaveAttributeAsync(customer,
            //        NopCustomerDefaults.UseRewardPointsDuringCheckoutAttribute, model.UseRewardPoints,
            //        store.Id);
            //}

            //Check whether payment workflow is required
            var isPaymentWorkflowRequired = await _orderProcessingService.IsPaymentWorkflowRequiredAsync(cart);
            if (!isPaymentWorkflowRequired)
            {
                //payment is not required
                await _genericAttributeService.SaveAttributeAsync<string>(customer,
                    NopCustomerDefaults.SelectedPaymentMethodAttribute, null, store.Id);

                return Json(new
                {
                    success = false,
                    message = "Payment method is not required",
                });
            }

            //parse selected method 
            var paymentMethod = await _paymentPluginManager.LoadPluginBySystemNameAsync(paymentoption, customer, store.Id);

            if (!_paymentPluginManager.IsPluginActive(paymentMethod))
                throw new Exception("Selected payment method can't be parsed");

            //save
            await _genericAttributeService.SaveAttributeAsync(customer, NopCustomerDefaults.SelectedPaymentMethodAttribute, paymentoption, store.Id);

            //return payment info page. A method that takes the payment in its own pages gets a note instead: its
            //payment info component would take the customer away from the checkout the moment it is selected,
            //before the destinations are checked. The Checkout button hands over to it.
            string paymentinfosectionhtml;

            if (await IsOwnPlacementPaymentMethodAsync(paymentoption, store.Id))
            {
                var note = await _localizationService.GetResourceAsync("Plugins.CW.Core.PackNameCheckout.OwnPlacementPaymentNote");
                paymentinfosectionhtml = $"<div class=\"info\">{System.Net.WebUtility.HtmlEncode(note)}</div>";
            }
            else
            {
                var paymenInfoModel = await _checkoutModelFactory.PreparePaymentInfoModelAsync(paymentMethod);
                paymentinfosectionhtml = await RenderPartialViewToStringAsync("~/Plugins/CW.Core/Views/PackNameCheckout/_PaymentMethodInfo.cshtml", paymenInfoModel);
            }

            //update blocks
            var totalssectionhtml = await RenderViewComponentToStringAsync(typeof(PackNameOrderTotalsViewComponent), new { isEditable = true });

            return Json(new
            {
                success = true,
                totalssectionhtml,
                paymentinfosectionhtml
            });
        }
        catch (Exception exc)
        {
            await _logger.WarningAsync(exc.Message, exc, await _workContext.GetCurrentCustomerAsync());
            return Json(new { success = false, message = exc.Message });
        }
    }

    public virtual async Task<IActionResult> CheckoutAttributes()
    {
        var customer = await _workContext.GetCurrentCustomerAsync();
        var store = await _storeContext.GetCurrentStoreAsync();
        var packNameCart = await _shoppingCartService.GetPackNameShoppingCartItemsAsync(customer, 0, store.Id);
        var cart = packNameCart.Cast<ShoppingCartItem>().ToList();

        var model = new PackNameShoppingCartModel()
        {
            CheckoutAttributes = await _shoppingCartModelFactory.PreparePackNameCheckoutAttributeModelsAsync(cart)
        };

        return PartialView("~/Plugins/CW.Core/Views/PackNameCheckout/_CheckoutAttributes.cshtml", model);
    }

    [HttpPost]
    public virtual async Task<IActionResult> CheckoutAttributeChange(IFormCollection form)
    {
        var customer = await _workContext.GetCurrentCustomerAsync();
        var store = await _storeContext.GetCurrentStoreAsync();
        var cart = await _shoppingCartService.GetShoppingCartAsync(customer, ShoppingCartType.ShoppingCart, store.Id);

        //save selected attributes
        await ParseAndSaveCheckoutAttributesAsync(cart, form);
        var attributeXml = await _genericAttributeService.GetAttributeAsync<string>(customer,
            NopCustomerDefaults.CheckoutAttributes, store.Id);

        //conditions
        var enabledAttributeIds = new List<int>();
        var disabledAttributeIds = new List<int>();
        var excludeShippableAttributes = !await _shoppingCartService.ShoppingCartRequiresShippingAsync(cart);
        var checkoutAttributes =
            await _checkoutAttributeService.GetAllAttributesAsync(_staticCacheManager, _storeMappingService, store.Id, excludeShippableAttributes);
        foreach (var attribute in checkoutAttributes)
        {
            var conditionMet = await _checkoutAttributeParser.IsConditionMetAsync(attribute.ConditionAttributeXml, attributeXml);
            if (conditionMet.HasValue)
            {
                if (conditionMet.Value)
                    enabledAttributeIds.Add(attribute.Id);
                else
                    disabledAttributeIds.Add(attribute.Id);
            }
        }

        //update blocks
        var selectedcheckoutattributesssectionhtml = await RenderViewComponentToStringAsync(typeof(SelectedCheckoutAttributesViewComponent));

        return Json(new
        {
            selectedcheckoutattributesssectionhtml,
            enabledattributeids = enabledAttributeIds.ToArray(),
            disabledattributeids = disabledAttributeIds.ToArray()
        });
    }

    [HttpPost]
    public virtual async Task<IActionResult> UpdateShoppingCartItem(int itemId, int quantity)
    {
        var customer = await _workContext.GetCurrentCustomerAsync();
        var store = await _storeContext.GetCurrentStoreAsync();
        var packNamesCart = await _shoppingCartService.GetPackNameShoppingCartItemsAsync(customer, 0, store.Id);
        var cart = packNamesCart.Cast<ShoppingCartItem>().ToList();

        var cartItem = cart.FirstOrDefault(item => item.Id == itemId);
        var warnings = new List<string>();

        //Same rule as RemoveShoppingCartItem: a line that has gone is reported, never answered with success.
        if (cartItem == null)
        {
            return Json(new
            {
                success = false,
                message = await _localizationService.GetResourceAsync("Plugins.CW.Core.ShoppingCart.ItemNotFound")
            });
        }

        if (cartItem.Quantity != quantity)
        {
            //A returned item is the cart-side half of an approved exchange, so it has a floor but no
            //ceiling. It may go up — buying more of the same product is ordinary shopping, and mixed carts
            //are expressly allowed — but never down, because the return was approved
            //for an exact quantity and the fee was priced over it. Removal is an update to zero, so it is
            //caught here as well as by RemoveShoppingCartItem, which guards the button.
            var returnedItems = await _exchangeService.GetReturnRequestItemsForCartItemsAsync([cartItem.Id]);

            if (returnedItems.Any())
            {
                var requiredQuantity = 0;

                foreach (var returnedItem in returnedItems)
                {
                    var linkedRequest = await _returnRequestService.GetReturnRequestByIdAsync(returnedItem.ReturnRequestId);

                    requiredQuantity += linkedRequest?.Quantity ?? 0;
                }

                //Fail closed. A floor of zero means the linked return request could not be read, and
                //without a floor "may not go below it" permits everything — including a removal typed as
                //a zero, which is the walk-around the previous guard existed to stop.
                if (requiredQuantity <= 0 || quantity < requiredQuantity)
                {
                    return Json(new
                    {
                        success = false,
                        message = string.Format(
                            await _localizationService.GetResourceAsync("Plugins.CW.Core.ShoppingCart.CannotChangeReturnedItem"),
                            requiredQuantity)
                    });
                }
            }

            //try to update cart items with new quantities and get warnings
            warnings = (await _shoppingCartService.UpdateShoppingCartItemAsync(customer,
                    cartItem.Id, cartItem.AttributesXml, cartItem.CustomerEnteredPrice,
                    cartItem.RentalStartDateUtc, cartItem.RentalEndDateUtc, quantity, true)).ToList();
        }

        //The line belongs to one packname, so only that packname's rate is asked for again, with the cart as it is
        //after the update: the list read above still holds the old quantity.
        var changedPackNameId = packNamesCart.FirstOrDefault(item => item.Id == itemId)?.PackNameId;
        packNamesCart = await _shoppingCartService.GetPackNameShoppingCartItemsAsync(customer, 0, store.Id);
        await _packNameCheckoutModelFactory.SetShippingOptionsByPackNamesAsync(customer, store.Id, packNamesCart, changedPackNameId);

        //update blocks
        var totalssectionhtml = await RenderViewComponentToStringAsync(typeof(PackNameOrderTotalsViewComponent), new { isEditable = true });

        //A quantity change moves the header count and the flyout contents exactly as a removal does.
        var fullCart = await _shoppingCartService.GetShoppingCartAsync(customer, ShoppingCartType.ShoppingCart, store.Id);

        var updatetopcartsectionhtml = string.Format(
            await _localizationService.GetResourceAsync("ShoppingCart.HeaderQuantity"),
            fullCart.Sum(item => item.Quantity));

        var updateflyoutcartsectionhtml = await RenderViewComponentToStringAsync(typeof(FlyoutShoppingCartViewComponent));

        return Json(new
        {
            success = true,
            warnings,
            totalssectionhtml,
            updatetopcartsectionhtml,
            updateflyoutcartsectionhtml,
        });
    }

    [HttpPost]
    public virtual async Task<IActionResult> RemoveShoppingCartItem(int itemId)
    {
        var customer = await _workContext.GetCurrentCustomerAsync();
        var store = await _storeContext.GetCurrentStoreAsync();
        var packNamesCart = await _shoppingCartService.GetPackNameShoppingCartItemsAsync(customer, 0, store.Id);
        var cart = packNamesCart.Cast<ShoppingCartItem>().ToList();

        var cartItem = cart.FirstOrDefault(item => item.Id == itemId);

        //The flyout and the checkout draw only lines of this cart, so an id that is not in it is a line that
        //has gone since the page was drawn. Say so: a silent success left the row on the screen.
        if (cartItem == null)
        {
            return Json(new
            {
                success = false,
                message = await _localizationService.GetResourceAsync("Plugins.CW.Core.ShoppingCart.ItemNotFound")
            });
        }

        //A returned item is the cart-side half of an open return request; removing it would orphan
        //the request. The cart views never draw the button for one (DisableRemoval) — this is the
        //matching server-side guard, which matters now that the flyout posts here from every page.
        var returnedItems = await _exchangeService.GetReturnRequestItemsForCartItemsAsync([cartItem.Id]);

        if (returnedItems.Any())
        {
            return Json(new
            {
                success = false,
                message = await _localizationService.GetResourceAsync("Plugins.CW.Core.ShoppingCart.CannotRemoveReturnedItem")
            });
        }

        //try to update cart items with new quantities and get warnings
        var warnings = (await _shoppingCartService.UpdateShoppingCartItemAsync(customer,
                cartItem.Id, cartItem.AttributesXml, cartItem.CustomerEnteredPrice,
                cartItem.RentalStartDateUtc, cartItem.RentalEndDateUtc, 0, true)).ToList();

        //The line belonged to one packname, so only that packname's rate is asked for again, with the cart as it is
        //after the removal: the list read above still holds the removed line.
        var changedPackNameId = packNamesCart.FirstOrDefault(item => item.Id == itemId)?.PackNameId;
        packNamesCart = await _shoppingCartService.GetPackNameShoppingCartItemsAsync(customer, 0, store.Id);
        await _packNameCheckoutModelFactory.SetShippingOptionsByPackNamesAsync(customer, store.Id, packNamesCart, changedPackNameId);

        //update blocks
        var totalssectionhtml = await RenderViewComponentToStringAsync(typeof(PackNameOrderTotalsViewComponent), new { isEditable = true });

        //The flyout cart and the header quantity sit on every page and are now a place items can be
        //removed from, so both have to come back refreshed — the header count is the whole cart across
        //every packname, which is what nop's own AddToCart responses return.
        var fullCart = await _shoppingCartService.GetShoppingCartAsync(customer, ShoppingCartType.ShoppingCart, store.Id);

        var updatetopcartsectionhtml = string.Format(
            await _localizationService.GetResourceAsync("ShoppingCart.HeaderQuantity"),
            fullCart.Sum(item => item.Quantity));

        var updateflyoutcartsectionhtml = await RenderViewComponentToStringAsync(typeof(FlyoutShoppingCartViewComponent));

        return Json(new
        {
            success = true,
            warnings,
            totalssectionhtml,
            updatetopcartsectionhtml,
            updateflyoutcartsectionhtml,
        });
    }

    [HttpPost]
    public virtual async Task<IActionResult> UpdateShippingMethod(int packNameId, string shippingMethod)
    {
        var customer = await _workContext.GetCurrentCustomerAsync();
        var store = await _storeContext.GetCurrentStoreAsync();
        var packNamesCart = await _shoppingCartService.GetPackNameShoppingCartItemsAsync(customer, 0, store.Id);

        try
        {
            if (packNamesCart?.Count == 0)
                throw new Exception("Your cart is empty");

            if (string.IsNullOrEmpty(shippingMethod))
                throw new Exception("Shipping method selection is not valid");

            var assignments = await _shippingAssignmentService.LoadAsync(customer, store.Id);

            var shippingOptions = assignments.GetOfferedOptions(packNameId);
            if (!shippingOptions.Any())
                throw new Exception("Shipping method selection is not valid");

            var shippingOption = shippingOptions.Find(so =>
                !string.IsNullOrEmpty(so.Name)
                && so.Name.Equals(shippingMethod, StringComparison.InvariantCultureIgnoreCase))
                ?? throw new Exception("Shipping method selection is not valid");

            assignments.SetSelectedOption(packNameId, shippingOption);
            await _shippingAssignmentService.SaveAsync(customer, store.Id, assignments);

            //Only this packname's method changed, so only its rate is asked for again; the others keep the
            //options they were offered.
            await _packNameCheckoutModelFactory.SetShippingOptionsByPackNamesAsync(customer, store.Id, packNamesCart, packNameId);

            //update blocks
            var totalssectionhtml = await RenderViewComponentToStringAsync(typeof(PackNameOrderTotalsViewComponent), new { isEditable = true });

            return Json(new
            {
                success = true,
                totalssectionhtml,
            });
        }
        catch (Exception exc)
        {
            return Json(new { success = false, message = exc.Message });
        }
    }

    #endregion
}
