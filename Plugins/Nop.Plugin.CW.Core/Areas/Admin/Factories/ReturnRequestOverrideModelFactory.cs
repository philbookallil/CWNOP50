using Nop.Core.Domain.Orders;
using Nop.Services.Catalog;
using Nop.Services.Customers;
using Nop.Services.Helpers;
using Nop.Services.Localization;
using Nop.Services.Media;
using Nop.Services.Orders;
using Nop.Web.Areas.Admin.Factories;
using Nop.Web.Areas.Admin.Models.Orders;
using Nop.Web.Framework.Factories;

namespace Nop.Plugin.CW.Core.Areas.Admin.Factories;

/// <summary>
/// The admin return request model of one return, for the exchange configuration screens
/// </summary>
/// <remarks>
/// It no longer overrides nop's list: the override read a first / last name filter that no screen posts and
/// otherwise did what nop's own does, so the Return requests list is nop's.
/// </remarks>
public class ReturnRequestOverrideModelFactory : ReturnRequestModelFactory, IReturnRequestOverrideModelFactory
{
    #region Ctor

    public ReturnRequestOverrideModelFactory(IBaseAdminModelFactory baseAdminModelFactory,
        IDateTimeHelper dateTimeHelper,
        IDownloadService downloadService,
        ICustomerService customerService,
        ILocalizationService localizationService,
        ILocalizedModelFactory localizedModelFactory,
        IOrderService orderService,
        IProductService productService,
        IReturnRequestService returnRequestService) : base(baseAdminModelFactory,
            dateTimeHelper,
            downloadService,
            customerService,
            localizationService,
            localizedModelFactory,
            orderService,
            productService,
            returnRequestService)
    {
    }

    #endregion

    #region Methods

    public async Task<ReturnRequestModel> PrepareReturnRequestModelAsync(ReturnRequest returnRequest, bool excludeProperties)
    {
        var model = new ReturnRequestModel();

        ArgumentNullException.ThrowIfNull(returnRequest);

        var orderItem = await _orderService.GetOrderItemByIdAsync(returnRequest.OrderItemId);

        if (orderItem != null)
        {
            var product = await _productService.GetProductByIdAsync(orderItem.ProductId);
            model.ProductId = product.Id;
            model.ProductName = product.Name;
            model.OrderId = orderItem.OrderId;
            model.AttributeInfo = orderItem.AttributeDescription;
            var order = await _orderService.GetOrderByIdAsync(orderItem.OrderId);
            model.CustomOrderNumber = order.CustomOrderNumber;
        }
        model.Id = returnRequest.Id;
        model.CustomNumber = returnRequest.CustomNumber;
        model.CustomerId = returnRequest.CustomerId;
        var customer = await _customerService.GetCustomerByIdAsync(returnRequest.CustomerId);

        model.CustomerInfo = await _customerService.IsRegisteredAsync(customer)
            ? customer.Email : await _localizationService.GetResourceAsync("Admin.Customers.Guest");

        model.Quantity = returnRequest.Quantity;
        model.ReturnRequestStatusStr = await _localizationService.GetLocalizedEnumAsync(returnRequest.ReturnRequestStatus);

        var download = await _downloadService.GetDownloadByIdAsync(returnRequest.UploadedFileId);
        model.UploadedFileGuid = download != null ? download.DownloadGuid : Guid.Empty;
        model.CreatedOn = await _dateTimeHelper.ConvertToUserTimeAsync(returnRequest.CreatedOnUtc, DateTimeKind.Utc);
        if (!excludeProperties)
        {
            model.ReasonForReturn = returnRequest.ReasonForReturn;
            model.RequestedAction = returnRequest.RequestedAction;
            model.CustomerComments = returnRequest.CustomerComments;
            model.StaffNotes = returnRequest.StaffNotes;
            model.ReturnRequestStatusId = returnRequest.ReturnRequestStatusId;
        }

        return model;
    }

    #endregion
}