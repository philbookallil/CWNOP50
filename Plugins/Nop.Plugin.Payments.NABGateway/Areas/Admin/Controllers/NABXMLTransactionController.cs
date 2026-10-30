using Microsoft.AspNetCore.Mvc;
using Nop.Plugin.Payments.NABGateway.Areas.Admin.Factories;
using Nop.Plugin.Payments.NABGateway.Areas.Admin.Models;
using Nop.Plugin.Payments.NABGateway.Services.Security;
using Nop.Web.Areas.Admin.Controllers;
using Nop.Web.Framework.Mvc.Filters;

namespace Nop.Plugin.Payments.NABGateway.Areas.Admin.Controllers;

/// <summary>
/// Admin controller backing the NAB XML "Payments" and "Failed Transactions" reports.
/// </summary>
public class NABXMLTransactionController : BaseAdminController
{
    #region Fields

    protected readonly INABXMLTransactionModelFactory _modelFactory;

    #endregion

    #region Ctor

    public NABXMLTransactionController(INABXMLTransactionModelFactory modelFactory)
    {
        _modelFactory = modelFactory;
    }

    #endregion

    #region Payments report

    [CheckPermission(NABXMLPaymentPermissionConfigManager.ACCESS_REPORTS)]
    public virtual IActionResult Payments()
    {
        return View("~/Plugins/Payments.NABGateway/Views/NABXMLTransaction/Payments.cshtml", new NABTransactionSearchModel());
    }

    [HttpPost]
    [CheckPermission(NABXMLPaymentPermissionConfigManager.ACCESS_REPORTS)]
    public virtual async Task<IActionResult> PaymentsList(NABTransactionSearchModel searchModel)
    {
        var model = await _modelFactory.PrepareTransactionListModelAsync(searchModel, approved: true);

        return Json(model);
    }

    #endregion

    #region Failed transactions report

    [CheckPermission(NABXMLPaymentPermissionConfigManager.ACCESS_REPORTS)]
    public virtual IActionResult Failed()
    {
        return View("~/Plugins/Payments.NABGateway/Views/NABXMLTransaction/Failed.cshtml", new NABTransactionSearchModel());
    }

    [HttpPost]
    [CheckPermission(NABXMLPaymentPermissionConfigManager.ACCESS_REPORTS)]
    public virtual async Task<IActionResult> FailedList(NABTransactionSearchModel searchModel)
    {
        var model = await _modelFactory.PrepareTransactionListModelAsync(searchModel, approved: false);

        return Json(model);
    }

    #endregion

    #region Details

    [CheckPermission(NABXMLPaymentPermissionConfigManager.ACCESS_REPORTS)]
    public virtual async Task<IActionResult> Details(int id)
    {
        var model = await _modelFactory.PrepareTransactionDetailsModelAsync(id);
        if (model == null)
            return Json(new { error = "Transaction not found" });

        return Json(model);
    }

    #endregion
}
