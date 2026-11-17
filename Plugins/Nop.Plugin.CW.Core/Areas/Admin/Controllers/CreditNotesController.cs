using Microsoft.AspNetCore.Mvc;
using Nop.Plugin.CW.Core.Areas.Admin.Factories;
using Nop.Plugin.CW.Core.Areas.Admin.Models.CreditNotes;
using Nop.Plugin.CW.Core.Domain.Settings;
using Nop.Plugin.CW.Core.Filters;
using Nop.Plugin.CW.Core.Services.CreditNotes;
using Nop.Plugin.CW.Core.Services.Security;
using Nop.Services.Customers;
using Nop.Services.Localization;
using Nop.Services.Security;
using Nop.Web.Areas.Admin.Controllers;
using Nop.Web.Framework.Mvc.Filters;

namespace Nop.Plugin.CW.Core.Areas.Admin.Controllers;

public class CreditNotesController : BaseAdminController
{
    #region Fields

    protected readonly ICreditNotesService _creditNotesService;
    protected readonly IPermissionService _permissionService;
    protected readonly ICreditNotesModelFactory _creditNotesModelFactory;
    protected readonly ICustomerService _customerService;
    protected readonly ICWFeatureGuard _cwFeatureGuard;
    protected readonly ILocalizationService _localizationService;

    #endregion

    #region Ctor

    public CreditNotesController(ICreditNotesService creditNotesService,
        IPermissionService permissionService,
        ICreditNotesModelFactory creditNotesModelFactory,
        ICustomerService customerService,
        ICWFeatureGuard cwFeatureGuard,
        ILocalizationService localizationService)
    {
        _creditNotesService = creditNotesService;
        _permissionService = permissionService;
        _creditNotesModelFactory = creditNotesModelFactory;
        _customerService = customerService;
        _cwFeatureGuard = cwFeatureGuard;
        _localizationService = localizationService;
    }

    #endregion

    #region Methods

    [HttpPost]
    [CheckPermission(CWPermissionConfigManager.ADMIN_ACCESS_CREDITNOTES)]
    public virtual async Task<IActionResult> CustomerCreditNotesList(CreditNoteSearchModel searchModel)
    {
        var model = await _creditNotesModelFactory.PrepareCreditNoteListModelAsync(searchModel);
        return Json(model);
    }

    [HttpPost]
    [CheckPermission(CWPermissionConfigManager.ADMIN_ACCESS_CREDITNOTES)]
    public virtual async Task<IActionResult> CreditNoteHistoryList(CreditNoteHistorySearchModel searchModel)
    {
        var model = await _creditNotesModelFactory.PrepareCreditNoteHistoryListModelAsync(searchModel);
        return Json(model);
    }

    [CheckPermission(CWPermissionConfigManager.ADMIN_ACCESS_CREDITNOTES)]
    public virtual async Task<IActionResult> GetCreditNotesSummary(int customerId)
    {
        var model = await _creditNotesModelFactory.PrepareCreditNotesCustomerSummaryModelAsync(customerId);
        return Json(new { amount = model.CreditNotesAvailableAmountFormated });

        /*var model = await _creditNotesModelFactory.PrepareCreditNotesCustomerSummaryModelAsync(id);

        return Json(new
        {
            model.CustomerId,
            CreditNotesAvailableAmount = FormatHelper.FormatCurrency(model.CreditNotesAvailableAmount)
        });*/
    }


    [CheckPermission(CWPermissionConfigManager.ADMIN_ACCESS_CREDITNOTES)]
    public virtual async Task<IActionResult> GetAvailableOrders(int customerId)
    {
        var model = await _creditNotesModelFactory.PrepareCreditNoteAddFormModelAsync(customerId);
        var orders = model.AvailableOrders.Select(o => new { id = o.Value, text = o.Text });
        return Json(orders);
    }

    [HttpPost]
    [CheckPermission(CWPermissionConfigManager.ADMIN_ACCESS_CREDITNOTES)]
    public virtual async Task<IActionResult> SaveCreditNote(CreditNoteAddFormModel model)
    {
        if (!await _permissionService.AuthorizeAsync(CreditNotesPermissionConfigManager.ALLOW_MANUAL_CREDITNOTES))
            return Json(new { success = false, message = "You are not allowed to manually create Credit Notes" });

        if (model.CustomerId <= 0 || model.OrderId <= 0)
            return Json(new { success = false, message = "Please select a valid order." });

        if (model.Amount <= decimal.Zero)
            return Json(new { success = false, message = "The amount must be greater than zero." });

        var customer = await _customerService.GetCustomerByIdAsync(model.CustomerId);
        if (customer == null)
            return Json(new { success = false, message = "Please select a valid customer." });

        if (await _cwFeatureGuard.BlockIfDisabledAsync(CWFeature.CreditNotes, customer.RegisteredInStoreId, Request) is IActionResult blocked)
            return blocked;

        try
        {
            await _creditNotesService.CreateManualCreditNoteAsync(model.CustomerId, model.OrderId, model.Amount, model.Description);
        }
        catch (Exception ex)
        {
            var message = string.Format("Error creating manual Credit Note: {0}", ex.Message);
            return Json(new { success = false, message });
        }

        return Json(new { success = true, message = "The credit note has been created." });
    }


    [HttpPost]
    [CheckPermission(CWPermissionConfigManager.ADMIN_ACCESS_CREDITNOTES)]
    public async Task<IActionResult> DeleteCreditNote(int creditNoteId)
    {
        var creditNote = await _creditNotesService.GetCreditNoteByIdAsync(creditNoteId);

        if (creditNote == null)
            return Json(new { success = false, message = $"Cannot delete Credit Note {creditNoteId}: Credit Note does not exist" });

        if (await _cwFeatureGuard.BlockIfDisabledAsync(CWFeature.CreditNotes, creditNote.StoreId, Request) is IActionResult blocked)
            return blocked;

        //A note that has paid for an order is part of that order's settlement: deleting it would take away the
        //record of the money that paid, so only a note never applied to an order can be deleted
        if (await _creditNotesService.CreditNoteHasHistoryAsync(creditNote.Id))
            return Json(new { success = false, message = await _localizationService.GetResourceAsync("Plugins.CW.Core.Admin.CreditNotes.Delete.Spent") });

        await _creditNotesService.DeleteCreditNoteAsync(creditNote);

        return Json(new { success = true, message = $"Credit Note {creditNoteId} deleted successfully!" });
    }

    #endregion

    #region Liability report

    [CheckPermission(CWPermissionConfigManager.ADMIN_ACCESS_CREDITNOTES)]
    public virtual async Task<IActionResult> MainReport()
    {
        var model = await _creditNotesModelFactory.PrepareCreditNotesReportModelAsync();
        return View(model);
    }

    [HttpPost]
    [CheckPermission(CWPermissionConfigManager.ADMIN_ACCESS_CREDITNOTES)]
    public virtual async Task<IActionResult> StoreLiabilityList(StoreLiabilitySearchModel searchModel)
    {
        var model = await _creditNotesModelFactory.PrepareStoreLiabilityListModelAsync(searchModel);
        return Json(model);
    }

    [HttpPost]
    [CheckPermission(CWPermissionConfigManager.ADMIN_ACCESS_CREDITNOTES)]
    public virtual async Task<IActionResult> StoreCustomerCreditNotesList(StoreLiabilitySearchModel searchModel)
    {
        var model = await _creditNotesModelFactory.PrepareStoreCustomerCreditNotesListModelAsync(searchModel);
        return Json(model);
    }

    #endregion
}
