using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Nop.Plugin.CW.Core.Services.Orders;
using Nop.Services.Localization;
using Nop.Services.Messages;
using Nop.Web.Areas.Admin.Controllers;
using Nop.Web.Areas.Admin.Models.Orders;

namespace Nop.Plugin.CW.Core.Filters;

/// <summary>
/// Blocks nop's admin order-line and order-address actions on a packname order.
/// </summary>
/// <remarks>
/// <para>
/// A packname order's items carry <c>PackNameOrderItem</c> rows, its addresses ARE its per-packname
/// shipping destinations, and its totals are the sum of per-packname settlement. nop's admin edit
/// actions know none of that: <c>DeleteOrderItem</c> hard-deletes the <c>OrderItem</c> and cascades the
/// <c>PackNameOrderItem</c> and <c>SubsidyOrderItem</c> rows away, so consumed subsidy silently returns
/// to available on an order that was placed and paid; <c>AddProductToOrderDetails</c> adds a line that
/// belongs to no destination and appears on no packname screen; <c>AddressEdit</c> moves destination #1
/// and every packname sharing it. So they are blocked outright. Rebuilding these screens for
/// packname orders is deferred.
/// </para>
/// <para>
/// <b>Matched on the METHOD name, not the action name.</b> Every one of these is
/// <c>[HttpPost, ActionName("Edit")]</c> with a <c>FormValueRequired</c> constraint, so the action name
/// is <c>"Edit"</c> for all of them and for the GET screen too - <c>CoreActionFilter.IsActionName</c>
/// cannot tell them apart and is not used here.
/// </para>
/// <para>
/// <b>Every identifier the action acts on is tested, not just the posted order id.</b> nop's
/// <c>EditOrderItem</c> and <c>DeleteOrderItem</c> load the
/// order from the route, then take the order ITEM from a form key and fetch it by id with no
/// <c>orderItem.OrderId == order.Id</c> check; <c>AddressEdit</c> POST does the same with
/// <c>model.Address.Id</c> against <c>model.OrderId</c>. Guarding the posted order id alone was
/// therefore bypassable: post a non-packname order's id with a packname order's item id in the button
/// key and nop deletes the line. The order item and the address are checked in their own right.
/// </para>
/// <para>
/// <b>No feature-flag check.</b> The first version asked
/// <c>ICWFeatureService.IsEnabledAsync(CWFeature.PackName)</c>, which resolves the CURRENT store - in the
/// admin area, whatever store the admin's host maps to, not the store the order belongs to. Admins edit
/// every store's orders from one host, so an order in another store was unguarded whenever the flag was
/// off here. It is not repaired but removed: the <c>PackNameOrderItem</c> rows are a fact about
/// the order, the flag is a setting, and an order carrying those rows needs protecting whether or not
/// someone has since switched the feature off. <c>IsPackNameOrderAsync</c> is served from the per-order
/// cache, so this is one cheap question per guarded POST.
/// </para>
/// <para>
/// <b>Fails CLOSED.</b> A blocked method whose subject cannot be identified at all is blocked. That is
/// the branch a change to nop's binding would land in, and letting it through is how a guard quietly
/// stops guarding.
/// </para>
/// <para>
/// <b>GET redirects, POST gets 403</b>, for the same reason as the storefront checkout filter: a
/// redirect returned to a grid or AJAX caller renders as markup and reads as nothing having happened.
/// </para>
/// <para>
/// Deliberately NOT blocked: <c>CancelOrder</c> and <c>Delete</c> (cancelling IS the designed subsidy
/// restore path - the consumption view filters <c>OrderStatusId != 40 AND Deleted = 0</c>), the refund
/// and void actions (money only, no packname coupling), and
/// <c>AddShipment</c>, which <see cref="AddShipmentDestinationFilter"/> already governs.
/// </para>
/// </remarks>
public class PackNameOrderAdminGuardFilter : CoreActionFilter, IAsyncActionFilter
{
    #region Fields

    /// <summary>
    /// nop OrderController METHOD names that mutate a placed order's items, addresses or totals.
    /// </summary>
    private static readonly string[] BlockedMethods =
    [
        "EditOrderItem",
        "DeleteOrderItem",
        "AddProductToOrder",
        "AddProductToOrderDetails",
        "AddressEdit",
        "EditOrderTotals",
        "EditShippingMethod"
    ];

    /// <summary>
    /// The form-key prefixes nop parses the order item id out of, in EditOrderItem and DeleteOrderItem.
    /// </summary>
    private static readonly string[] OrderItemButtonPrefixes =
    [
        "btnSaveOrderItem",
        "btnDeleteOrderItem"
    ];

    protected readonly ICWOrderService _cwOrderService;
    protected readonly ILocalizationService _localizationService;
    protected readonly INotificationService _notificationService;

    #endregion

    #region Ctor

    public PackNameOrderAdminGuardFilter(ICWOrderService cwOrderService,
        ILocalizationService localizationService,
        INotificationService notificationService)
    {
        _cwOrderService = cwOrderService;
        _localizationService = localizationService;
        _notificationService = notificationService;
    }

    #endregion

    #region Utilities

    private static bool IsBlockedMethod(object controller, ControllerActionDescriptor actionDescriptor)
    {
        return actionDescriptor != null
            && controller is OrderController
            && BlockedMethods.Contains(actionDescriptor.MethodInfo.Name);
    }

    /// <summary>
    /// The order id, from whichever argument this particular action carries it in.
    /// </summary>
    /// <remarks>
    /// Model binding has already run when an action filter is entered, so the bound arguments are the
    /// reliable source - re-parsing the form would miss the route-bound cases.
    /// </remarks>
    private static int ResolveOrderId(ActionExecutingContext context)
    {
        if (context.ActionArguments.TryGetValue("model", out var model)
            && model is OrderAddressModel addressModel)
            return addressModel.OrderId;

        if (context.ActionArguments.TryGetValue("searchModel", out var searchModel)
            && searchModel is AddProductToOrderSearchModel productSearchModel)
            return productSearchModel.OrderId;

        if (context.ActionArguments.TryGetValue("orderId", out var orderId) && orderId is int orderIdValue)
            return orderIdValue;

        //EditOrderItem, DeleteOrderItem, EditOrderTotals and EditShippingMethod all bind the order id
        //as the route's "id".
        if (context.ActionArguments.TryGetValue("id", out var id) && id is int idValue)
            return idValue;

        return 0;
    }

    /// <summary>
    /// EVERY order item id the form could make nop act on.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Plural on purpose. nop picks one id out of the form with a loop that has no <c>break</c>
    /// in <c>OrderController</c>, so the LAST matching key is the one it acts on. Returning
    /// only the first let a post carrying two button keys slip past the guard. Every candidate is
    /// returned instead, so the guard does not depend on which one nop happens to choose.
    /// </para>
    /// <para>
    /// <c>HasFormContentType</c> first: reading <c>Request.Form</c> on a GET throws. <c>TryParse</c>
    /// rather than nop's <c>Convert.ToInt32</c>: a non-numeric suffix makes nop throw before it mutates
    /// anything, so skipping it here costs no coverage.
    /// </para>
    /// </remarks>
    private static IEnumerable<int> ResolveOrderItemIdsFromForm(HttpContext httpContext)
    {
        if (!httpContext.Request.HasFormContentType)
            yield break;

        foreach (var key in httpContext.Request.Form.Keys)
            foreach (var prefix in OrderItemButtonPrefixes)
                if (key.StartsWith(prefix, StringComparison.InvariantCultureIgnoreCase)
                    && int.TryParse(key[prefix.Length..], out var orderItemId)
                    && orderItemId > 0)
                    yield return orderItemId;
    }

    /// <summary>
    /// The ADDRESS id AddressEdit acts on - which nop never ties to the posted order.
    /// </summary>
    /// <remarks>
    /// Both shapes, because the two overloads carry it differently: the POST binds it inside
    /// <c>OrderAddressModel</c>, the GET takes it as a plain
    /// <c>addressId</c> argument and carries no model at all. Reading only the model
    /// left the GET guarded by the posted order id alone, so the edit screen for a packname order's
    /// address opened normally through a non-packname order's id and only its POST was refused - the
    /// admin filled in a form that could never save.
    /// </remarks>
    private static int ResolveAddressId(ActionExecutingContext context)
    {
        if (context.ActionArguments.TryGetValue("model", out var model)
            && model is OrderAddressModel addressModel
            && addressModel.Address?.Id > 0)
            return addressModel.Address.Id;

        if (context.ActionArguments.TryGetValue("addressId", out var addressId) && addressId is int addressIdValue)
            return addressIdValue;

        return 0;
    }

    /// <summary>
    /// Whether this request touches a packname order by ANY of the identifiers the action acts on.
    /// Returns true - block - when none of them could be resolved.
    /// </summary>
    private async Task<bool> TouchesPackNameOrderAsync(ActionExecutingContext context, string methodName)
    {
        var resolvedSomething = false;

        var orderId = ResolveOrderId(context);

        if (orderId > 0)
        {
            resolvedSomething = true;

            if (await _cwOrderService.IsPackNameOrderAsync(orderId))
                return true;
        }

        if (methodName is "EditOrderItem" or "DeleteOrderItem")
        {
            //EVERY candidate, not just one. nop's parse loop has no break, so the LAST matching form
            //key wins; checking only the first let a post carrying two
            //button keys - an innocent item's, then a packname item's - pass this guard while nop acted
            //on the packname one. Testing all of them makes key order irrelevant, which is the only
            //version of this that cannot drift with nop's implementation.
            foreach (var orderItemId in ResolveOrderItemIdsFromForm(context.HttpContext))
            {
                resolvedSomething = true;

                if (await _cwOrderService.GetPackNameOrderItemAsync(orderItemId) != null)
                    return true;
            }
        }

        if (methodName == "AddressEdit")
        {
            var addressId = ResolveAddressId(context);

            if (addressId > 0)
            {
                resolvedSomething = true;

                if (await _cwOrderService.IsPackNameOrderAddressAsync(addressId))
                    return true;
            }
        }

        return !resolvedSomething;
    }

    #endregion

    #region Methods

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (context.ActionDescriptor is not ControllerActionDescriptor descriptor
            || !IsBlockedMethod(context.Controller, descriptor))
        {
            await next();
            return;
        }

        if (!await TouchesPackNameOrderAsync(context, descriptor.MethodInfo.Name))
        {
            await next();
            return;
        }

        if (IsGet(context.HttpContext))
        {
            _notificationService.ErrorNotification(
                await _localizationService.GetResourceAsync("Plugins.CW.Core.Admin.Orders.PackNameOrderNotEditable"));

            var orderId = ResolveOrderId(context);

            context.Result = orderId > 0
                ? CreateRedirect("Edit", "Order",
                    new Microsoft.AspNetCore.Routing.RouteValueDictionary { ["id"] = orderId },
                    Nop.Web.Framework.AreaNames.ADMIN)
                : CreateRedirect("List", "Order", null, Nop.Web.Framework.AreaNames.ADMIN);

            return;
        }

        context.Result = new StatusCodeResult(StatusCodes.Status403Forbidden);
    }

    #endregion
}