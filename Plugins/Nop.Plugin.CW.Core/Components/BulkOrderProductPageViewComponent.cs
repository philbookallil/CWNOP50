using Microsoft.AspNetCore.Mvc;
using Nop.Plugin.CW.Core.Services.BulkOrder;
using Nop.Web.Framework.Components;

namespace Nop.Plugin.CW.Core.Components;

/// <summary>
/// Renders the bulk grid on a product details page. Registered on three widget zones and drawing in at
/// most one of them.
/// </summary>
/// <remarks>
/// Why three zones: if the model has been emptied and the theme does not render the zone we chose, the page
/// has no attributes, no add-to-cart and no grid — it is unbuyable. Zones 1 and 2 are both declared in
/// ProductTemplate.Simple.cshtml, which is the file a theme is most likely to rewrite, so losing both at
/// once takes one edit. ContentAfter is declared in Views/Shared/_Root.cshtml instead, so losing it as well
/// takes a second, separate decision to rewrite the layout root — which breaks widgets generally, not just
/// ours. The degradation is graded: right place, worse place, then bottom of the page.
///
/// The zone that draws is the first one the theme happens to render, and `Rendered` makes the rest no-ops.
///
/// This component is on ContentAfter, so it is invoked on EVERY page of the store. The first line has to be
/// cheap and it is: the context is null on any page where the ModelPrepared consumer did not resolve a
/// grid, which is every page that is not a qualifying product page. No service call, no route inspection.
/// </remarks>
[ViewComponent(Name = "BulkOrderProductPage")]
public class BulkOrderProductPageViewComponent : NopViewComponent
{
    #region Fields

    protected readonly BulkOrderProductPageContext _bulkOrderProductPageContext;

    #endregion

    #region Ctor

    public BulkOrderProductPageViewComponent(BulkOrderProductPageContext bulkOrderProductPageContext)
    {
        _bulkOrderProductPageContext = bulkOrderProductPageContext;
    }

    #endregion

    #region Methods

    public async Task<IViewComponentResult> InvokeAsync(string widgetZone, object additionalData)
    {
        if (_bulkOrderProductPageContext.Grid == null || _bulkOrderProductPageContext.Rendered)
            return Content(string.Empty);

        _bulkOrderProductPageContext.Rendered = true;

        return await ViewAsync(_bulkOrderProductPageContext.Grid);
    }

    #endregion
}