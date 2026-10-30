using Microsoft.AspNetCore.Mvc;
using Nop.Core;
using Nop.Plugin.CW.Core.Factories;
using Nop.Plugin.CW.Core.Services.Orders;
using Nop.Web.Framework.Components;

namespace Nop.Plugin.CW.Core.Components;

[ViewComponent(Name = "PackNameOrderTotals")]
public partial class PackNameOrderTotalsViewComponent : NopViewComponent
{
    #region Fields

    private readonly IShoppingCartOverrideModelFactory _shoppingCartModelFactory;
    private readonly IShoppingCartOverrideService _shoppingCartService;
    private readonly IStoreContext _storeContext;
    private readonly IWorkContext _workContext;

    #endregion

    #region Ctor

    public PackNameOrderTotalsViewComponent(IShoppingCartOverrideModelFactory shoppingCartModelFactory,
        IShoppingCartOverrideService shoppingCartService,
        IStoreContext storeContext,
        IWorkContext workContext)
    {
        _shoppingCartModelFactory = shoppingCartModelFactory;
        _shoppingCartService = shoppingCartService;
        _storeContext = storeContext;
        _workContext = workContext;
    }

    #endregion

    #region Methods

    public async Task<IViewComponentResult> InvokeAsync(bool isEditable)
    {
        var store = await _storeContext.GetCurrentStoreAsync();
        var cart = await _shoppingCartService.GetPackNameShoppingCartItemsAsync(await _workContext.GetCurrentCustomerAsync(), 0, store.Id);

        var model = await _shoppingCartModelFactory.PreparePackNameOrderTotalsModelAsync(cart, isEditable);

        return View(model);
    }

    #endregion
}
