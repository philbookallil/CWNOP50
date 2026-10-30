using Microsoft.AspNetCore.Mvc;
using Nop.Plugin.CW.Core.Areas.Admin.Models.Orders;
using Nop.Plugin.CW.Core.Services.CreditNotes;
using Nop.Plugin.CW.Core.Services.Security;
using Nop.Services.Catalog;
using Nop.Services.Security;
using Nop.Web.Framework.Components;

namespace Nop.Plugin.CW.Core.Areas.Admin.Components;

/// <summary>
/// Renders the CW content in the admin order-details block: the credit-notes-applied summary
/// (gated by ADMIN_ACCESS_CREDITNOTES) and the return-requests grid (gated by ADMIN_ACCESS_EXCHANGES).
/// One component owns the zone, so both pieces are composed here.
/// </summary>
[ViewComponent(Name = "CWOrderReturnRequests")]
public class OrderReturnRequestsViewComponent : NopViewComponent
{
    #region Fields

    protected readonly IPermissionService _permissionService;
    protected readonly ICreditNotesService _creditNotesService;
    protected readonly IPriceFormatter _priceFormatter;

    #endregion

    #region Ctor

    public OrderReturnRequestsViewComponent(IPermissionService permissionService,
        ICreditNotesService creditNotesService,
        IPriceFormatter priceFormatter)
    {
        _permissionService = permissionService;
        _creditNotesService = creditNotesService;
        _priceFormatter = priceFormatter;
    }

    #endregion

    #region Methods

    public async Task<IViewComponentResult> InvokeAsync(int orderId = 0)
    {
        if (orderId <= 0)
            return Content(string.Empty);

        var canViewCreditNotes = await _permissionService.AuthorizeAsync(CWPermissionConfigManager.ADMIN_ACCESS_CREDITNOTES);

        var model = new OrderReturnRequestSearchModel
        {
            OrderId = orderId,
            ShowReturns = await _permissionService.AuthorizeAsync(CWPermissionConfigManager.ADMIN_ACCESS_EXCHANGES)
        };

        // Nothing to show for this user - Edit.cshtml gates the card on the same two permissions,
        // so reaching this means the card was not rendered either
        if (!model.ShowReturns && !canViewCreditNotes)
            return Content(string.Empty);

        if (canViewCreditNotes)
        {
            var histories = await _creditNotesService.GetCreditNoteHistoriesByOrderIdAsync(orderId);
            var applied = histories.Sum(h => h.Amount);
            if (applied > decimal.Zero)
                model.CreditNotesApplied = await _priceFormatter.FormatPriceAsync(applied, true, false);
        }

        return View("~/Plugins/CW.Core/Areas/Admin/Views/Shared/Components/OrderReturnRequests/Default.cshtml", model);
    }

    #endregion
}