using Microsoft.AspNetCore.Mvc;
using Nop.Core;
using Nop.Plugin.CW.Core.Services.BulkOrder;
using Nop.Web.Framework.Components;

namespace Nop.Plugin.CW.Core.Components;

/// <summary>
/// The storefront link to the bulk ordering screen, in the header links list.
/// </summary>
/// <remarks>
/// <c>HeaderLinksBefore</c> rather than <c>HeaderLinksAfter</c> because the latter already carries
/// <see cref="PackNameSelectorViewComponent"/> — nop's <c>IWidgetPlugin</c> maps one component per zone per
/// plugin, so the two cannot share.
///
/// One zone, not the three the product-page grid uses. There the safety net exists because an emptied model
/// plus a missing zone is a product page nobody can buy from; here the worst case is a missing link and the
/// URL still works, so the same machinery would be far more than the consequence warrants.
/// If a theme ever drops this zone, the fallback that needs no new mechanism is the My Account navigation,
/// which is model-driven.
///
/// This runs on every page of the store, so the gate is the first thing it does and is two cached reads.
/// </remarks>
[ViewComponent(Name = "BulkOrderLink")]
public class BulkOrderLinkViewComponent : NopViewComponent
{
    #region Fields

    protected readonly IBulkOrderEntryService _bulkOrderEntryService;
    protected readonly IWorkContext _workContext;

    #endregion

    #region Ctor

    public BulkOrderLinkViewComponent(IBulkOrderEntryService bulkOrderEntryService,
        IWorkContext workContext)
    {
        _bulkOrderEntryService = bulkOrderEntryService;
        _workContext = workContext;
    }

    #endregion

    #region Methods

    public async Task<IViewComponentResult> InvokeAsync(string widgetZone, object additionalData)
    {
        var customer = await _workContext.GetCurrentCustomerAsync();

        //CanUseBulkOrderingAsync, not ResolveLandingRouteAsync: the link must appear whether or not the
        //store also sends people there after login. Those are different questions and the service keeps
        //them apart so the link and the redirect cannot disagree about who qualifies.
        if (!await _bulkOrderEntryService.CanUseBulkOrderingAsync(customer))
            return Content(string.Empty);

        return await ViewAsync();
    }

    #endregion
}