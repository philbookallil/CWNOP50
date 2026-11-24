using Nop.Core;
using Nop.Core.Domain.Orders;
using Nop.Plugin.CW.Core.Domain.Approvals;
using Nop.Plugin.CW.Core.Models.Approvals;
using Nop.Plugin.CW.Core.Services.Approvals;
using Nop.Plugin.CW.Core.Services.Orders;
using Nop.Services.Catalog;
using Nop.Services.Customers;
using Nop.Services.Directory;
using Nop.Services.Helpers;
using Nop.Services.Localization;
using Nop.Services.Orders;

namespace Nop.Plugin.CW.Core.Factories;

/// <summary>
/// Prepares the page an approver opens from the e-mail
/// </summary>
/// <remarks>
/// The approver is usually not signed in, so nothing here reads the current customer: the order is found from the link.
/// What was ordered is shown only while the link can still decide the order; afterwards the page says only where the
/// order stands.
/// </remarks>
public class OrderApprovalModelFactory : IOrderApprovalModelFactory
{
    #region Fields

    protected readonly ICurrencyService _currencyService;
    protected readonly ICustomerService _customerService;
    protected readonly ICWOrderService _cwOrderService;
    protected readonly IDateTimeHelper _dateTimeHelper;
    protected readonly ILocalizationService _localizationService;
    protected readonly IOrderApprovalService _orderApprovalService;
    protected readonly IOrderService _orderService;
    protected readonly IPriceFormatter _priceFormatter;
    protected readonly IProductService _productService;
    protected readonly IWorkContext _workContext;

    #endregion

    #region Ctor

    public OrderApprovalModelFactory(ICurrencyService currencyService,
        ICustomerService customerService,
        ICWOrderService cwOrderService,
        IDateTimeHelper dateTimeHelper,
        ILocalizationService localizationService,
        IOrderApprovalService orderApprovalService,
        IOrderService orderService,
        IPriceFormatter priceFormatter,
        IProductService productService,
        IWorkContext workContext)
    {
        _currencyService = currencyService;
        _customerService = customerService;
        _cwOrderService = cwOrderService;
        _dateTimeHelper = dateTimeHelper;
        _localizationService = localizationService;
        _orderApprovalService = orderApprovalService;
        _orderService = orderService;
        _priceFormatter = priceFormatter;
        _productService = productService;
        _workContext = workContext;
    }

    #endregion

    #region Utilities

    /// <summary>
    /// An amount of the order, in the currency the customer ordered in
    /// </summary>
    protected virtual async Task<string> FormatAsync(Order order, decimal amount, int languageId)
        => await _priceFormatter.FormatPriceAsync(_currencyService.ConvertCurrency(amount, order.CurrencyRate), true, order.CustomerCurrencyCode, false, languageId);

    /// <summary>
    /// The lines and totals of the order, as the approver reviews them
    /// </summary>
    protected virtual async Task PrepareOrderDetailsAsync(OrderApprovalReviewModel model, Order order, int languageId)
    {
        foreach (var item in await _orderService.GetOrderItemsAsync(order.Id))
        {
            var product = await _productService.GetProductByIdAsync(item.ProductId);

            model.Lines.Add(new OrderApprovalReviewLineModel
            {
                ProductName = product == null ? string.Empty : await _localizationService.GetLocalizedAsync(product, entity => entity.Name, languageId),
                AttributeInfo = item.AttributeDescription,
                PackName = (await _cwOrderService.GetPackNameOrderItemAsync(item.Id))?.PackName,
                Quantity = item.Quantity,
                UnitPrice = await FormatAsync(order, item.UnitPriceInclTax, languageId),
                Total = await FormatAsync(order, item.PriceInclTax, languageId)
            });
        }

        //the order as subsidy and store credit settled it; an order neither paid for has no settlement row
        var settlement = await _cwOrderService.GetOrderSettlementAsync(order.Id);
        var subsidy = (settlement?.SubsidyStockAmount ?? decimal.Zero) + (settlement?.SubsidyShippingAmount ?? decimal.Zero);
        var storeCredit = settlement?.CreditNoteAmount ?? decimal.Zero;

        model.Gross = await FormatAsync(order, settlement?.SettleableAmount ?? order.OrderTotal, languageId);
        model.Subsidy = subsidy > decimal.Zero ? await FormatAsync(order, subsidy, languageId) : null;
        model.StoreCredit = storeCredit > decimal.Zero ? await FormatAsync(order, storeCredit, languageId) : null;
        model.Payable = await FormatAsync(order, order.OrderTotal, languageId);
    }

    #endregion

    #region Methods

    public virtual async Task<OrderApprovalReviewModel> PrepareReviewModelAsync(string token, OrderApprovalResult? result = null)
    {
        var model = new OrderApprovalReviewModel { Token = token };

        var (approval, request) = await _orderApprovalService.GetByTokenAsync(token);
        var order = approval == null ? null : await _orderService.GetOrderByIdAsync(approval.OrderId);

        if (approval == null || request == null || order == null || order.Deleted)
        {
            model.Message = await _localizationService.GetResourceAsync("Plugins.CW.Core.OrderApproval.Result.NotFound");
            model.MessageIsError = true;
            return model;
        }

        var languageId = (await _workContext.GetWorkingLanguageAsync()).Id;
        var customer = await _customerService.GetCustomerByIdAsync(order.CustomerId);

        model.ShowOrder = true;
        model.ApproverName = request.ApproverName;
        model.OrderNumber = order.CustomOrderNumber;
        model.CustomerName = customer == null ? string.Empty : await _customerService.GetCustomerFullNameAsync(customer);
        model.CreatedOn = (await _dateTimeHelper.ConvertToUserTimeAsync(order.CreatedOnUtc, DateTimeKind.Utc)).ToString("D");
        model.Status = await _localizationService.GetResourceAsync($"Plugins.CW.Core.OrderApproval.Status.{approval.Status}");
        model.DecidedBy = approval.DecidedBy;
        model.DecisionComment = approval.DecisionComment;
        model.CanDecide = approval.Status == OrderApprovalStatus.Pending && request.RevokedOnUtc == null;

        if (model.CanDecide)
            await PrepareOrderDetailsAsync(model, order, languageId);

        //what the page says: what was just done, or why the link no longer decides
        var message = result switch
        {
            OrderApprovalResult.Done => approval.Status == OrderApprovalStatus.Approved ? "Plugins.CW.Core.OrderApproval.Result.Accepted" : "Plugins.CW.Core.OrderApproval.Result.Declined",
            OrderApprovalResult.NotFound => "Plugins.CW.Core.OrderApproval.Result.NotFound",
            OrderApprovalResult.Busy => "Plugins.CW.Core.OrderApproval.Result.Busy",
            OrderApprovalResult.Failed => "Plugins.CW.Core.OrderApproval.Result.Failed",
            OrderApprovalResult.ReasonRequired => "Plugins.CW.Core.OrderApproval.Result.ReasonRequired",
            _ when approval.Status != OrderApprovalStatus.Pending => "Plugins.CW.Core.OrderApproval.Result.AlreadyDecided",
            _ when request.RevokedOnUtc != null => "Plugins.CW.Core.OrderApproval.Result.LinkReplaced",
            _ => null
        };

        if (message != null)
        {
            model.Message = await _localizationService.GetResourceAsync(message);
            model.MessageIsError = result is not OrderApprovalResult.Done;
        }

        return model;
    }

    #endregion
}
