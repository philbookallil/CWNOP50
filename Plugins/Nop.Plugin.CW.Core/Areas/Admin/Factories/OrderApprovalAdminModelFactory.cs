using Nop.Core.Domain.Directory;
using Nop.Plugin.CW.Core.Areas.Admin.Models.Approvals;
using Nop.Plugin.CW.Core.Domain.Approvals;
using Nop.Plugin.CW.Core.Services.Approvals;
using Nop.Plugin.CW.Core.Services.Security;
using Nop.Services.Catalog;
using Nop.Services.Directory;
using Nop.Services.Helpers;
using Nop.Services.Localization;
using Nop.Services.Security;

namespace Nop.Plugin.CW.Core.Areas.Admin.Factories;

/// <summary>
/// Prepares the approval card of the admin order page
/// </summary>
public class OrderApprovalAdminModelFactory : IOrderApprovalAdminModelFactory
{
    #region Fields

    protected readonly CurrencySettings _currencySettings;
    protected readonly ICurrencyService _currencyService;
    protected readonly IDateTimeHelper _dateTimeHelper;
    protected readonly ILocalizationService _localizationService;
    protected readonly IOrderApprovalService _orderApprovalService;
    protected readonly IPermissionService _permissionService;
    protected readonly IPriceFormatter _priceFormatter;

    #endregion

    #region Ctor

    public OrderApprovalAdminModelFactory(CurrencySettings currencySettings,
        ICurrencyService currencyService,
        IDateTimeHelper dateTimeHelper,
        ILocalizationService localizationService,
        IOrderApprovalService orderApprovalService,
        IPermissionService permissionService,
        IPriceFormatter priceFormatter)
    {
        _currencySettings = currencySettings;
        _currencyService = currencyService;
        _dateTimeHelper = dateTimeHelper;
        _localizationService = localizationService;
        _orderApprovalService = orderApprovalService;
        _permissionService = permissionService;
        _priceFormatter = priceFormatter;
    }

    #endregion

    #region Utilities

    protected virtual async Task<string> FormatDateAsync(DateTime? utc)
        => utc.HasValue ? (await _dateTimeHelper.ConvertToUserTimeAsync(utc.Value, DateTimeKind.Utc)).ToString("g") : null;

    #endregion

    #region Methods

    public virtual async Task<OrderApprovalAdminModel> PrepareOrderApprovalAdminModelAsync(int orderId)
    {
        var approval = await _orderApprovalService.GetOrderApprovalByOrderIdAsync(orderId);

        if (approval == null)
            return null;

        //the thresholds and the measured amount are in the primary store currency
        var primaryCurrency = await _currencyService.GetCurrencyByIdAsync(_currencySettings.PrimaryStoreCurrencyId);

        var model = new OrderApprovalAdminModel
        {
            OrderId = orderId,
            Status = await _localizationService.GetResourceAsync($"Plugins.CW.Core.OrderApproval.Status.{approval.Status}"),
            IsPending = approval.Status == OrderApprovalStatus.Pending,
            CanManage = await _permissionService.AuthorizeAsync(CWPermissionConfigManager.ADMIN_MANAGE_ORDER_APPROVALS),
            MeasuredAmount = await _priceFormatter.FormatPriceAsync(approval.MeasuredAmount, true, primaryCurrency),
            RequestedOn = await FormatDateAsync(approval.RequestedOnUtc),
            LastReminderNumber = approval.LastReminderNumber,
            DecidedBy = approval.DecidedBy,
            DecidedOn = await FormatDateAsync(approval.DecidedOnUtc),
            DecisionComment = approval.DecisionComment
        };

        foreach (var request in await _orderApprovalService.GetRequestsAsync(approval.Id))
        {
            model.Requests.Add(new OrderApprovalRequestAdminModel
            {
                ApproverName = request.ApproverName,
                ApproverEmail = request.ApproverEmail,
                SentOn = await FormatDateAsync(request.SentOnUtc),
                IsLive = request.RevokedOnUtc == null && model.IsPending
            });
        }

        return model;
    }

    #endregion
}
