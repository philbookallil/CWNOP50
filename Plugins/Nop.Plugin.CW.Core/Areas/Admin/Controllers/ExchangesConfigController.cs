using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Nop.Plugin.CW.Core.Areas.Admin.Factories;
using Nop.Plugin.CW.Core.Areas.Admin.Models.Exchanges;
using Nop.Plugin.CW.Core.Areas.Admin.Models.Exchanges.FeeRule;
using Nop.Plugin.CW.Core.Areas.Admin.Models.Exchanges.ReturnActionRule;
using Nop.Plugin.CW.Core.Areas.Admin.Models.Exchanges.ReturnReasonRule;
using Nop.Plugin.CW.Core.Areas.Admin.Models.Exchanges.ReturnStatusRule;
using Nop.Plugin.CW.Core.Areas.Admin.Models.Orders;
using Nop.Plugin.CW.Core.Domain.Exchanges;
using Nop.Plugin.CW.Core.Domain.Settings;
using Nop.Plugin.CW.Core.Filters;
using Nop.Plugin.CW.Core.Services.Exchanges;
using Nop.Plugin.CW.Core.Services.Security;
using Nop.Services.Orders;
using Nop.Web.Areas.Admin.Controllers;
using Nop.Web.Framework.Models.Extensions;
using Nop.Web.Framework.Mvc.Filters;

namespace Nop.Plugin.CW.Core.Areas.Admin.Controllers;

/// <summary>
/// Single admin controller for the Exchanges/Returns settings (rule grids, reason→action linking,
/// fee rules, and the order-details return-requests grid). Consolidated from the former
/// ExchangesController + ExchangesConfigController; every action is permission-gated.
/// </summary>
public class ExchangesConfigController : BaseAdminController
{
    #region Fields

    protected readonly IExchangeSettingsModelFactory _exchangeSettingsModelFactory;
    protected readonly IExchangeService _exchangeService;
    protected readonly IReturnRequestService _returnRequestService;
    protected readonly IOrderService _orderService;
    protected readonly IReturnRequestOverrideModelFactory _returnRequestModelFactory;

    #endregion

    #region Ctor

    public ExchangesConfigController(IExchangeSettingsModelFactory exchangeSettingsModelFactory,
        IExchangeService exchangeService,
        IReturnRequestService returnRequestService,
        IOrderService orderService,
        IReturnRequestOverrideModelFactory returnRequestModelFactory)
    {
        _exchangeSettingsModelFactory = exchangeSettingsModelFactory;
        _exchangeService = exchangeService;
        _returnRequestService = returnRequestService;
        _orderService = orderService;
        _returnRequestModelFactory = returnRequestModelFactory;
    }

    #endregion

    #region Utilities

    private async Task<JsonResult> CreateReturnStatusRuleAsync(ReturnStatusRuleModel model, int returnStatusId)
    {
        //Pending status is zero
        if (returnStatusId < 0)
            ModelState.AddModelError(string.Empty, "A Return Status must be selected");

        if (await _exchangeService.ReturnStatusRuleExistByStatusIdAsync(returnStatusId, model.StoreId))
            ModelState.AddModelError(string.Empty, "A rule for this Return Status is already in place");

        if (ModelState.IsValid)
        {
            var rule = new ReturnStatusRule
            {
                StoreId = model.StoreId,
                ReturnStatusId = returnStatusId,
                PerformExchange = model.PerformExchange,
                PerformStoreCredit = model.PerformStoreCredit,
                BackInStock = model.BackInStock,
                Authorised = model.Authorised,
            };

            await _exchangeService.InsertReturnStatusRuleAsync(rule);

            return JSONSuccess("The Return Status rule has been created!");
        }

        return JSONError("The Return Status rule for this Store can not be created");
    }

    private async Task<JsonResult> UpdateReturnStatusRuleAsync(ReturnStatusRule rule, ReturnStatusRuleModel model)
    {
        rule.PerformStoreCredit = model.PerformStoreCredit;
        rule.PerformExchange = model.PerformExchange;
        rule.BackInStock = model.BackInStock;
        rule.Authorised = model.Authorised;

        await _exchangeService.UpdateReturnStatusRuleAsync(rule);

        return JSONSuccess("The Return Status rule has been updated!");
    }

    private async Task<IList<SelectListItem>> PrepareLinkedActionsAsync()
    {
        IList<SelectListItem> items = new List<SelectListItem>();

        var reasonActions = await _returnRequestService.GetAllReturnRequestActionsAsync();

        foreach (var action in reasonActions)
        {
            items.Add(new SelectListItem
            {
                Text = action.Name,
                Value = action.Id.ToString()
            });
        }

        return items;
    }

    private async Task<JsonResult> CreateReturnReasonRuleAsync(ReturnReasonRuleModel model)
    {
        if (model.ReturnReasonId == 0)
            ModelState.AddModelError(string.Empty, "A Return Reason must be selected");

        if (await _exchangeService.ReturnReasonRuleExistByReasonIdAsync(model.ReturnReasonId, model.StoreId))
            ModelState.AddModelError(string.Empty, "A rule for this Return Reason is already in place");

        if (ModelState.IsValid)
        {
            var rule = new ReturnReasonRule
            {
                StoreId = model.StoreId,
                ReturnReasonId = model.ReturnReasonId,
                AllowExchange = model.AllowExchange,
                AllowStoreCredit = model.AllowStoreCredit,
                AllowRepackagingFee = model.AllowRepackagingFee,
                AllowShippingFee = model.AllowShippingFee,
                BackInStock = model.BackInStock,
                IsDisabled = !model.IsActive
            };

            await _exchangeService.InsertReturnReasonRuleAsync(rule);

            return JSONSuccess("The Return Reason rule has been created!");
        }

        return JSONError("The Reason Rule for this store can not be created");
    }

    private async Task<JsonResult> UpdateReturnReasonRuleAsync(ReturnReasonRule rule, ReturnReasonRuleModel model)
    {
        rule.AllowExchange = model.AllowExchange;
        rule.AllowStoreCredit = model.AllowStoreCredit;
        rule.AllowRepackagingFee = model.AllowRepackagingFee;
        rule.AllowShippingFee = model.AllowShippingFee;
        rule.BackInStock = model.BackInStock;
        rule.IsDisabled = !model.IsActive;

        await _exchangeService.UpdateReturnReasonRuleAsync(rule);

        return JSONSuccess("The Return Reason rule has been updated!");
    }

    private async Task<JsonResult> CreateReturnActionRuleAsync(ReturnActionRuleModel model)
    {
        if (model.ReturnActionId == 0)
            ModelState.AddModelError(string.Empty, "A Return Action must be selected");

        if (await _exchangeService.ReturnActionRuleExistByActionIdAsync(model.ReturnActionId, model.StoreId))
            ModelState.AddModelError(string.Empty, "A rule for this Return Action is already in place");

        if (ModelState.IsValid)
        {
            var rule = new ReturnActionRule
            {
                StoreId = model.StoreId,
                ReturnActionId = model.ReturnActionId,
                AllowExchange = model.AllowExchange,
                AllowStoreCredit = model.AllowStoreCredit,
                AllowRepackagingFee = model.AllowRepackagingFee,
                AllowShippingFee = model.AllowShippingFee,
                BackInStock = model.BackInStock,
                IsDisabled = !model.IsActive,
            };

            await _exchangeService.InsertReturnActionRuleAsync(rule);

            return JSONSuccess("The Return Action rule has been created!");
        }

        return JSONError("The Action Rule for this store can not be created");
    }

    private async Task<JsonResult> UpdateReturnActionRuleAsync(ReturnActionRule rule, ReturnActionRuleModel model)
    {
        rule.AllowExchange = model.AllowExchange;
        rule.AllowStoreCredit = model.AllowStoreCredit;
        rule.BackInStock = model.BackInStock;
        rule.AllowRepackagingFee = model.AllowRepackagingFee;
        rule.AllowShippingFee = model.AllowShippingFee;
        rule.IsDisabled = !model.IsActive;

        await _exchangeService.UpdateReturnActionRuleAsync(rule);

        return JSONSuccess("The Return Action rule has been updated!");
    }

    protected JsonResult JSONSuccess(string message)
    {
        return JSONResult(message, true);
    }

    protected JsonResult JSONError(string message)
    {
        return JSONResult(message, false);
    }

    protected JsonResult JSONResult(string message, bool success)
    {
        return new JsonResult(new
        {
            success,
            message
        });
    }

    private static string ValidateFeeRule(FeeRuleModel model)
    {
        if (!Enum.IsDefined(typeof(FeeType), model.FeeTypeId) || (FeeType)model.FeeTypeId == FeeType.AdditionalFee)
            return "Please select a valid fee type.";
        if (!Enum.IsDefined(typeof(FeeRuleOperator), model.OperatorId))
            return "Please select a valid rule type.";
        if (model.Amount <= decimal.Zero)
            return "The amount must be greater than zero.";
        if ((FeeRuleOperator)model.OperatorId == FeeRuleOperator.PerUnitsQuantity && model.UnitsQuantity < 1)
            return "The units quantity must be at least 1.";
        return null;
    }

    private static int NormalizeUnitsQuantity(FeeRuleModel model)
    {
        // UnitsQuantity is meaningless for fixed-price rules; store 1 so fee math never divides by zero
        return (FeeRuleOperator)model.OperatorId == FeeRuleOperator.FixedPrice ? 1 : model.UnitsQuantity;
    }

    #endregion


    #region Fee rules

    [CheckPermission(CWPermissionConfigManager.ADMIN_ACCESS_EXCHANGES)]
    [CheckCWFeature(CWFeature.Exchanges, StoreSource = CWFeatureStoreSource.AnyStore)]
    public virtual IActionResult GlobalFeeSettings()
    {
        return View("~/Plugins/CW.Core/Areas/Admin/Views/Exchanges/GlobalFeeSettings.cshtml", new FeeRuleSearchModel { StoreId = 0 });
    }

    [HttpPost]
    [CheckPermission(CWPermissionConfigManager.ADMIN_ACCESS_EXCHANGES)]
    [CheckCWFeature(CWFeature.Exchanges, StoreSource = CWFeatureStoreSource.Parameter, AllowDefaultStore = true)]
    public virtual async Task<IActionResult> GetFeeRules(FeeRuleSearchModel searchModel)
    {
        var model = await _exchangeSettingsModelFactory.PrepareFeeRuleListModelAsync(searchModel);
        return Json(model);
    }

    [HttpPost]
    [CheckPermission(CWPermissionConfigManager.ADMIN_ACCESS_EXCHANGES)]
    [CheckCWFeature(CWFeature.Exchanges, StoreSource = CWFeatureStoreSource.Parameter, AllowDefaultStore = true)]
    public virtual async Task<IActionResult> CreateFeeRule(FeeRuleModel model)
    {
        var validationError = ValidateFeeRule(model);
        if (validationError != null)
            return ErrorJson(validationError);

        // One rule per fee type per store
        var existing = await _exchangeService.GetRuleByFeeTypeAsync(model.FeeTypeId, model.StoreId);
        if (existing != null)
            return ErrorJson("A rule for this fee type already exists. Edit it instead.");

        await _exchangeService.InsertFeeRuleAsync(new FeeRule
        {
            StoreId = model.StoreId,
            FeeTypeId = model.FeeTypeId,
            Amount = model.Amount,
            OperatorId = model.OperatorId,
            UnitsQuantity = NormalizeUnitsQuantity(model)
        });

        return Json(new { success = true });
    }

    [HttpPost]
    [CheckPermission(CWPermissionConfigManager.ADMIN_ACCESS_EXCHANGES)]
    [CheckCWFeature(CWFeature.Exchanges, StoreSource = CWFeatureStoreSource.Parameter, AllowDefaultStore = true)]
    public virtual async Task<IActionResult> UpdateFeeRule(FeeRuleModel model)
    {
        var validationError = ValidateFeeRule(model);
        if (validationError != null)
            return ErrorJson(validationError);

        var rule = await _exchangeService.GetFeeRuleByIdAsync(model.Id);
        if (rule == null)
            return ErrorJson("The fee rule no longer exists.");

        // Editing an inherited (global) rule from a store creates a store-level override (4.10 behavior)
        if (rule.StoreId != model.StoreId)
        {
            await _exchangeService.InsertFeeRuleAsync(new FeeRule
            {
                StoreId = model.StoreId,
                FeeTypeId = rule.FeeTypeId,
                Amount = model.Amount,
                OperatorId = model.OperatorId,
                UnitsQuantity = NormalizeUnitsQuantity(model)
            });
            return Json(new { success = true });
        }

        rule.FeeTypeId = model.FeeTypeId;
        rule.Amount = model.Amount;
        rule.OperatorId = model.OperatorId;
        rule.UnitsQuantity = NormalizeUnitsQuantity(model);
        await _exchangeService.UpdateFeeRuleAsync(rule);

        return Json(new { success = true });
    }

    [HttpPost]
    [CheckPermission(CWPermissionConfigManager.ADMIN_ACCESS_EXCHANGES)]
    [CheckCWFeature(CWFeature.Exchanges, StoreSource = CWFeatureStoreSource.Parameter, AllowDefaultStore = true)]
    public virtual async Task<IActionResult> DeleteFeeRule(int id, int storeId)
    {
        var rule = await _exchangeService.GetFeeRuleByIdAsync(id);
        if (rule == null)
            return ErrorJson("The fee rule no longer exists.");

        // Inherited (global) rules can't be deleted from a store — only overridden
        if (rule.StoreId != storeId)
            return ErrorJson("Inherited rules cannot be deleted at store level.");

        await _exchangeService.DeleteFeeRuleAsync(rule);

        return Json(new { success = true });
    }

    #endregion

    #region Return reason rules

    [HttpPost]
    [CheckPermission(CWPermissionConfigManager.ADMIN_ACCESS_EXCHANGES)]
    [CheckCWFeature(CWFeature.Exchanges, StoreSource = CWFeatureStoreSource.Parameter)]
    public async Task<JsonResult> GetReturnReasonsRules(ReturnReasonRuleSearchModel searchModel)
    {
        //prepare model
        var model = await _exchangeSettingsModelFactory.PrepareReturnReasonRuleListModelAsync(searchModel);

        return Json(model);
    }

    [HttpPost]
    [CheckPermission(CWPermissionConfigManager.ADMIN_ACCESS_EXCHANGES)]
    [CheckCWFeature(CWFeature.Exchanges, StoreSource = CWFeatureStoreSource.Parameter)]
    public async Task<JsonResult> UpdateReturnReasonRule(ReturnReasonRuleModel model)
    {
        var existingRule = await _exchangeService.GetReturnReasonRuleByReasonIdAsync(model.ReturnReasonId, model.StoreId);

        if (existingRule != null) // We are editing a rule
        {
            return await UpdateReturnReasonRuleAsync(existingRule, model);
        }
        else // We need to create this rule
        {
            return await CreateReturnReasonRuleAsync(model);
        }
    }

    #endregion

    #region Return action rules

    [HttpPost]
    [CheckPermission(CWPermissionConfigManager.ADMIN_ACCESS_EXCHANGES)]
    [CheckCWFeature(CWFeature.Exchanges, StoreSource = CWFeatureStoreSource.Parameter)]
    public async Task<JsonResult> GetReturnActionsRules(ReturnActionRuleSearchModel searchModel)
    {
        //prepare model
        var model = await _exchangeSettingsModelFactory.PrepareReturnActionRuleListModelAsync(searchModel);

        return Json(model);
    }

    [HttpPost]
    [CheckPermission(CWPermissionConfigManager.ADMIN_ACCESS_EXCHANGES)]
    [CheckCWFeature(CWFeature.Exchanges, StoreSource = CWFeatureStoreSource.Parameter)]
    public async Task<JsonResult> UpdateReturnActionRule(ReturnActionRuleModel model)
    {
        var existingRule = await _exchangeService.GetReturnActionRuleByActionIdAsync(model.ReturnActionId, model.StoreId);

        if (existingRule != null) // We are editing a rule
        {
            return await UpdateReturnActionRuleAsync(existingRule, model);
        }
        else // We need to create this rule
        {
            return await CreateReturnActionRuleAsync(model);
        }
    }

    #endregion

    #region Return status rules

    [HttpPost]
    [CheckPermission(CWPermissionConfigManager.ADMIN_ACCESS_EXCHANGES)]
    [CheckCWFeature(CWFeature.Exchanges, StoreSource = CWFeatureStoreSource.Parameter)]
    public async Task<JsonResult> GetReturnStatusRules(ReturnStatusRuleSearchModel searchModel)
    {
        //prepare model
        var model = await _exchangeSettingsModelFactory.PrepareReturnStatusRuleListModelAsync(searchModel);

        return Json(model);
    }

    [HttpPost]
    [CheckPermission(CWPermissionConfigManager.ADMIN_ACCESS_EXCHANGES)]
    [CheckCWFeature(CWFeature.Exchanges, StoreSource = CWFeatureStoreSource.Parameter)]
    public async Task<JsonResult> UpdateReturnStatusRule(ReturnStatusRuleModel model)
    {
        var returnStatusId = model.Id - 1; //when we create the model we add 1 to the return status id to avoid having a 0 value in the model. So we need to subtract 1 here to get the correct return status id.
        var existingRule = await _exchangeService.GetRuleByStatusIdAsync(returnStatusId, model.StoreId);

        if (existingRule != null) // We are editing a rule
        {
            return await UpdateReturnStatusRuleAsync(existingRule, model);
        }
        else // We need to create this rule
        {
            return await CreateReturnStatusRuleAsync(model, returnStatusId);
        }
    }

    #endregion

    #region Reason → action linking

    [CheckPermission(CWPermissionConfigManager.ADMIN_ACCESS_EXCHANGES)]
    [CheckCWFeature(CWFeature.Exchanges, StoreSource = CWFeatureStoreSource.Parameter)]
    public virtual async Task<IActionResult> AddLinkedActionsPopup(int id, int storeId)
    {
        //prepare model
        var model = new ReasonRulesActionsModel
        {
            AvailableLinkedActions = await PrepareLinkedActionsAsync(),
            StoreId = storeId,
            ReturnReasonId = id,
            LinkedActionsIds = (await _exchangeService.GetActionMappingsByReasonIdAsync(id, storeId)).Select(a => a.ActionId).ToList()
        };

        return View("~/Plugins/CW.Core/Areas/Admin/Views/Exchanges/AddLinkedActionsPopup.cshtml", model);
    }

    [HttpPost]
    [CheckPermission(CWPermissionConfigManager.ADMIN_ACCESS_EXCHANGES)]
    [CheckCWFeature(CWFeature.Exchanges, StoreSource = CWFeatureStoreSource.Parameter)]
    public async Task<IActionResult> AddLinkedActionsPopup(ReasonRulesActionsModel model)
    {
        //nothing selected posts no value at all, which binds as null
        await _exchangeService.LinkReasonToActionsAsync(model.ReturnReasonId, model.StoreId, model.LinkedActionsIds ?? []);

        ViewBag.RefreshPage = true;

        return View("~/Plugins/CW.Core/Areas/Admin/Views/Exchanges/AddLinkedActionsPopup.cshtml", model);
    }

    #endregion

    #region Order return requests grid (injected via AdminWidgetZones.OrderDetailsBlock)

    [HttpPost]
    [CheckPermission(CWPermissionConfigManager.ADMIN_ACCESS_EXCHANGES)]
    public async Task<JsonResult> GetOrderReturnRequests(OrderReturnRequestSearchModel searchModel)
    {
        var order = await _orderService.GetOrderByIdAsync(searchModel.OrderId);

        if (order != null)
        {
            var orderReturns = (await _exchangeService.GetReturnsForOrderAsync(order)).ToPagedList(searchModel);

            var gridModel = await new OrderReturnRequestListModel().PrepareToGridAsync(searchModel, orderReturns, () =>
            {
                return orderReturns.SelectAwait(async rr => await _returnRequestModelFactory.PrepareReturnRequestModelAsync(rr, true));
            });

            return Json(gridModel);
        }

        return JSONError("No order with ID: " + searchModel.OrderId);
    }

    #endregion
}
