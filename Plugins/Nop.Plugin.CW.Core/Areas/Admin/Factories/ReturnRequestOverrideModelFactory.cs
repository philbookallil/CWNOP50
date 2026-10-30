using Nop.Core.Domain.Orders;
using Nop.Plugin.CW.Core.Areas.Admin.Models.Exchanges;
using Nop.Plugin.CW.Core.Services.Orders;
using Nop.Services.Catalog;
using Nop.Services.Common;
using Nop.Services.Customers;
using Nop.Services.Helpers;
using Nop.Services.Localization;
using Nop.Services.Media;
using Nop.Services.Orders;
using Nop.Web.Areas.Admin.Factories;
using Nop.Web.Areas.Admin.Models.Orders;
using Nop.Web.Framework.Factories;
using Nop.Web.Framework.Models.Extensions;

namespace Nop.Plugin.CW.Core.Areas.Admin.Factories;

/// <summary>
/// Override class for the admin area ReturnRequestModelFactory
/// </summary>
public class ReturnRequestOverrideModelFactory : ReturnRequestModelFactory, IReturnRequestOverrideModelFactory
{
    #region Fields

    protected readonly IGenericAttributeService _genericAttributeService;
    protected readonly ICWReturnRequestService _cwReturnRequestService;

    #endregion

    #region Ctor

    public ReturnRequestOverrideModelFactory(IGenericAttributeService genericAttributeService,
        ICWReturnRequestService cwReturnRequestService,
        IBaseAdminModelFactory baseAdminModelFactory,
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
        _genericAttributeService = genericAttributeService;
        _cwReturnRequestService = cwReturnRequestService;
    }

    #endregion

    #region Overrides

    public override async Task<ReturnRequestListModel> PrepareReturnRequestListModelAsync(ReturnRequestSearchModel searchModel)
    {
        ArgumentNullException.ThrowIfNull(searchModel);

        //get parameters to filter emails
        var startDateValue = !searchModel.StartDate.HasValue ? null
            : (DateTime?)_dateTimeHelper.ConvertToUtcTime(searchModel.StartDate.Value, await _dateTimeHelper.GetCurrentTimeZoneAsync());
        var endDateValue = !searchModel.EndDate.HasValue ? null
            : (DateTime?)_dateTimeHelper.ConvertToUtcTime(searchModel.EndDate.Value, await _dateTimeHelper.GetCurrentTimeZoneAsync()).AddDays(1);
        var returnRequestStatus = searchModel.ReturnRequestStatusId == -1 ? null : (ReturnRequestStatus?)searchModel.ReturnRequestStatusId;

        var firstName = string.Empty;
        var lastName = string.Empty;

        if (searchModel is CWReturnRequestSearchModel cwSearchModel)
        {
            firstName = cwSearchModel.FirstName;
            lastName = cwSearchModel.LastName;
        }

        //get return requests
        var returnRequests = await _cwReturnRequestService.SearchReturnRequestsAsync(customNumber: searchModel.CustomNumber,
            firstname: firstName,
            lastname: lastName,
            rs: returnRequestStatus,
            createdFromUtc: startDateValue,
            createdToUtc: endDateValue,
            pageIndex: searchModel.Page - 1, pageSize: searchModel.PageSize);



        //prepare list model
        var model = await new ReturnRequestListModel().PrepareToGridAsync(searchModel, returnRequests, () =>
        {
            return returnRequests.SelectAwait(async returnRequest => await
            PrepareReturnRequestCWModelAsync(returnRequest));
        });


        return model;
    }

    public virtual async Task<ReturnRequestModel> PrepareReturnRequestCWModelAsync(ReturnRequest returnRequest, bool excludeProperties = false)
    {
        //fill in model values from the entity
        /*    var returnRequestModel = new ReturnRequestModel
            {
                Id = returnRequest.Id,
                CustomNumber = returnRequest.CustomNumber,
                CustomerId = returnRequest.CustomerId,
                Quantity = returnRequest.Quantity
            };*/

        var model = await PrepareReturnRequestModelAsync(null, returnRequest);

        #region CW Code

        var customer = await _customerService.GetCustomerByIdAsync(returnRequest.CustomerId);

        var cwModel = new CWReturnRequestModel(model)
        {
            CustomerName = await _customerService.GetCustomerFullNameAsync(customer) //returnRequest.Customer.FullName(_genericAttributeService);
        };

        #endregion
        return cwModel;
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
