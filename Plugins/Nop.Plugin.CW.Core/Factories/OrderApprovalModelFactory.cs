using Nop.Core;
using Nop.Core.Domain.Common;
using Nop.Core.Domain.Orders;
using Nop.Plugin.CW.Core.Domain.Approvals;
using Nop.Plugin.CW.Core.Domain.Orders.DTOs;
using Nop.Plugin.CW.Core.Helpers;
using Nop.Plugin.CW.Core.Models.Approvals;
using Nop.Plugin.CW.Core.Services.Approvals;
using Nop.Plugin.CW.Core.Services.Orders;
using Nop.Plugin.CW.Core.Services.PackNames;
using Nop.Services.Catalog;
using Nop.Services.Common;
using Nop.Services.Customers;
using Nop.Services.Directory;
using Nop.Services.Helpers;
using Nop.Services.Localization;
using Nop.Services.Orders;

namespace Nop.Plugin.CW.Core.Factories;

/// <summary>
/// Prepares the page an approver opens from the e-mail, and the status the customer sees
/// </summary>
/// <remarks>
/// The approver is usually not signed in, so nothing here reads the current customer: the order is found from the link.
/// What was ordered is shown only while the link can still decide the order; afterwards the page says only where the
/// order stands.
/// </remarks>
public class OrderApprovalModelFactory : IOrderApprovalModelFactory
{
    #region Constants

    /// <summary>
    /// The most people named as sharing a delivery
    /// </summary>
    protected const int MaxSharedPackNames = 3;

    /// <summary>
    /// The colour of a packname that has none, or is gone
    /// </summary>
    protected const string DefaultColour = "#666666";

    /// <summary>
    /// What is shown of an address, in the order a packname's address is written
    /// </summary>
    protected static readonly AddressField[] AddressFields =
        [AddressField.Address1, AddressField.Address2, AddressField.City, AddressField.StateProvince, AddressField.ZipPostalCode];

    #endregion

    #region Fields

    protected readonly IAddressService _addressService;
    protected readonly ICurrencyService _currencyService;
    protected readonly ICustomerService _customerService;
    protected readonly ICWOrderService _cwOrderService;
    protected readonly IDateTimeHelper _dateTimeHelper;
    protected readonly ILocalizationService _localizationService;
    protected readonly IOrderApprovalService _orderApprovalService;
    protected readonly IOrderService _orderService;
    protected readonly IPackNameService _packNameService;
    protected readonly IPriceFormatter _priceFormatter;
    protected readonly IProductService _productService;
    protected readonly IWorkContext _workContext;

    #endregion

    #region Ctor

    public OrderApprovalModelFactory(IAddressService addressService,
        ICurrencyService currencyService,
        ICustomerService customerService,
        ICWOrderService cwOrderService,
        IDateTimeHelper dateTimeHelper,
        ILocalizationService localizationService,
        IOrderApprovalService orderApprovalService,
        IOrderService orderService,
        IPackNameService packNameService,
        IPriceFormatter priceFormatter,
        IProductService productService,
        IWorkContext workContext)
    {
        _addressService = addressService;
        _currencyService = currencyService;
        _customerService = customerService;
        _cwOrderService = cwOrderService;
        _dateTimeHelper = dateTimeHelper;
        _localizationService = localizationService;
        _orderApprovalService = orderApprovalService;
        _orderService = orderService;
        _packNameService = packNameService;
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
    /// The products of one group, with how many and what they cost together
    /// </summary>
    protected virtual async Task PrepareLinesAsync(OrderApprovalReviewGroupModel model, IList<OrderItem> items, Order order, int languageId)
    {
        foreach (var item in items)
        {
            var product = await _productService.GetProductByIdAsync(item.ProductId);

            model.Lines.Add(new OrderApprovalReviewLineModel
            {
                ProductName = product == null ? string.Empty : await _localizationService.GetLocalizedAsync(product, entity => entity.Name, languageId),
                AttributeInfo = item.AttributeDescription,
                Quantity = item.Quantity,
                UnitPrice = await FormatAsync(order, item.UnitPriceInclTax, languageId),
                Total = await FormatAsync(order, item.PriceInclTax, languageId)
            });
        }

        model.ItemCount = items.Sum(item => item.Quantity);
        model.Total = await FormatAsync(order, items.Sum(item => item.PriceInclTax), languageId);
    }

    /// <summary>
    /// How and where a packname is delivered, and with whom when only a few share it
    /// </summary>
    protected virtual async Task<string> GetDeliveryAsync(PackNameOrderGroupDTO group)
    {
        var parts = new List<string> { group.Shipping?.ShippingMethod };

        //a packname collected in store has no address of its own
        if (group.Shipping?.PickupInStore != true)
            parts.AddRange([group.AddressName, group.AddressLine1, group.AddressLine2, group.AddressCityStateZip]);

        var delivery = string.Join(", ", parts.Where(part => !string.IsNullOrWhiteSpace(part)));

        if (delivery.Length == 0)
            return null;

        if (group.Shipping?.PickupInStore == true || group.SharedWithPackNames.Count is 0 or > MaxSharedPackNames)
            return delivery;

        var sharedWith = string.Format(await _localizationService.GetResourceAsync("Plugins.CW.Core.Order.SharedShippingWith"), string.Join(", ", group.SharedWithPackNames));

        return $"{delivery} ({sharedWith})";
    }

    /// <summary>
    /// How and where an order without packnames is delivered
    /// </summary>
    protected virtual async Task<string> GetPlainDeliveryAsync(Order order, int languageId)
    {
        var parts = new List<string> { order.ShippingMethod };

        //an order collected in store is delivered to no address
        if (!order.PickupInStore && order.ShippingAddressId.HasValue)
        {
            var address = await _addressService.GetAddressByIdAsync(order.ShippingAddressId.Value);

            if (address != null)
            {
                var (_, fields) = await _addressService.FormatAddressAsync(address, languageId);

                parts.Add($"{address.FirstName} {address.LastName}".Trim());
                parts.AddRange(AddressFields.Select(field => fields.FirstOrDefault(value => value.Key == field).Value));
            }
        }

        var delivery = string.Join(", ", parts.Where(part => !string.IsNullOrWhiteSpace(part)));

        return delivery.Length == 0 ? null : delivery;
    }

    /// <summary>
    /// What was ordered for whom, as the approver reviews it
    /// </summary>
    protected virtual async Task PrepareGroupsAsync(OrderApprovalReviewModel model, Order order, IList<OrderItem> items, int languageId)
    {
        var groups = await _cwOrderService.GetPackNameOrderGroupsAsync(order.Id);

        var colours = groups.Count == 0
            ? new Dictionary<int, string>()
            : (await _packNameService.GetPackNamesByIdsAsync(groups.Select(group => group.PackNameId).Distinct().ToArray()))
                .ToDictionary(packName => packName.Id, packName => packName.Colour);

        foreach (var group in groups)
        {
            var groupModel = new OrderApprovalReviewGroupModel
            {
                PackName = group.PackName,
                Colour = PackNameColourHelper.Normalize(colours.GetValueOrDefault(group.PackNameId)) ?? DefaultColour,
                Details = string.Join(" · ", new[] { group.Branch, group.Department, group.CostCenter }.Where(detail => !string.IsNullOrWhiteSpace(detail))),
                Delivery = await GetDeliveryAsync(group)
            };

            await PrepareLinesAsync(groupModel, [.. group.Items], order, languageId);

            model.Groups.Add(groupModel);
        }

        //the lines that are for no packname: all of them in an order without packnames
        var groupedItemIds = groups.SelectMany(group => group.Items).Select(item => item.Id).ToHashSet();
        var otherItems = items.Where(item => !groupedItemIds.Contains(item.Id)).ToList();

        if (otherItems.Count > 0)
        {
            var groupModel = new OrderApprovalReviewGroupModel
            {
                Delivery = groups.Count == 0 ? await GetPlainDeliveryAsync(order, languageId) : null
            };

            await PrepareLinesAsync(groupModel, otherItems, order, languageId);

            model.Groups.Add(groupModel);
        }

        model.ItemCount = items.Sum(item => item.Quantity);
    }

    /// <summary>
    /// The totals of the order, from what was ordered to what is left to pay; every row adds up to it
    /// </summary>
    protected virtual async Task PrepareTotalsAsync(OrderApprovalReviewModel model, Order order, IList<OrderItem> items, int languageId)
    {
        //the products listed, so the groups add up to it; nop's subtotal also has what a checkout attribute costs
        var products = items.Sum(item => item.PriceInclTax);

        //the order as subsidy and store credit settled it; an order neither paid for has no settlement row
        var settlement = await _cwOrderService.GetOrderSettlementAsync(order.Id);
        var subsidy = (settlement?.SubsidyStockAmount ?? decimal.Zero) + (settlement?.SubsidyShippingAmount ?? decimal.Zero);
        var storeCredit = settlement?.CreditNoteAmount ?? decimal.Zero;

        //discounts, gift cards, reward points, fees and checkout attributes: whatever else makes the total; a cent is rounding
        var other = order.OrderTotal - (products + order.OrderShippingInclTax - subsidy - storeCredit);

        model.Subtotal = await FormatAsync(order, products, languageId);
        model.Delivery = order.OrderShippingInclTax > decimal.Zero ? await FormatAsync(order, order.OrderShippingInclTax, languageId) : null;
        model.Other = Math.Abs(other) > 0.01M ? await FormatAsync(order, other, languageId) : null;
        model.Subsidy = subsidy > decimal.Zero ? await FormatAsync(order, -subsidy, languageId) : null;
        model.StoreCredit = storeCredit > decimal.Zero ? await FormatAsync(order, -storeCredit, languageId) : null;
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
        model.OrderNumber = order.CustomOrderNumber;
        model.CustomerName = customer == null ? string.Empty : await _customerService.GetCustomerFullNameAsync(customer);
        model.CreatedOn = (await _dateTimeHelper.ConvertToUserTimeAsync(order.CreatedOnUtc, DateTimeKind.Utc)).ToString("D");
        model.Status = await _localizationService.GetResourceAsync($"Plugins.CW.Core.OrderApproval.Status.{approval.Status}");
        model.StatusClass = approval.Status.ToString().ToLowerInvariant();
        model.DecidedBy = approval.DecidedBy;
        model.DecisionComment = approval.DecisionComment;
        model.CanDecide = approval.Status == OrderApprovalStatus.Pending && request.RevokedOnUtc == null;

        if (model.CanDecide)
        {
            var items = await _orderService.GetOrderItemsAsync(order.Id);

            await PrepareGroupsAsync(model, order, items, languageId);
            await PrepareTotalsAsync(model, order, items, languageId);
        }

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

    public virtual async Task<OrderApprovalStatusModel> PrepareStatusModelAsync(int orderId)
    {
        var approval = await _orderApprovalService.GetOrderApprovalByOrderIdAsync(orderId);

        switch (approval?.Status)
        {
            case OrderApprovalStatus.Pending:
                var approvers = await _orderApprovalService.GetWaitingApproverNamesAsync(approval.Id);

                return new OrderApprovalStatusModel
                {
                    StatusClass = "pending",
                    Message = approvers.Count == 0
                        ? await _localizationService.GetResourceAsync("Plugins.CW.Core.OrderApproval.Customer.Waiting")
                        : string.Format(await _localizationService.GetResourceAsync("Plugins.CW.Core.OrderApproval.Customer.WaitingBy"), string.Join(", ", approvers)),
                    ReservedMessage = await _cwOrderService.GetOrderSettlementAsync(orderId) == null
                        ? null
                        : await _localizationService.GetResourceAsync("Plugins.CW.Core.OrderApproval.Customer.Reserved")
                };

            case OrderApprovalStatus.Approved:
                return new OrderApprovalStatusModel
                {
                    StatusClass = "approved",
                    Message = string.Format(await _localizationService.GetResourceAsync("Plugins.CW.Core.OrderApproval.Customer.Approved"), approval.DecidedBy)
                };

            case OrderApprovalStatus.Declined:
                return new OrderApprovalStatusModel
                {
                    StatusClass = "declined",
                    Message = string.Format(await _localizationService.GetResourceAsync("Plugins.CW.Core.OrderApproval.Customer.Declined"), approval.DecidedBy),
                    Reason = approval.DecisionComment
                };

            case OrderApprovalStatus.Expired:
                return new OrderApprovalStatusModel
                {
                    StatusClass = "expired",
                    Message = await _localizationService.GetResourceAsync("Plugins.CW.Core.OrderApproval.Customer.Expired"),
                    Reason = approval.DecisionComment
                };

            default:
                return null;
        }
    }

    #endregion
}
