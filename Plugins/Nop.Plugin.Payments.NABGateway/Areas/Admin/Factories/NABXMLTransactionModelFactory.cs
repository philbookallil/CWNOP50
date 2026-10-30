using Nop.Core;
using Nop.Plugin.Payments.NABGateway.Areas.Admin.Models;
using Nop.Plugin.Payments.NABGateway.Domain;
using Nop.Services.Helpers;
using Nop.Services.Orders;
using Nop.Plugin.Payments.NABGateway.Services;
using Nop.Web.Framework.Models.Extensions;

namespace Nop.Plugin.Payments.NABGateway.Areas.Admin.Factories;

/// <summary>
/// Default <see cref="INABXMLTransactionModelFactory"/> implementation.
/// </summary>
public class NABXMLTransactionModelFactory : INABXMLTransactionModelFactory
{
    #region Fields

    protected readonly INABXMLTransactionService _transactionService;
    protected readonly IDateTimeHelper _dateTimeHelper;
    protected readonly IOrderService _orderService;

    #endregion

    #region Ctor

    public NABXMLTransactionModelFactory(INABXMLTransactionService transactionService,
        IDateTimeHelper dateTimeHelper,
        IOrderService orderService)
    {
        _transactionService = transactionService;
        _dateTimeHelper = dateTimeHelper;
        _orderService = orderService;
    }

    #endregion

    #region Utilities

    protected virtual async Task<NABTransactionModel> PrepareModelAsync(NABXMLTransaction entity, bool includeRaw)
    {
        var createdOn = await _dateTimeHelper.ConvertToUserTimeAsync(entity.CreatedOnUtc, DateTimeKind.Utc);

        var model = new NABTransactionModel
        {
            Id = entity.Id,
            OrderGuid = entity.OrderGuid.ToString(),
            CustomerId = entity.CustomerId,
            UserEmail = entity.UserEmail,
            UserFullName = entity.UserFullName,
            Approved = entity.Approved,
            MessageId = entity.MessageId,
            TxnId = entity.TxnId,
            AuthId = entity.AuthId,
            ResponseCode = entity.ResponseCode,
            ResponseText = entity.ResponseText,
            SettlementDate = entity.SettlementDate,
            MaskedPan = entity.MaskedPan,
            CardType = entity.CardType,
            PurchaseOrderNo = entity.PurchaseOrderNo,
            Amount = $"{entity.Amount:0.00} {entity.CurrencyCode}".Trim(),
            CreatedOn = createdOn.ToString("g")
        };

        //link back to the nopCommerce order when it exists
        var order = await _orderService.GetOrderByGuidAsync(entity.OrderGuid);
        if (order != null)
            model.OrderId = order.Id;

        if (includeRaw)
            model.RawResponseXml = entity.RawResponseXml;

        return model;
    }

    #endregion

    #region Methods

    public virtual async Task<NABTransactionListModel> PrepareTransactionListModelAsync(NABTransactionSearchModel searchModel, bool approved)
    {
        ArgumentNullException.ThrowIfNull(searchModel);

        //convert the admin (user time) search dates to UTC for comparison against CreatedOnUtc
        var timeZone = await _dateTimeHelper.GetCurrentTimeZoneAsync();

        DateTime? startDateUtc = searchModel.StartDate.HasValue
            ? _dateTimeHelper.ConvertToUtcTime(searchModel.StartDate.Value, timeZone)
            : null;

        DateTime? endDateUtc = searchModel.EndDate.HasValue
            ? _dateTimeHelper.ConvertToUtcTime(searchModel.EndDate.Value.AddDays(1), timeZone)
            : null;

        var transactions = await _transactionService.SearchTransactionsAsync(
            startDateUtc: startDateUtc,
            endDateUtc: endDateUtc,
            email: searchModel.UserEmail,
            userName: searchModel.UserName,
            authId: approved ? searchModel.AuthId : null,
            txnId: approved ? searchModel.TxnId : null,
            approved: approved,
            pageIndex: searchModel.Page - 1,
            pageSize: searchModel.PageSize);

        var model = await new NABTransactionListModel().PrepareToGridAsync(searchModel, transactions, () =>
        {
            return transactions.SelectAwait(async t => await PrepareModelAsync(t, includeRaw: false));
        });

        return model;
    }

    public virtual async Task<NABTransactionModel> PrepareTransactionDetailsModelAsync(int id)
    {
        var entity = await _transactionService.GetByIdAsync(id);
        if (entity == null)
            return null;

        return await PrepareModelAsync(entity, includeRaw: true);
    }

    #endregion
}
