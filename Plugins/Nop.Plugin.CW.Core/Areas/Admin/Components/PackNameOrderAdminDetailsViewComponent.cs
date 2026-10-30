using Microsoft.AspNetCore.Mvc;
using Nop.Plugin.CW.Core.Areas.Admin.Factories;
using Nop.Web.Areas.Admin.Models.Orders;
using Nop.Web.Framework.Components;

namespace Nop.Plugin.CW.Core.Areas.Admin.Components;

[ViewComponent(Name = "PackNameOrderAdminDetails")]
public class PackNameOrderAdminDetailsViewComponent : NopViewComponent
{
    private readonly IPackNameOrderModelFactory _factory;

    public PackNameOrderAdminDetailsViewComponent(IPackNameOrderModelFactory factory)
        => _factory = factory;

    public async Task<IViewComponentResult> InvokeAsync(string widgetZone, object additionalData)
    {
        if (additionalData is not OrderModel orderModel)
            return Content(string.Empty);

        var model = await _factory.PreparePackNameOrderDetailsModelAsync(orderModel.Id);

        if (!model.HasPackNameItems)
            return Content(string.Empty);

        return View(model);
    }
}
