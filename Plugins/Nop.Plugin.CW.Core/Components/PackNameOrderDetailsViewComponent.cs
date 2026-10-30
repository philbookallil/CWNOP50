using Microsoft.AspNetCore.Mvc;
using Nop.Plugin.CW.Core.Models.Orders;
using Nop.Web.Framework.Components;

namespace Nop.Plugin.CW.Core.Components;

[ViewComponent(Name = "PackNameOrderDetails")]
public class PackNameOrderDetailsViewComponent : NopViewComponent
{
    public IViewComponentResult Invoke(PackNameOrderDetailsModel model) => View(model);
}