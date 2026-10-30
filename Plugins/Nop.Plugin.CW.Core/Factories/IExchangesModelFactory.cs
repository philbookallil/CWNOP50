using Nop.Core.Domain.Catalog;
using Nop.Core.Domain.Customers;
using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Stores;
using Nop.Plugin.CW.Core.Models.Exchanges;
using Nop.Web.Models.Catalog;
using Nop.Web.Models.Order;

namespace Nop.Plugin.CW.Core.Factories;

public interface IExchangesModelFactory
{
    Task<ExchangesFeesModel> PrepareExchangesFeesModelFromCartAsync(Customer customer, Store store);

    Task<ExchangesFeesModel> PrepareExchangesFeesModelFromOrderAsync(Order order);

    /// <summary>
    /// Assembles the gather step model. Quantities, group id and case id are resolved by the caller,
    /// so this works both for a first visit and for redisplaying the step after a validation failure.
    /// </summary>
    /// <param name="order">The order being returned against</param>
    /// <param name="returnRequestModel">Reason, action, comments and uploaded file</param>
    /// <param name="submission">Submission data</param>
    /// <param name="returnRequestGroupId">Identifies this attempt; preserved across a retry</param>
    /// <param name="returnCaseId">The draft return case, already ensured by the caller</param>
    Task<ReturnRequestGatherInfoModel> PrepareGatherInfoModelAsync(
        Order order,
        SubmitReturnRequestModel returnRequestModel,
        ReturnWizardSubmission submission,
        Guid returnRequestGroupId,
        int returnCaseId);

    Task<ReturnRequestGatherInfoModel.ReturnedOrderItemModel> BuildReturnedItemModelAsync(Order order, OrderItem returnedOrderItem, int returnedQuantity, int? exchangeTypeId = null);

    /// <summary>
    /// Prepares a page of candidate replacement products for the picker
    /// </summary>
    Task<ReplacementProductListModel> PrepareReplacementProductListModelAsync(int storeId, int orderItemId,
        int excludeProductId, string keywords, int pageIndex, int pageSize);

    /// <summary>
    /// Prepares one product's options for stage 2 of the replacement picker
    /// </summary>
    Task<ReplacementProductOptionsModel> PrepareReplacementProductOptionsModelAsync(Product product, int orderItemId);

    /// <summary>
    /// Marks the attribute values the customer already has as pre-selected, so the picker opens
    /// showing their current combination rather than an empty form.
    /// </summary>
    Task PreselectCurrentAttributesAsync(ProductDetailsModel product, int? returnRequestId, int orderItemId);

    Task PrepareSummaryFeesAsync(ReturnRequestSummaryModel model);

    /// <summary>
    /// Rebuilds the gather model from a submission. The returned items are not model-bound, so every
    /// action that receives a wizard post has to reconstruct them.
    /// </summary>
    /// <param name="gatheredInfo">The model-bound hidden fields carried through the wizard</param>
    /// <param name="submission">The parsed post</param>
    /// <returns>The rebuilt model, or null when the order no longer exists</returns>
    Task<ReturnRequestGatherInfoModel> RebuildGatheredInfoAsync(ReturnRequestGatherInfoModel gatheredInfo, ReturnWizardSubmission submission);
}