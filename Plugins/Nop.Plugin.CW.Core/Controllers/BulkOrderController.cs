using Microsoft.AspNetCore.Mvc;
using Nop.Core.Domain.Orders;
using Nop.Plugin.CW.Core.BusinessLogic;
using Nop.Plugin.CW.Core.BusinessLogic.Requests;
using Nop.Plugin.CW.Core.Domain.Settings;
using Nop.Plugin.CW.Core.Factories;
using Nop.Plugin.CW.Core.Filters;
using Nop.Plugin.CW.Core.Models.BulkOrder;
using Nop.Plugin.CW.Core.Services.Security;
using Nop.Services.Localization;
using Nop.Web.Components;
using Nop.Web.Controllers;
using Nop.Web.Framework.Mvc.Filters;

namespace Nop.Plugin.CW.Core.Controllers;

[CheckCWFeature(CWFeature.BulkOrder, StoreSource = CWFeatureStoreSource.CurrentStore)]
[AutoValidateAntiforgeryToken]
public class BulkOrderController : BasePublicController
{
    #region Fields

    protected readonly IBulkOrderLogic _bulkOrderLogic;
    protected readonly IBulkOrderModelFactory _bulkOrderModelFactory;
    protected readonly ILocalizationService _localizationService;
    protected readonly ShoppingCartSettings _shoppingCartSettings;

    #endregion

    #region Ctor

    public BulkOrderController(IBulkOrderLogic bulkOrderLogic,
        IBulkOrderModelFactory bulkOrderModelFactory,
        ILocalizationService localizationService,
        ShoppingCartSettings shoppingCartSettings)
    {
        _bulkOrderLogic = bulkOrderLogic;
        _bulkOrderModelFactory = bulkOrderModelFactory;
        _localizationService = localizationService;
        _shoppingCartSettings = shoppingCartSettings;
    }

    #endregion

    #region Methods

    [CheckPermission(CWPermissionConfigManager.PUBLIC_ACCESS_BULKORDERING)]
    public virtual async Task<IActionResult> Index(BulkOrderCommand command)
    {
        var model = await _bulkOrderModelFactory.PrepareShellModelAsync(command);

        return View("~/Plugins/CW.Core/Views/BulkOrder/BulkOrdering.cshtml", model);
    }

    [CheckPermission(CWPermissionConfigManager.PUBLIC_ACCESS_BULKORDERING)]
    public virtual async Task<IActionResult> Items(BulkOrderCommand command)
    {
        var model = await _bulkOrderModelFactory.PrepareItemsModelAsync(command);

        return PartialView("~/Plugins/CW.Core/Views/BulkOrder/_Content.cshtml", model);
    }

    [CheckPermission(CWPermissionConfigManager.PUBLIC_ACCESS_BULKORDERING)]
    public virtual async Task<IActionResult> Grid(int productId, string pins)
    {
        var model = await _bulkOrderModelFactory.PrepareGridModelAsync(productId, pins);

        //not visible to this customer, or gone since the page rendered
        if (model == null)
            return Content(string.Empty);

        return PartialView("~/Plugins/CW.Core/Views/BulkOrder/_Grid.cshtml", model);
    }

    /// <summary>
    /// Adds one or many lines to the cart. Same endpoint for a single card's button and for
    /// the whole page, because the only difference is which lines the client puts in the request.
    /// </summary>
    /// <remarks>
    /// POST and antiforgery-protected: this changes the customer's cart. The GET actions above are safe to
    /// leave as GETs because they only render.
    /// The request body is JSON, so the model binder needs [FromBody]; without it MVC looks in the form
    /// collection and every line arrives empty, which reads as "nothing was submitted" rather than as an
    /// error.
    /// The header count and the flyout are rendered HERE rather than in the logic layer: rendering a view
    /// component is a controller's job, and BusinessLogic/ has no business knowing the site has a header.
    /// </remarks>
    [HttpPost]
    [CheckPermission(CWPermissionConfigManager.PUBLIC_ACCESS_BULKORDERING)]
    public virtual async Task<IActionResult> AddToCart([FromBody] BulkOrderAddRequest request)
    {
        //a body that does not read as a request (a quantity past what a number holds), or holds an empty line, is
        //refused, so the page says the add failed instead of "0 added"
        if (request == null || request.Lines?.Any(line => line == null) == true)
            return BadRequest();

        var result = await _bulkOrderLogic.AddToCartAsync(request);

        //nop's own mechanism rather than an invented one: nop refreshes the cart blocks by
        //returning their rendered HTML from the add endpoint and letting public.ajaxcart.js write it in
        //(ShoppingCartController.AddProductToCart_Details :435-436, public.ajaxcart.js :136-144). There is no
        //client-side "cart changed" event in nop to raise. PackNameCheckoutController :960-965 already does
        //this. The two field names are lowercase because they are nop's names, spelled nop's way.
        var updatetopcartsectionhtml = string.Format(
            await _localizationService.GetResourceAsync("ShoppingCart.HeaderQuantity"),
            result.CartTotalQuantity);

        var updateflyoutcartsectionhtml = _shoppingCartSettings.MiniShoppingCartEnabled
            ? await RenderViewComponentToStringAsync(typeof(FlyoutShoppingCartViewComponent))
            : string.Empty;

        //The result's own members keep their declared casing: nop pins Newtonsoft to DefaultContractResolver
        //(ServiceCollectionExtensions :327), so nothing here is camel-cased on the way out and the script
        //reads PascalCase for these three.
        return Json(new
        {
            result.AddedQuantity,
            result.Results,
            result.CartQuantities,
            updatetopcartsectionhtml,
            updateflyoutcartsectionhtml
        });
    }

    #endregion
}