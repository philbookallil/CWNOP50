using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Nop.Core;
using Nop.Core.Caching;
using Nop.Core.Domain.Catalog;
using Nop.Core.Domain.Customers;
using Nop.Core.Domain.Messages;
using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Shipping;
using Nop.Core.Events;
using Nop.Data;
using Nop.Plugin.CW.Core.BusinessLogic;
using Nop.Plugin.CW.Core.Domain.Orders;
using Nop.Plugin.CW.Core.Domain.PackNames;
using Nop.Plugin.CW.Core.Domain.Settings;
using Nop.Plugin.CW.Core.Domain.Subsidies;
using Nop.Plugin.CW.Core.Services.BulkOrder;
using Nop.Plugin.CW.Core.Services.CreditNotes;
using Nop.Plugin.CW.Core.Services.Exchanges;
using Nop.Plugin.CW.Core.Services.Messages;
using Nop.Plugin.CW.Core.Services.Orders;
using Nop.Plugin.CW.Core.Services.PackNames;
using Nop.Plugin.CW.Core.Services.Security;
using Nop.Plugin.CW.Core.Services.Settings;
using Nop.Plugin.CW.Core.Services.Subsidies;
using Nop.Services.Common;
using Nop.Services.Events;
using Nop.Services.Localization;
using Nop.Services.Messages;
using Nop.Services.Orders;
using Nop.Services.Plugins;
using Nop.Services.Security;
using Nop.Services.Shipping;
using Nop.Web.Framework;
using Nop.Web.Framework.Events;
using Nop.Web.Framework.Menu;
using Nop.Web.Framework.Models;
using Nop.Web.Framework.UI;
using Nop.Web.Models.Catalog;
using Nop.Web.Models.Customer;

namespace Nop.Plugin.CW.Core.Services;

public class EventConsumer : BaseAdminMenuCreatedEventConsumer,
    IConsumer<PageRenderingEvent>,
    IConsumer<MultiStoreSettingsChangedEvent>,
    IConsumer<EntityUpdatedEvent<Category>>,
    IConsumer<EntityInsertedEvent<Category>>,
    IConsumer<EntityUpdatedEvent<Manufacturer>>,
    IConsumer<EntityInsertedEvent<Manufacturer>>,
    IConsumer<EntityUpdatedEvent<Product>>,
    IConsumer<EntityInsertedEvent<Product>>,
    IConsumer<CustomerLoggedinEvent>,
    IConsumer<EntityUpdatedEvent<ShoppingCartItem>>,
    IConsumer<AdminMenuCreatedEvent>,
    IConsumer<AdditionalTokensAddedEvent>,
    IConsumer<EntityDeletedEvent<PackName>>,
    IConsumer<EntityDeletedEvent<Customer>>,
    IConsumer<EntityDeletedEvent<ProductGroup>>,
    IConsumer<EntityDeletedEvent<Subsidy>>,
    IConsumer<ModelPreparedEvent<BaseNopModel>>,
    IConsumer<ShipmentSentEvent>,
    IConsumer<ShipmentDeliveredEvent>,
    IConsumer<ShoppingCartItemMovedToOrderItemEvent>
{
    #region Fields

    protected readonly IPermissionService _permissionService;
    protected readonly ILocalizationService _localizationService;
    protected readonly IAdminMenu _adminMenu;
    protected readonly IStaticCacheManager _staticCacheManager;
    protected readonly IHttpContextAccessor _httpContextAccessor;
    protected readonly IAclOverrideService _aclService;
    protected readonly IPackNameService _packNameService;
    protected readonly IRepository<PackNameCartItem> _packNameCartItemRepository;
    protected readonly IEventPublisher _eventPublisher;
    protected readonly IWorkContext _workContext;
    protected readonly IPackNameContext _packNameContext;
    protected readonly IRepository<SubsidyPackName> _subsidyPackNameRepository;
    protected readonly ISubsidyService _subsidyService;
    protected readonly ISubsidyReportingService _subsidyReportingService;
    protected readonly ICWFeatureService _cwFeatureService;
    protected readonly ICreditNotesService _creditNotesService;
    protected readonly ICWOrderService _cwOrderService;
    protected readonly IOrderService _orderService;
    protected readonly IShipmentService _shipmentService;
    protected readonly IExchangeService _exchangeService;
    protected readonly IReturnResolutionLogic _returnResolutionLogic;
    protected readonly IBulkOrderProductPageService _bulkOrderProductPageService;
    protected readonly BulkOrderProductPageContext _bulkOrderProductPageContext;
    protected readonly IGenericAttributeService _genericAttributeService;
    protected readonly IShoppingCartOverrideService _shoppingCartService;

    #endregion

    #region Ctor

    public EventConsumer(IPermissionService permissionService,
        ILocalizationService localizationService,
        IPluginManager<IPlugin> pluginManager,
        IAdminMenu adminMenu,
        IStaticCacheManager staticCacheManager,
        IHttpContextAccessor httpContextAccessor,
        IAclOverrideService aclService,
        IPackNameService packNameService,
        IPackNameContext packNameContext,
        IWorkContext workContext,
        IRepository<PackNameCartItem> packNameCartItemRepository,
        IEventPublisher eventPublisher,
        IRepository<SubsidyPackName> subsidyPackNameRepository,
        ISubsidyService subsidyService,
        ISubsidyReportingService subsidyReportingService,
        ICWFeatureService cwFeatureService,
        ICreditNotesService creditNotesService,
        ICWOrderService cwOrderService,
        IOrderService orderService,
        IShipmentService shipmentService,
        IExchangeService exchangeService,
        IReturnResolutionLogic returnResolutionLogic,
        IBulkOrderProductPageService bulkOrderProductPageService,
        BulkOrderProductPageContext bulkOrderProductPageContext,
        IGenericAttributeService genericAttributeService,
        IShoppingCartOverrideService shoppingCartService) : base(pluginManager)
    {
        _permissionService = permissionService;
        _localizationService = localizationService;
        _adminMenu = adminMenu;
        _staticCacheManager = staticCacheManager;
        _httpContextAccessor = httpContextAccessor;
        _aclService = aclService;
        _packNameService = packNameService;
        _packNameCartItemRepository = packNameCartItemRepository;
        _eventPublisher = eventPublisher;
        _workContext = workContext;
        _packNameContext = packNameContext;
        _subsidyPackNameRepository = subsidyPackNameRepository;
        _subsidyService = subsidyService;
        _subsidyReportingService = subsidyReportingService;
        _cwFeatureService = cwFeatureService;
        _creditNotesService = creditNotesService;
        _cwOrderService = cwOrderService;
        _orderService = orderService;
        _shipmentService = shipmentService;
        _exchangeService = exchangeService;
        _returnResolutionLogic = returnResolutionLogic;
        _bulkOrderProductPageService = bulkOrderProductPageService;
        _bulkOrderProductPageContext = bulkOrderProductPageContext;
        _genericAttributeService = genericAttributeService;
        _shoppingCartService = shoppingCartService;
    }

    #endregion

    #region Utilities

    /// <summary>
    /// Gets the plugin system name
    /// </summary>
    protected override string PluginSystemName => CWCoreDefaults.SystemName;

    // Insert BEFORE the "Catalog" menu item
    protected override string BeforeMenuSystemName => "Catalog";

    // Controls insertion behavior
    protected override MenuItemInsertType InsertType => MenuItemInsertType.Before;

    protected override async Task<bool> CheckAccessAsync()
    {
        return await _permissionService.AuthorizeAsync(
            StandardPermission.Configuration.MANAGE_PLUGINS); //TODO: Should we check each permission?
    }

    protected override async Task<AdminMenuItem> GetAdminMenuItemAsync(IPlugin plugin)
    {
        var descriptor = plugin.PluginDescriptor;

        var anywhere = await _cwFeatureService.GetFeaturesForAnyStoreAsync();
        var exchangesAnywhere = anywhere.Exchanges;
        var creditNotesAnywhere = anywhere.CreditNotes;

        return new AdminMenuItem
        {
            SystemName = descriptor.SystemName,
            Title = await _localizationService.GetResourceAsync("Plugins.CW.Core.Admin.Menu"),
            IconClass = "far fa-dot-circle",
            Visible = true,
            ChildNodes = new List<AdminMenuItem>
            {
                new()
                {
                    Visible = true,
                    SystemName = CWCoreDefaults.MultiStoreAdminMenuSystemName,
                    Title = await _localizationService.GetResourceAsync("Plugins.CW.Core.Admin.MultiStores"),
                    Url = _adminMenu.GetMenuItemUrl("MultiStore", "List"),
                    IconClass = "far fa-circle",
                    PermissionNames = new List<string> { CWPermissionConfigManager.ADMIN_ACCESS_MULTISTORE, CWPermissionConfigManager.ADMIN_ACCESS_MULTISTORE_SETTINGS }
                },
                new()
                {
                    Visible = true,
                    SystemName = CWCoreDefaults.DefaultStoreSettingsAdminMenuSystemName,
                    Title = await _localizationService.GetResourceAsync("Plugins.CW.Core.Admin.MultiStores.Default.Settings"),
                    Url = _adminMenu.GetMenuItemUrl("MultiStore", "Settings"),
                    IconClass = "far fa-circle",
                    PermissionNames = new List<string> { CWPermissionConfigManager.ADMIN_ACCESS_DEFAULT_STORE_SETTINGS }
                },
                new()
                {
                    Visible = true,
                    SystemName = CWCoreDefaults.BranchAdminMenuSystemName,
                    Title = await _localizationService.GetResourceAsync("Plugins.CW.Core.Admin.Menu.Branches"),
                    Url = _adminMenu.GetMenuItemUrl("Branch", "Index"),
                    IconClass = "far fa-circle",
                    PermissionNames = new List<string> { CWPermissionConfigManager.ADMIN_ACCESS_BRANCH }
                },
                new()
                {
                    Visible = exchangesAnywhere || creditNotesAnywhere,
                    SystemName = CWCoreDefaults.ExchangesAdminMenuSystemName,
                    Title = await _localizationService.GetResourceAsync("Plugins.CW.Core.Admin.Menu.ExchangesAndReturns"),
                    IconClass = "fa fa-exchange",
                    PermissionNames = new List<string> { ExchangesPermissionConfigManager.ACCESS_EXCHANGES },
                    ChildNodes = new List<AdminMenuItem>
                    {
                        new()
                        {
                            Visible = exchangesAnywhere,
                            Title = await _localizationService.GetResourceAsync("Plugins.CW.Core.Admin.Menu.FeeSettings"),
                            Url = _adminMenu.GetMenuItemUrl("ExchangesConfig", "GlobalFeeSettings"),
                            SystemName = CWCoreDefaults.FeeSettingsAdminMenuSystemName,
                            IconClass = "far fa-circle",
                            PermissionNames = new List<string> { CWPermissionConfigManager.ADMIN_ACCESS_EXCHANGES }
                        },
                        new()
                        {
                            Title = await _localizationService.GetResourceAsync("Plugins.CW.Core.Admin.Menu.CreditNotesReport"),
                            Url = _adminMenu.GetMenuItemUrl("CreditNotes", "MainReport"),
                            SystemName = CWCoreDefaults.CreditNotesReportAdminMenuSystemName,
                            Visible = creditNotesAnywhere,
                            IconClass = "far fa-dot-circle",

                        },

                    }
                }
            }
        };
    }

    /// <summary>
    /// Whether the request is nop's product form posted for another product than this one
    /// </summary>
    /// <param name="request">The current request, if any</param>
    /// <param name="product">The product being saved</param>
    /// <returns>True when the form's Id field names another product</returns>
    protected static bool IsAnotherProductsForm(HttpRequest request, Product product) =>
        request is { HasFormContentType: true }
        && request.Path.StartsWithSegments("/Admin/Product/Edit")
        && int.TryParse(request.Form["Id"], out var postedId)
        && postedId != product.Id;

    /// <remarks>
    /// Saves onto the event's OWN entity, never a copy loaded by id. On Create, nop inserts the entity, raises
    /// this event, and then updates the same instance again further down the action - with the
    /// <c>SubjectToAcl</c> it still holds. Setting it on a reloaded copy was overwritten by that update, so a
    /// department ACL chosen on Create left the entity unrestricted until it was saved a second time.
    /// </remarks>
    protected static async Task HandleEntityEventAsync<TEntity>(HttpRequest request, string expectedPath, TEntity entity, Func<TEntity, List<int>, Task> saveAclDepartmentAsync)
    {
        if (request == null) return;

        // Only act on specific Admin area POSTs
        if (!request.Path.StartsWithSegments(expectedPath))
            return;

        // Read the selected departments values
        var departmentIds = request.Form["SelectedDepartmentIds"]
            .Where(v => int.TryParse(v, out _))
            .Select(int.Parse)
            .ToList();

        await saveAclDepartmentAsync(entity, departmentIds);
    }

    protected async Task SetPackNameContext(Customer customer)
    {
        var packName = await _packNameService.GetPackNameByCustomerIdAsync(customer.Id);

        //If the customer have a packName assigned, we set the packName in the context for ordering.
        //If the packName can not order, no products (or categories) are going to be shown but the packName can go to My Account to check his historical
        if (packName != null)
        {
            var branch = await _packNameService.GetBranchByPackNameIdAsync(packName.Id);

            //The branch should never be null, but we do the validation anyway
            if (branch != null)
                await _packNameService.SetPackNameAsync(packName.Id, packName.PackNameListId, customer, branch);
        }
    }

    protected bool IsAdminPage(string controllerName, params string[] actionNames)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext is null)
            return false;

        if (httpContext.GetRouteValue("area") is not string area || area != AreaNames.ADMIN)
            return false;

        if (httpContext.GetRouteValue("controller") is not string controller ||
            !controller.Equals(controllerName, StringComparison.OrdinalIgnoreCase))
            return false;

        return httpContext.GetRouteValue("action") is string action &&
               actionNames.Any(a => action.Equals(a, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Moves the OrderShipping rows of every destination this shipment touched, using the same rules
    /// OrderProcessingService applies to the order as a whole — one level down.
    /// </summary>
    protected async Task AdvancePackNameShippingAsync(Shipment shipment, bool delivered)
    {
        if (shipment == null)
            return;

        var destinations = await _cwOrderService.GetOrderShippingDestinationsAsync(shipment.OrderId);

        //not a packname order — nop already moved Order.ShippingStatus and there is nothing else to do
        if (destinations.Count == 0)
            return;

        var order = await _orderService.GetOrderByIdAsync(shipment.OrderId);

        if (order == null)
            return;

        var shipmentItems = await _shipmentService.GetShipmentItemsByShipmentIdAsync(shipment.Id);
        var touchedOrderItemIds = shipmentItems.Select(i => i.OrderItemId).ToHashSet();

        var touched = destinations
            .Where(d => d.OrderItemIds.Any(touchedOrderItemIds.Contains))
            .ToList();

        if (touched.Count == 0)
            return;

        //every shipment on this order, with its items, so each destination can be judged on its own
        var orderShipments = await _shipmentService.GetShipmentsByOrderIdAsync(shipment.OrderId);
        var itemsByShipment = new Dictionary<int, IList<ShipmentItem>>();

        foreach (var orderShipment in orderShipments)
            itemsByShipment[orderShipment.Id] = await _shipmentService.GetShipmentItemsByShipmentIdAsync(orderShipment.Id);

        var allShipping = await _cwOrderService.GetOrderShippingAsync(shipment.OrderId);

        foreach (var destination in touched)
        {
            var destinationItemIds = destination.OrderItemIds.ToHashSet();

            //shipments carrying at least one of this destination's items
            var relevant = orderShipments
                .Where(s => itemsByShipment[s.Id].Any(i => destinationItemIds.Contains(i.OrderItemId)))
                .ToList();

            //nop: HasItemsToAddToShipmentAsync — quantity ordered but not yet in any shipment.
            //Computed from itemsByShipment, which we already loaded: calling
            //GetTotalNumberOfItemsCanBeAddedToShipmentAsync here would re-read every shipment per item.
            var hasItemsToAdd = false;

            foreach (var orderItemId in destination.OrderItemIds)
            {
                var orderItem = await _orderService.GetOrderItemByIdAsync(orderItemId);

                if (orderItem == null)
                    continue;

                var inShipments = itemsByShipment.Values
                    .SelectMany(items => items)
                    .Where(i => i.OrderItemId == orderItemId)
                    .Sum(i => i.Quantity);

                if (orderItem.Quantity - inShipments > 0)
                {
                    hasItemsToAdd = true;
                    break;
                }
            }

            //nop: HasItemsToShipAsync / HasItemsToReadyForPickupAsync — both are "in a shipment that has
            //not left yet", and which date applies depends on PickupInStore, exactly as OrderService does
            var hasItemsToDispatch = order.PickupInStore
                ? relevant.Any(s => !s.ReadyForPickupDateUtc.HasValue)
                : relevant.Any(s => !s.ShippedDateUtc.HasValue);

            ShippingStatus? resolved;

            if (delivered)
            {
                var hasItemsToDeliver = relevant.Any(s => !s.DeliveryDateUtc.HasValue);

                //nop DeliverAsync:2271 — Delivered only when nothing is outstanding anywhere. Otherwise
                //the status is left exactly as it was, which is why this is nullable rather than a value.
                resolved = !hasItemsToAdd && !hasItemsToDispatch && !hasItemsToDeliver
                    ? ShippingStatus.Delivered
                    : null;
            }
            else
            {
                //nop ShipAsync:2190 — PartiallyShipped while anything is still unshipped or unpacked
                resolved = hasItemsToAdd || hasItemsToDispatch
                    ? ShippingStatus.PartiallyShipped
                    : ShippingStatus.Shipped;
            }

            if (resolved == null)
                continue;

            foreach (var packNameId in destination.PackNameIds)
            {
                var orderShipping = allShipping.FirstOrDefault(s => s.PackNameId == packNameId);

                if (orderShipping == null || orderShipping.ShippingStatus == resolved.Value)
                    continue;

                orderShipping.ShippingStatus = resolved.Value;
                await _cwOrderService.UpdateOrderShippingAsync(orderShipping);
            }
        }
    }


    /// <summary>
    /// Credit notes and subsidies each add an entry to the My Account menu, both positioned after Orders.
    /// Extracted from the ModelPrepared handler when that became a dispatch; the body is unchanged, and the
    /// two lines that opened it — the type test and its early return — are now the `case` that reaches here.
    /// </summary>
    protected async Task AddCustomerNavigationItemsAsync(CustomerNavigationModel navigationModel)
    {
        var customer = await _workContext.GetCurrentCustomerAsync();
        var features = await _cwFeatureService.GetFeaturesAsync();

        //add a new menu item
        var ordersNavItem = navigationModel.CustomerNavigationItems.FirstOrDefault(item => item.Tab == (int)CustomerNavigationEnum.Orders);
        var position = navigationModel.CustomerNavigationItems.IndexOf(ordersNavItem) + 1;

        if (features.CreditNotes || await _creditNotesService.CustomerHasCreditNotesAsync(customer.Id))
        {
            navigationModel.CustomerNavigationItems.Insert(position++, new()
            {
                RouteName = CWCoreDefaults.CustomerCreditsForMeRouteName,
                ItemClass = CWCoreDefaults.CustomerCreditsForMeMenuClassName,
                Tab = CWCoreDefaults.CustomerCreditsForMeMenuTab,
                Title = await _localizationService.GetResourceAsync("Plugins.CW.Core.CustomerCreditsForMe")
            });
        }

        if (!features.Subsidy)
            return;

        //Ensure that there is a packname selected
        var packName = await _packNameContext.GetCurrentPackNameAsync(customer);

        if (packName == null || packName.Id == 0)
            return;

        //the menu item shows when the page has a subsidy to list: an active subsidy, actively assigned to the packname
        var assignments = await _subsidyReportingService.GetSubsidyDetailsByPackNameIdAsync(packName.Id, inOrderingPeriodOnly: false);

        if (!assignments.Any(assignment => assignment.Active && assignment.AssignmentActive))
            return;

        navigationModel.CustomerNavigationItems.Insert(position++, new()
        {
            RouteName = CWCoreDefaults.CustomerSubsidiesRouteName,
            ItemClass = CWCoreDefaults.CustomerSubsidiesMenuClassName,
            Tab = CWCoreDefaults.CustomerSubsidiesMenuTab,
            Title = await _localizationService.GetResourceAsync("Plugins.CW.Core.CustomerSubsidies")
        });
    }


    /// <summary>
    /// Decides whether the bulk grid takes this product page, and if it does, empties the fields the stock
    /// partials read so they render nothing.
    /// </summary>
    /// <remarks>
    /// This is what makes the control removal work in ANY theme, and it is the whole reason nothing here
    /// edits a view.
    /// A theme may reorder the product page, restyle it, or replace the template outright — the two product
    /// templates in this repository already disagree about whether attributes come before or after the
    /// price — but no theme can change what ProductDetailsModel contains. Every partial on that page is
    /// gated on the model, so emptying the right fields removes the control wherever that theme happens to
    /// put it. Swapping partials by hand would only work if every store ran one theme, and a widget zone
    /// cannot stand in for it either, because a zone's position relative to the partials is a property of
    /// the theme.
    ///
    /// Each field below was read in the shipped partial rather than assumed, and the guard is quoted beside
    /// it. If a future nop changes one of those guards this method goes quietly wrong — a stock control
    /// reappearing beside the grid — so the acceptance run checks for their ABSENCE rather than only for
    /// the grid's presence (lessons #19, #34).
    /// </remarks>
    protected virtual async Task ResolveBulkOrderProductPageAsync(ProductDetailsModel model)
    {
        //nop renders this same page as an EDIT form for an existing cart line when AllowCartItemEditing is
        //on (ProductController :164), with the button becoming Update and the line id riding on the model.
        //Our grid adds; it has no concept of updating a line. Suppressing here would strand the customer on
        //an edit page with nothing to edit, so the grid stands aside for the flow it cannot serve.
        if (model.AddToCart.UpdatedShoppingCartItemId > 0)
            return;

        var grid = await _bulkOrderProductPageService.TryPrepareAsync(model.Id);

        if (grid == null)
            return;

        _bulkOrderProductPageContext.Grid = grid;

        //_ProductAttributes: @if (Model.ProductAttributes.Count > 0)
        model.ProductAttributes.Clear();

        //_AddToCart: @if (!Model.DisableBuyButton || Model.CustomerEntersPrice) - BOTH are needed, the
        //second is an OR. CustomerEntersPrice cannot actually be true here (UnsupportedReasonFor returns
        //CustomerEntersPrice and the gate has already refused), so this is belt and braces on a condition
        //that is not ours and could change.
        model.AddToCart.DisableBuyButton = true;
        model.AddToCart.CustomerEntersPrice = false;

        //_AddToWishlist: @if (!Model.DisableWishlistButton). HIDDEN, not replaced. A bulk wishlist partial
        //is not an option; we have no wishlist path, and the button cannot simply be left standing -
        //with ProductAttributes emptied its form posts no attribute values, so nop rejects it for any
        //product whose attributes are required. The choice was a hidden button or a broken one.
        model.AddToCart.DisableWishlistButton = true;

        //_Availability: @if (!string.IsNullOrWhiteSpace(Model.StockAvailability) || Model.DisplayBackInStock-
        //Subscription). One product-level stock sentence above a grid that states stock PER CELL is a second
        //answer to a question the grid answers better, and on a bundle the two genuinely disagree.
        //The back-in-stock subscription rides inside the same partial, which is why one flag covers both.
        model.StockAvailability = string.Empty;
        model.DisplayBackInStockSubscription = false;

        //_ProductTierPrices. The bulk grid prices tier-priced products itself and threads the quantities
        //through the factory, so the product is allowed onto the page and nop's tier table goes instead.
        //Keeping it would state the same price breaks twice in two different shapes.
        model.TierPrices.Clear();

        //_ProductEstimateShipping is deliberately NOT touched: it answers "when would this reach me", which
        //is about delivery rather than selection, and the grid says nothing about it.
    }

    /// <summary>
    /// Persists the per-product product-page override from nop's product edit form.
    /// </summary>
    /// <remarks>
    /// A widget's inputs are not model-bound — nop's ProductController binds its own ProductModel and knows
    /// nothing about ours — so the value comes back off the raw form by the same key the input was named
    /// with. That is the pattern this plugin already uses for the ACL department field, one line above.
    ///
    /// Guarded on the admin edit path for the same reason that one is: EntityUpdatedEvent&lt;Product&gt;
    /// fires on every product save from anywhere, including our own background work and nop's own
    /// stock adjustments, and none of those have a form to read. Without the guard the first such save would
    /// clear every override in the catalogue, one product at a time, with nothing to show what did it.
    ///
    /// Written at StoreId 0. The RUNTIME read is per store — store-specific, then store 0, then the setting
    /// — so a per-store override is expressible as data; this editor writes the store-agnostic one, which is
    /// the answer for a single control on a form that already carries "Limited to stores".
    /// An empty value means "follow the store": nop's SaveAttributeAsync deletes the row for an empty value, and an
    /// absent row reads the same way.
    /// </remarks>
    protected virtual async Task SaveBulkOrderProductPageOverrideAsync(Product product)
    {
        var request = _httpContextAccessor.HttpContext?.Request;

        if (request == null || !request.Path.StartsWithSegments("/Admin/Product/Edit"))
            return;

        if (!request.HasFormContentType ||
            !request.Form.TryGetValue(CWCoreDefaults.BULKORDER_SHOW_ON_PRODUCT_PAGE_PROPERTY, out var posted))
            return;

        var value = posted.ToString();

        //only the three states the editor can post; anything else is not ours to record
        if (value != "1" && value != "0")
            value = string.Empty;

        await _genericAttributeService.SaveAttributeAsync(product,
            CWCoreDefaults.BULKORDER_SHOW_ON_PRODUCT_PAGE_PROPERTY, value, storeId: 0);
    }

    #endregion

    #region Methods

    /// <summary>
    /// Handle page rendering event
    /// </summary>
    /// <param name="eventMessage">Event message</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    public async Task HandleEventAsync(PageRenderingEvent eventMessage)
    {
        eventMessage.Helper.AddCssFileParts("~/Plugins/CW.Core/Content/Common/cw-dialog.css");
        eventMessage.Helper.AddCssFileParts("~/Plugins/CW.Core/Content/icons/cw-icons.css");
        eventMessage.Helper.AddCssFileParts("~/Plugins/CW.Core/Content/PackName/flyout-cart.css");

        //The grid's assets cannot be registered by the view component that renders it: a component
        //invoked from a widget zone runs while the BODY is rendering, and this event - raised from
        //_Root.Head.cshtml, which is NopHtml's chance to emit the head - has already passed by then.
        //The list page gets away with AppendCssFileParts inside BulkOrdering.cshtml because a view body
        //executes before its layout; a component does not.
        //The context is resolved before this runs: ModelPrepared is published from a result filter, ahead
        //of every view. Registered only when a grid will actually draw, so an ordinary product page loads
        //neither file.
        if (_bulkOrderProductPageContext.Grid != null)
        {
            eventMessage.Helper.AddCssFileParts("~/Plugins/CW.Core/Content/BulkOrder/bulk-order.css");
            eventMessage.Helper.AddScriptParts(ResourceLocation.Footer, "~/Plugins/CW.Core/Content/BulkOrder/bulk-order.js");
        }

        //The cart-changed signal. Everything that listens to it (the selector, the info strip, the subsidy
        //catalogue) exists only while packnames are on.
        if (await _cwFeatureService.IsEnabledAsync(CWFeature.PackName))
            eventMessage.Helper.AddScriptParts(ResourceLocation.Footer, "~/Plugins/CW.Core/Content/Common/cw-cart-events.js");

        if (await _cwFeatureService.IsEnabledAsync(CWFeature.Subsidy))
        {
            eventMessage.Helper.AddCssFileParts("~/Plugins/CW.Core/Content/icons/pg-icons.css");

            //covers both /admin/customer/edit/{id} and /admin/extendedcustomer/edit/{id}
            var isCustomerCreateOrEdit = IsAdminPage("ExtendedCustomer", "Create", "Edit")
                                      || IsAdminPage("Customer", "Create", "Edit"); //fallback if the override route isn't registered
            var isMultiStoreEdit = IsAdminPage("MultiStore", "Edit");

            if (isCustomerCreateOrEdit || isMultiStoreEdit)
                eventMessage.Helper.AddCssFileParts("~/Plugins/CW.Core/Content/Admin/Subsidy/packname-subsidies.css");

            if (isMultiStoreEdit)
                eventMessage.Helper.AddCssFileParts("~/Plugins/CW.Core/Content/Admin/Subsidy/product-groups.css");
        }
    }

    /// <summary>
    /// Handle settings changed event
    /// </summary>
    /// <param name="eventMessage">Event message</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    public async Task HandleEventAsync(MultiStoreSettingsChangedEvent eventMessage)
    {
        await _staticCacheManager.ClearAsync();
    }

    public async Task HandleEventAsync(EntityUpdatedEvent<Category> eventMessage)
    {
        await HandleEntityEventAsync(_httpContextAccessor.HttpContext?.Request, "/Admin/Category/Edit", eventMessage.Entity, _aclService.SaveAclDepartmentAsync);
    }

    public async Task HandleEventAsync(EntityInsertedEvent<Category> eventMessage)
    {
        await HandleEntityEventAsync(_httpContextAccessor.HttpContext?.Request, "/Admin/Category/Create", eventMessage.Entity, _aclService.SaveAclDepartmentAsync);
    }

    public async Task HandleEventAsync(EntityUpdatedEvent<Manufacturer> eventMessage)
    {
        await HandleEntityEventAsync(_httpContextAccessor.HttpContext?.Request, "/Admin/Manufacturer/Edit", eventMessage.Entity, _aclService.SaveAclDepartmentAsync);
    }

    public async Task HandleEventAsync(EntityInsertedEvent<Manufacturer> eventMessage)
    {
        await HandleEntityEventAsync(_httpContextAccessor.HttpContext?.Request, "/Admin/Manufacturer/Create", eventMessage.Entity, _aclService.SaveAclDepartmentAsync);
    }

    public async Task HandleEventAsync(EntityUpdatedEvent<Product> eventMessage)
    {
        //the form's values belong to the product it posted. nop's save can update other products in the same request:
        //turning a grouped product into a simple one updates each of its associated products
        if (IsAnotherProductsForm(_httpContextAccessor.HttpContext?.Request, eventMessage.Entity))
            return;

        await HandleEntityEventAsync(_httpContextAccessor.HttpContext?.Request, "/Admin/Product/Edit", eventMessage.Entity, _aclService.SaveAclDepartmentAsync);
        await SaveBulkOrderProductPageOverrideAsync(eventMessage.Entity);
    }

    public async Task HandleEventAsync(EntityInsertedEvent<Product> eventMessage)
    {
        await HandleEntityEventAsync(_httpContextAccessor.HttpContext?.Request, "/Admin/Product/Create", eventMessage.Entity, _aclService.SaveAclDepartmentAsync);

        //not on create: the panel does not render for an unsaved product, so there is no field to read and
        //an unconditional save would write an empty override for every new product in the catalogue
    }

    /// <summary>
    /// Handle customer logged-in event
    /// </summary>
    /// <param name="eventMessage">Event message</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    public async Task HandleEventAsync(CustomerLoggedinEvent eventMessage)
    {
        if (eventMessage.Customer is null)
            return;

        //do not seed packname context for a store that has PackNames disabled
        if (!await _cwFeatureService.IsEnabledAsync(CWFeature.PackName))
            return;

        await SetPackNameContext(eventMessage.Customer);
    }

    /// <summary>
    /// Handle entity deleted event
    /// </summary>
    /// <param name="eventMessage">Event message</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    public async Task HandleEventAsync(EntityUpdatedEvent<ShoppingCartItem> eventMessage)
    {
        if (eventMessage.Entity is null)
            return;

        var query = from item in _packNameCartItemRepository.Table
                    where item.ShoppingCartItemId == eventMessage.Entity.Id
                    select item;

        var packNameCartItem = await query.FirstOrDefaultAsync();

        if (packNameCartItem != null)
        {
            //event notification for cache management
            await _eventPublisher.EntityUpdatedAsync(packNameCartItem);
        }
    }

    public Task HandleEventAsync(AdditionalTokensAddedEvent eventMessage)
    {
        if (eventMessage.TokenGroups == null || eventMessage.TokenGroups.Contains(CWTokenGroupNames.ExchangeReturnsTokens))
        {
            eventMessage.AdditionalTokens.Add("%ReturnRequest.PendingAmount%");
            eventMessage.AdditionalTokens.Add("%ReturnRequest.ReShippingFee%");
            eventMessage.AdditionalTokens.Add("%ReturnRequest.RePackagingFee%");
            eventMessage.AdditionalTokens.Add("%ReturnRequest.AdditionalFeeName%");
            eventMessage.AdditionalTokens.Add("%ReturnRequest.AdditionalFeeAmount%");
        }

        //The welcome e-mail of a customer registered by a manager: the packname and the subsidies assigned to it
        if (eventMessage.TokenGroups == null || eventMessage.TokenGroups.Contains(CWTokenGroupNames.SubsidyTokens))
        {
            eventMessage.AdditionalTokens.Add("%PackName.Name%");
            eventMessage.AdditionalTokens.Add("%PackName.EmployeeNumber%");
            eventMessage.AdditionalTokens.Add("%PackName.Branch%");
            eventMessage.AdditionalTokens.Add("%Subsidy.SubsidyName%");
        }

        //Shipment-scoped delivery address, so a shipment email can name the branch that parcel went to
        //rather than the order's placeholder. The three shipment templates already carry ShipmentTokens,
        //so these appear in the admin token list without touching GetTokenGroups.
        if (eventMessage.TokenGroups == null || eventMessage.TokenGroups.Contains(TokenGroupNames.ShipmentTokens))
        {
            eventMessage.AdditionalTokens.Add("%Shipment.ShippingMethod%");
            eventMessage.AdditionalTokens.Add("%Shipment.ShippingFirstName%");
            eventMessage.AdditionalTokens.Add("%Shipment.ShippingLastName%");
            eventMessage.AdditionalTokens.Add("%Shipment.ShippingCompany%");
            eventMessage.AdditionalTokens.Add("%Shipment.ShippingAddress1%");
            eventMessage.AdditionalTokens.Add("%Shipment.ShippingAddress2%");
            eventMessage.AdditionalTokens.Add("%Shipment.ShippingCity%");
            eventMessage.AdditionalTokens.Add("%Shipment.ShippingZipPostalCode%");
            eventMessage.AdditionalTokens.Add("%Shipment.ShippingStateProvince%");
            eventMessage.AdditionalTokens.Add("%Shipment.ShippingCountry%");
            eventMessage.AdditionalTokens.Add("%Shipment.ShippingPhoneNumber%");
            eventMessage.AdditionalTokens.Add("%Shipment.ShippingEmail%");
            eventMessage.AdditionalTokens.Add("%Shipment.ShippingFaxNumber%");
            eventMessage.AdditionalTokens.Add("%Shipment.ShippingCounty%");
            eventMessage.AdditionalTokens.Add("%Shipment.ShippingCustomAttributes%");
            eventMessage.AdditionalTokens.Add("%Shipment.ShippingAddressLine%");
        }

        //The order's whole delivery-address section. Every order-level template already carries
        //OrderTokens, so this appears in the admin token list without touching GetTokenGroups.
        if (eventMessage.TokenGroups == null || eventMessage.TokenGroups.Contains(TokenGroupNames.OrderTokens))
        {
            eventMessage.AdditionalTokens.Add("%Order.ShippingDestinations%");
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Handle entity deleted event
    /// </summary>
    /// <param name="eventMessage">Event message</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    public async Task HandleEventAsync(EntityDeletedEvent<PackName> eventMessage)
    {
        if (eventMessage.Entity is null)
            return;

        var subsidyPackNames = await _subsidyService.GetAllSubsidyPackNamesByPackNameIdAsync(eventMessage.Entity.Id);

        foreach (var subsidyPackName in subsidyPackNames)
        {
            await _subsidyPackNameRepository.DeleteAsync(subsidyPackName);
        }

        //A deleted packname's lines can no longer be shown or placed, so they leave the cart now. The
        //customer's selection needs nothing here: the packname context drops a deleted packname the next
        //time it is read.
        await _shoppingCartService.DeletePackNameCartItemsAsync(eventMessage.Entity.Id);
    }

    /// <summary>
    /// Breaks the packname → customer link when the customer is deleted.
    /// </summary>
    /// <remarks>
    /// A consumer rather than a patch in the delete action, because <c>DeleteCustomerAsync</c> has
    /// several callers (the admin action, imports, GDPR erasure) and the event covers all of them — the
    /// same reasoning as the existing <c>EntityDeletedEvent&lt;PackName&gt;</c> handler. NOT a query
    /// filter: <c>!customer.Deleted</c> would have to be added to five queries and two SQL views, which
    /// is five definitions of one rule, and it would leave the stale id in the data for the next reader
    /// to trip over.
    /// <para>
    /// The packname, its orders and its <c>PackNameOrderItem</c> history are deliberately untouched:
    /// <c>PackNameOrderItem.PackNameCustomerId</c> is a snapshot of who ordered and must not be
    /// rewritten.
    /// </para>
    /// </remarks>
    public async Task HandleEventAsync(EntityDeletedEvent<Customer> eventMessage)
    {
        if (eventMessage.Entity == null)
            return;

        var packName = await _packNameService.GetPackNameByCustomerIdAsync(eventMessage.Entity.Id);

        if (packName == null)
            return;

        packName.CustomerId = null;
        packName.Manager = false;

        await _packNameService.UpdatePackNameAsync(packName);
    }

    /// <summary>
    /// Handle entity deleted event
    /// </summary>
    /// <param name="eventMessage">Event message</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    public async Task HandleEventAsync(EntityDeletedEvent<ProductGroup> eventMessage)
    {
        if (eventMessage.Entity is null)
            return;

        var subsidyProductGroups = await _subsidyService.GetSubsidyProductGroupsByProductGroupIdAsync(eventMessage.Entity.Id);

        foreach (var subsidyProductGroup in subsidyProductGroups)
        {
            await _subsidyService.DeleteSubsidyProductGroupAsync(subsidyProductGroup);
        }
    }

    /// <summary>
    /// Handle entity deleted event
    /// </summary>
    /// <param name="eventMessage">Event message</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    public async Task HandleEventAsync(EntityDeletedEvent<Subsidy> eventMessage)
    {
        if (eventMessage.Entity is null)
            return;

        var subsidyProductGroups = await _subsidyService.GetSubsidyProductGroupsBySubsidyIdAsync(eventMessage.Entity.Id);

        foreach (var subsidyProductGroup in subsidyProductGroups)
        {
            await _subsidyService.DeleteSubsidyProductGroupAsync(subsidyProductGroup);
        }

        var subsidyPackNames = await _subsidyService.GetAllSubsidyPackNamesBySubsidyIdAsync(eventMessage.Entity.Id);

        foreach (var subsidyPackName in subsidyPackNames)
        {
            await _subsidyService.DeleteSubsidyPackNameAsync(subsidyPackName);
        }
    }

    /// <summary>
    /// Handle model prepared event.
    /// </summary>
    /// <remarks>
    /// nop publishes EVERY model through this one event, typed as <see cref="BaseNopModel"/> —
    /// `EventPublisherExtensions.ModelPreparedAsync` says so in its own comment, and
    /// <c>[PublishModelEvents]</c> sits on <c>BaseController</c>, so this method runs on every request of
    /// every page in the store, admin included. Two things follow from that and neither is optional.
    ///
    /// First, this method is a DISPATCH and nothing else: one type test per model we care about, each
    /// handled by its own method. It read `if (model is not CustomerNavigationModel) return;` while there
    /// was exactly one, which is a shape that quietly refuses the second — the early return is the whole
    /// method's contract, so a second model cannot be added without rewriting it.
    ///
    /// Second, a model we do not care about must cost a type test and nothing more. No service call, no
    /// context read, before we know which model this is. The handlers below start work only once their
    /// case has matched.
    /// </remarks>
    /// <param name="eventMessage">Event message</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    public async Task HandleEventAsync(ModelPreparedEvent<BaseNopModel> eventMessage)
    {
        switch (eventMessage.Model)
        {
            case CustomerNavigationModel navigationModel:
                await AddCustomerNavigationItemsAsync(navigationModel);
                break;

            //Resolved HERE, and only here, because this is the one moment in the request that has both
            //the product's identity and a position before any view has rendered. The head needs the answer
            //to decide on the stylesheet and the widget zone needs it to draw; neither can work it out for
            //itself, and ContentAfter - which renders from _Root.cshtml - is handed no product at all.
            case ProductDetailsModel productDetailsModel:
                await ResolveBulkOrderProductPageAsync(productDetailsModel);
                break;
        }
    }

    /// <summary>
    /// A shipment belongs to one destination (AddShipmentDestinationFilter enforces it), so shipping it
    /// advances that destination's packnames — not the whole order. Without this the per-packname status
    /// stays on "Not yet shipped" forever, which is what it did until now.
    /// </summary>
    public async Task HandleEventAsync(ShipmentSentEvent eventMessage)
    {
        await AdvancePackNameShippingAsync(eventMessage.Shipment, delivered: false);
    }

    public async Task HandleEventAsync(ShipmentDeliveredEvent eventMessage)
    {
        await AdvancePackNameShippingAsync(eventMessage.Shipment, delivered: true);
    }

    public async Task HandleEventAsync(ShoppingCartItemMovedToOrderItemEvent eventMessage)
    {
        var cartItem = eventMessage.ShoppingCartItem;
        var orderItem = eventMessage.OrderItem;

        if (cartItem == null || orderItem == null)
            return;

        var returnRequestItem = await _exchangeService.GetReturnRequestItemForCartItemAsync(cartItem.Id);

        if (returnRequestItem == null)
            return;

        //Set together so the row is written once. SetExchangeRequestCompletedAsync persists it and also
        //writes the order note recording which return this order settled.
        returnRequestItem.OrderItemId = orderItem.Id;

        await _exchangeService.SetExchangeRequestCompletedAsync(returnRequestItem);

        //Completing the line is half of it. The case's resolution was stamped Pending with the fees
        //outstanding while this replacement sat in the cart, and paying for it is what settles that —
        //but only once every other accepted line of the same case has been paid for too.
        await _returnResolutionLogic.TrySettleResolutionAsync(returnRequestItem.ReturnRequestId);
    }

    #endregion
}
