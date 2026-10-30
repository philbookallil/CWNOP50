using Microsoft.AspNetCore.Mvc;
using Nop.Plugin.CW.Core.Factories;
using Nop.Plugin.CW.Core.Models.BulkOrder;
using Nop.Web.Framework.Components;

namespace Nop.Plugin.CW.Core.Components;

[ViewComponent(Name = "BulkOrderCategoryNavigation")]
public class BulkOrderCategoryNavigationViewComponent : NopViewComponent
{
    protected readonly IBulkOrderModelFactory _bulkOrderModelFactory;

    public BulkOrderCategoryNavigationViewComponent(IBulkOrderModelFactory bulkOrderModelFactory)
    {
        _bulkOrderModelFactory = bulkOrderModelFactory;
    }

    public async Task<IViewComponentResult> InvokeAsync(BulkOrderingModel bulkOrderingModel)
    {
        var model = await _bulkOrderModelFactory.PrepareCategoryNavigationModelAsync(bulkOrderingModel);

        return await ViewAsync(model);
    }
}