using Nop.Core;
using Nop.Plugin.CW.Core.Areas.Admin.Models.Exchanges;
using Nop.Plugin.CW.Core.Areas.Admin.Models.Exchanges.FeeRule;
using Nop.Plugin.CW.Core.Areas.Admin.Models.Exchanges.ReturnActionRule;
using Nop.Plugin.CW.Core.Areas.Admin.Models.Exchanges.ReturnReasonRule;
using Nop.Plugin.CW.Core.Areas.Admin.Models.Exchanges.ReturnStatusRule;
using Nop.Plugin.CW.Core.Domain.Exchanges;
using Nop.Plugin.CW.Core.Helpers;
using Nop.Plugin.CW.Core.Services.Exchanges;
using Nop.Services.Catalog;
using Nop.Services.Customers;
using Nop.Services.Helpers;
using Nop.Services.Localization;
using Nop.Services.Orders;
using Nop.Web.Framework.Models.Extensions;

namespace Nop.Plugin.CW.Core.Areas.Admin.Factories;

public class ExchangeSettingsModelFactory : IExchangeSettingsModelFactory
{
    #region Fields

    protected readonly IExchangeService _exchangeService;
    protected readonly IReturnRequestService _returnRequestService;
    protected readonly ILocalizationService _localizationService;
    protected readonly IOrderService _orderService;
    protected readonly IProductService _productService;
    protected readonly ICustomerService _customerService;
    protected readonly IDateTimeHelper _dateTimeHelper;

    #endregion

    #region Ctor

    public ExchangeSettingsModelFactory(IExchangeService exchangeService,
        IReturnRequestService returnRequestService,
        ILocalizationService localizationService,
        IOrderService orderService,
        IProductService productService,
        ICustomerService customerService,
        IDateTimeHelper dateTimeHelper)
    {
        _exchangeService = exchangeService;
        _returnRequestService = returnRequestService;
        _localizationService = localizationService;
        _orderService = orderService;
        _productService = productService;
        _customerService = customerService;
        _dateTimeHelper = dateTimeHelper;
    }

    #endregion

    #region Utilities

    protected virtual async Task<FeeRuleModel> PrepareFeeRuleGridModelAsync(FeeRule rule, bool isInherited)
    {
        return new FeeRuleModel
        {
            Id = rule.Id,
            StoreId = rule.StoreId,
            FeeTypeId = rule.FeeTypeId,
            FeeTypeName = ExchangesHelper.DetermineFeeTypeName(rule.FeeType),
            Amount = rule.Amount,
            OperatorId = rule.OperatorId,
            OperatorName = ExchangesHelper.DetermineFeeRuleOperatorName(rule.Operator),
            UnitsQuantity = rule.UnitsQuantity,
            IsInherited = isInherited
        };
    }

    #endregion

    #region Methods

    public virtual async Task<ReturnReasonRuleListModel> PrepareReturnReasonRuleListModelAsync(ReturnReasonRuleSearchModel searchModel)
    {
        ArgumentNullException.ThrowIfNull(searchModel);

        var returnReasonRules = await _exchangeService.GetReturnReasonRulesAsync(searchModel.StoreId, searchModel.Page - 1, searchModel.PageSize);

        //prepare grid model
        var model = new ReturnReasonRuleListModel().PrepareToGrid(searchModel, returnReasonRules, () =>
        {
            return returnReasonRules.Select(rule =>
            {
                //fill in model values from the entity
                var ruleModel = new ReturnReasonRuleModel
                {
                    Id = rule.ReturnReasonId, //We use the return reason identifier because the rule could not exist and the reason is unique, we shouldn't have two rules for the same reason
                    StoreId = searchModel.StoreId,
                    ReturnReasonId = rule.ReturnReasonId,
                    ReturnReason = rule.ReturnReason,
                    AllowStoreCredit = rule.AllowStoreCredit,
                    AllowExchange = rule.AllowExchange,
                    AllowRepackagingFee = rule.AllowRepackagingFee,
                    AllowShippingFee = rule.AllowShippingFee,
                    BackInStock = rule.BackInStock,
                    IsActive = !rule.IsDisabled,
                    DisplayOrder = rule.DisplayOrder,
                    // Count of EXPLICIT reason → action links (0 = "all active actions apply" default)
                    LinkedActions = rule.LinkedActionsCount,
                    LinkedActionsNames = rule.LinkedActionsNames
                };

                return ruleModel;
            });
        });

        return model;
    }

    public virtual async Task<ReturnActionRuleListModel> PrepareReturnActionRuleListModelAsync(ReturnActionRuleSearchModel searchModel)
    {
        ArgumentNullException.ThrowIfNull(searchModel);

        var returnActionRules = await _exchangeService.GetReturnActionRulesAsync(searchModel.StoreId, searchModel.Page - 1, searchModel.PageSize);

        //prepare grid model
        var model = new ReturnActionRuleListModel().PrepareToGrid(searchModel, returnActionRules, () =>
        {
            return returnActionRules.Select(rule =>
            {
                //fill in model values from the entity
                var ruleModel = new ReturnActionRuleModel
                {
                    Id = rule.ReturnActionId, //We use the return action identifier because the rule could not exist and the action is unique, we shouldn't have two rules for the same action
                    StoreId = searchModel.StoreId,
                    ReturnActionId = rule.ReturnActionId,
                    ReturnAction = rule.ReturnAction,
                    AllowStoreCredit = rule.AllowStoreCredit,
                    AllowExchange = rule.AllowExchange,
                    AllowRepackagingFee = rule.AllowRepackagingFee,
                    AllowShippingFee = rule.AllowShippingFee,
                    BackInStock = rule.BackInStock,
                    IsActive = !rule.IsDisabled,
                    DisplayOrder = rule.DisplayOrder,
                };

                return ruleModel;
            });
        });

        return model;
    }

    public virtual async Task<ReturnStatusRuleListModel> PrepareReturnStatusRuleListModelAsync(ReturnStatusRuleSearchModel searchModel)
    {
        ArgumentNullException.ThrowIfNull(searchModel);

        var statuses = _exchangeService.GetAllReturnStatuses();
        var rules = await _exchangeService.GetReturnStatusRulesAsync(searchModel.StoreId);
        var rulesByStatus = rules.ToDictionary(r => r.ReturnStatusId);

        var rows = new List<ReturnStatusRuleModel>();
        foreach (var status in statuses)
        {
            rulesByStatus.TryGetValue((int)status, out var rule);

            rows.Add(new ReturnStatusRuleModel
            {
                // Grid row ids are statusId + 1: ReturnRequestStatus.Pending = 0 and the DataTables
                // inline-edit JS keys rows on Id, where 0 is unsafe. The controller reverses the shift.
                Id = (int)status + 1,
                StoreId = searchModel.StoreId,
                ReturnStatusId = (int)status,
                ReturnStatus = await _localizationService.GetLocalizedEnumAsync(status),
                PerformStoreCredit = rule?.PerformStoreCredit ?? false,
                PerformExchange = rule?.PerformExchange ?? false,
                BackInStock = rule?.BackInStock ?? false,
                Authorised = rule?.Authorised ?? false,
            });
        }

        var paged = new PagedList<ReturnStatusRuleModel>(rows, searchModel.Page - 1, searchModel.PageSize);
        return await new ReturnStatusRuleListModel().PrepareToGridAsync(searchModel, paged, () => paged.ToAsyncEnumerable());
    }

    public virtual async Task<FeeRuleListModel> PrepareFeeRuleListModelAsync(FeeRuleSearchModel searchModel)
    {
        ArgumentNullException.ThrowIfNull(searchModel);

        var storeRules = await _exchangeService.GetFeeRulesAsync(searchModel.StoreId);

        var rows = new List<FeeRuleModel>();

        // At store level, global rules without a store override for the same fee type show as inherited
        if (searchModel.StoreId > 0)
        {
            var globalRules = await _exchangeService.GetFeeRulesAsync(0);
            foreach (var globalRule in globalRules.Where(g => !storeRules.Any(s => s.FeeTypeId == g.FeeTypeId)))
                rows.Add(await PrepareFeeRuleGridModelAsync(globalRule, isInherited: true));
        }

        foreach (var rule in storeRules)
            rows.Add(await PrepareFeeRuleGridModelAsync(rule, isInherited: false));

        rows = rows.OrderBy(r => r.FeeTypeId).ToList();

        var paged = new PagedList<FeeRuleModel>(rows, searchModel.Page - 1, searchModel.PageSize);
        return await new FeeRuleListModel().PrepareToGridAsync(searchModel, paged, () => paged.ToAsyncEnumerable());
    }

    public virtual async Task<IList<CWReturnRequestModel>> PrepareOrderReturnRequestGridAsync(int orderId)
    {
        var order = await _orderService.GetOrderByIdAsync(orderId);
        if (order is null)
            return new List<CWReturnRequestModel>();

        var returnRequests = await _exchangeService.GetReturnsForOrderAsync(order);
        var result = new List<CWReturnRequestModel>();

        foreach (var rr in returnRequests)
        {
            var orderItem = await _orderService.GetOrderItemByIdAsync(rr.OrderItemId);
            var product = orderItem is not null ? await _productService.GetProductByIdAsync(orderItem.ProductId) : null;
            var customer = await _customerService.GetCustomerByIdAsync(rr.CustomerId);
            var createdOn = await _dateTimeHelper.ConvertToUserTimeAsync(rr.CreatedOnUtc, DateTimeKind.Utc);

            var model = new CWReturnRequestModel
            {
                Id = rr.Id,
                CustomNumber = rr.CustomNumber,
                ProductId = product?.Id ?? 0,
                ProductName = product?.Name ?? string.Empty,
                AttributeInfo = orderItem?.AttributeDescription ?? string.Empty,
                Quantity = rr.Quantity,
                CustomerId = rr.CustomerId,
                CustomerInfo = customer?.Email ?? rr.CustomerId.ToString(),
                ReturnRequestStatusStr = await _localizationService.GetLocalizedEnumAsync(rr.ReturnRequestStatus),
                CreatedOn = createdOn
            };

            result.Add(model);
        }

        return result;
    }


    #endregion
}
