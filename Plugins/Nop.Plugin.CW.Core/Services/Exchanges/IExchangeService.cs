using Nop.Core;
using Nop.Core.Domain.Catalog;
using Nop.Core.Domain.Orders;
using Nop.Plugin.CW.Core.Domain.Exchanges;
using Nop.Plugin.CW.Core.Domain.Exchanges.DTOs;

namespace Nop.Plugin.CW.Core.Services.Exchanges
{
    public interface IExchangeService
    {
        #region Reason Rules

        /// <summary>
        /// Retrieves all Return Reason Rules for a given store.
        /// If a return reason rule is not defined for an existing return reason entity, it will be returned in the list with default values.
        /// </summary>
        /// <param name="storeId">Store Identifier</param>
        /// <param name="pageIndex">Page index</param>
        /// <param name="pageSize">Page size</param>
        /// <returns></returns>
        Task<IPagedList<ReturnReasonRuleDTO>> GetReturnReasonRulesAsync(int storeId, int pageIndex = 0, int pageSize = int.MaxValue);

        /// <summary>
        /// Determines whether a return reason rule exists for the specified return reason and store.
        /// </summary>
        /// <param name="returnReasonId">The ID of the return reason to check.</param>
        /// <param name="storeId">The ID of the store associated with the return reason rule.</param>
        /// <returns>A task representing the asynchronous operation. The task result contains <c>true</c> if a matching return reason rule exists; otherwise, <c>false</c>.</returns>
        Task<bool> ReturnReasonRuleExistByReasonIdAsync(int returnReasonId, int storeId);

        /// <summary>
        /// Retrieves the Return Reason Rule for the specified return reason and store.
        /// </summary>
        /// <param name="returnReasonId">The ID of the return reason to retrieve the rule for.</param>
        /// <param name="storeId">The ID of the store associated with the return reason rule.</param>
        /// <returns>A task representing the asynchronous operation. The task result contains the <see cref="ReturnReasonRule"/> entity if found; otherwise, <c>null</c>.</returns>
        Task<ReturnReasonRule> GetReturnReasonRuleByReasonIdAsync(int returnReasonId, int storeId);

        Task InsertReturnReasonRuleAsync(ReturnReasonRule rule);

        Task UpdateReturnReasonRuleAsync(ReturnReasonRule rule);

        #endregion

        #region Action Rules

        /// <summary>
        /// Retrieves all Return Action Rules for a given store.
        /// If a return action rule is not defined for an existing return action entity, it will be returned in the list with default values.
        /// </summary>
        /// <param name="storeId">Store Identifier</param>
        /// <param name="pageIndex">Page index</param>
        /// <param name="pageSize">Page size</param>
        /// <returns></returns>
        Task<IPagedList<ReturnActionRuleDTO>> GetReturnActionRulesAsync(int storeId, int pageIndex = 0, int pageSize = int.MaxValue);

        /// <summary>
        /// Determines whether a return action rule exists for the specified return action and store.
        /// </summary>
        /// <param name="returnActionId">The ID of the return action to check.</param>
        /// <param name="storeId">The ID of the store associated with the return action rule.</param>
        /// <returns>A task representing the asynchronous operation. The task result contains <c>true</c> if a matching return action rule exists; otherwise, <c>false</c>.</returns>
        Task<bool> ReturnActionRuleExistByActionIdAsync(int returnActionId, int storeId);

        /// <summary>
        /// Retrieves the Return Action Rule for the specified return action and store.
        /// </summary>
        /// <param name="returnActionId">The ID of the return action to retrieve the rule for.</param>
        /// <param name="storeId">The ID of the store associated with the return action rule.</param>
        /// <returns>A task representing the asynchronous operation. The task result contains the <see cref="ReturnActionRule"/> entity if found; otherwise, <c>null</c>.</returns>
        Task<ReturnActionRule> GetReturnActionRuleByActionIdAsync(int returnActionId, int storeId);

        Task InsertReturnActionRuleAsync(ReturnActionRule rule);

        Task UpdateReturnActionRuleAsync(ReturnActionRule rule);

        #endregion

        #region Return Status Rules

        Task<IList<ReturnStatusRule>> GetReturnStatusRulesAsync(int storeId);

        Task<ReturnStatusRule> GetRuleByStatusIdAsync(int returnStatusId, int storeId);

        /// <summary>
        /// Determines whether a return status rule exists for the specified return status and store.
        /// </summary>
        /// <param name="returnStatusId">The ID of the return status to check.</param>
        /// <param name="storeId">The ID of the store associated with the return status rule.</param>
        /// <returns>A task representing the asynchronous operation. The task result contains <c>true</c> if a matching return status rule exists; otherwise, <c>false</c>.</returns>
        Task<bool> ReturnStatusRuleExistByStatusIdAsync(int returnStatusId, int storeId);

        IList<ReturnRequestStatus> GetAllReturnStatuses();

        Task InsertReturnStatusRuleAsync(ReturnStatusRule rule);

        Task UpdateReturnStatusRuleAsync(ReturnStatusRule rule);

        #endregion

        #region Fee Rules

        Task<IList<FeeRule>> GetFeeRulesAsync(int storeId);

        Task<FeeRule> GetFeeRuleByIdAsync(int ruleId);

        Task<FeeRule> GetRuleByFeeTypeAsync(int feeTypeId, int storeId, bool fallbackToDefault = false);

        Task InsertFeeRuleAsync(FeeRule rule);

        Task UpdateFeeRuleAsync(FeeRule rule);

        Task DeleteFeeRuleAsync(FeeRule rule);

        #endregion

        #region Return Request Items

        Task RegisterReturnRequestItemAsync(ReturnRequest returnRequest, ReturnRequestItem item, ReturnReplacementItem replacementItem = null);

        /// <summary>
        /// Gets the replacement product chosen for a return request item.
        /// </summary>
        Task<ReturnReplacementItem> GetReplacementItemAsync(int returnRequestItemId);

        Task UpdateReplacementItemAsync(ReturnReplacementItem replacementItem);

        Task<ReturnRequestItem> GetReturnRequestItemAsync(int returnRequestId);

        Task<IList<FeeRule>> GetFeeRulesForActionAndReasonAsync(int actionId, int reasonId, int storeId);

        Task UpdateReturnRequestItemAsync(ReturnRequestItem item);

        Task<ReturnRequestReason> GetSelectedReasonAsync(ReturnRequestItem returnRequestItem);

        Task<ReturnRequestAction> GetSelectedActionAsync(ReturnRequestItem returnRequestItem);

        Task AssignCartItemToReturnRequestItemAsync(ShoppingCartItem cartItem, ReturnRequestItem returnRequestItem);

        Task SetExchangeRequestCompletedAsync(ReturnRequestItem returnRequestItem);

        Task SetExchangeRequestNeedsCompletionAsync(ReturnRequestItem returnRequestItem);

        Task<ReturnRequestItem> GetReturnRequestItemForCartItemAsync(int cartItemId);

        /// <summary>
        /// Gets the return request items linked to a customer's cart lines, keyed by shopping cart item identifier
        /// </summary>
        /// <remarks>
        /// Read once per request and kept for it; every write of a return request item in this service drops it.
        /// The rows are for reading: a caller that changes one writes it through this service.
        /// </remarks>
        /// <param name="customerId">Customer identifier</param>
        /// <returns>
        /// A task that represents the asynchronous operation
        /// The task result contains the return request items by shopping cart item identifier
        /// </returns>
        Task<IReadOnlyDictionary<int, ReturnRequestItem>> GetCartReturnRequestItemsAsync(int customerId);

        Task<IList<ReturnRequestItem>> GetReturnRequestItemsForCartItemsAsync(int[] cartItemsIds);

        Task<IList<ReturnRequestItem>> GetReturnRequestItemsForOrderItemsAsync(int[] orderItemsIds);

        /// <summary>
        /// Gets the exchange and return flags of the given orders, in two queries whatever their number.
        /// </summary>
        /// <param name="orderIds">Order identifiers</param>
        /// <returns>One entry per order that has a flag; orders with none are left out</returns>
        Task<IList<OrderReturnFlags>> GetOrderReturnFlagsAsync(int[] orderIds);

        #endregion

        #region Reason and Action linking

        /// <summary>
        /// Synchronizes the relationship between a reason and a collection of actions for a specific store.
        /// </summary>
        /// <param name="reasonId">The ID of the reason to link to actions</param>
        /// <param name="storeId">The ID of the store context for the relationship</param>
        /// <param name="actionsIds">The collection of action IDs that should be associated with the reason</param>
        /// <returns>A task representing the asynchronous operation</returns>
        Task LinkReasonToActionsAsync(int reasonId, int storeId, IList<int> actionsIds);

        /// <summary>
        /// Retrieves all return request action mappings associated with a specific return reason and store.
        /// </summary>
        /// <param name="returnReasonId">The ID of the return reason to filter actions by.</param>
        /// <param name="storeId">The ID of the store to filter actions by.</param>
        /// <returns>A list of <see cref="ReasonActionRelationship"/> objects that match the specified reason and store criteria.</returns>
        Task<IList<ReasonActionRelationship>> GetActionMappingsByReasonIdAsync(int returnReasonId, int storeId);

        /// <summary>
        /// Retrieves a list of active return request actions associated with a specific return reason for a given store.
        /// </summary>
        /// <param name="returnReasonId">The ID of the return reason to filter by.</param>
        /// <param name="storeId">The ID of the store to filter by.</param>
        /// <returns>An asynchronous task that returns a list of active <see cref="ReturnRequestAction"/> objects.</returns>
        Task<IList<ReturnRequestAction>> GetActiveActionsByReasonIdAsync(int reasonId, int storeId);

        /// <summary>
        /// Gets the identifiers of the return reasons a store offers: those with an active reason rule for the store
        /// </summary>
        /// <param name="storeId">Store identifier</param>
        /// <returns>A task that represents the asynchronous operation; the task result contains the reason identifiers</returns>
        Task<IList<int>> GetActiveReasonIdsAsync(int storeId);

        #endregion

        #region Return Requests

        Task<Order> GetOrderForReturnRequestAsync(ReturnRequest returnRequest);

        Task<Product> GetReturnedProductAsync(ReturnRequest returnRequest);

        Task<IPagedList<ReturnRequest>> GetReturnsForOrderAsync(Order order, int pageIndex = 0,
        int pageSize = int.MaxValue);

        IList<ReturnRequest> GetReturnRequestsById(int[] ids);

        /// <summary>
        /// Gets a customer's own return requests among the given identifiers
        /// </summary>
        /// <param name="customerId">Customer identifier</param>
        /// <param name="ids">Return request identifiers</param>
        /// <returns>The customer's return requests among them, in identifier order; the others are left out</returns>
        Task<IList<ReturnRequest>> GetCustomerReturnRequestsByIdsAsync(int customerId, int[] ids);

        /// <summary>
        /// Gets how many units of each of an order's items the customer may still return
        /// </summary>
        /// <param name="order">Order</param>
        /// <returns>Order item id -> units the customer may return; an item that cannot be returned is left out</returns>
        Task<IDictionary<int, int>> GetReturnableQuantitiesAsync(Order order);

        Task<IReadOnlyDictionary<int, int>> CountReturnedUnitsAsync(int[] orderItemIds);

        bool IsReturnRequestAlreadyPerformed(int returnedOrderItemId, Guid returnRequestGroupId, out int returnRequestId);

        #endregion

        #region Exchange Fees Amounts

        Task<decimal> CalculateExchangeFeesAmountAsync(ReturnRequestItem returnRequestItem);

        Task<IDictionary<FeeType, decimal>> GetExchangeFeesAmountsAsync(ReturnRequestItem returnRequestItem);

        Task<IDictionary<FeeType, decimal>> GetExchangeFeesAmountsAsync(IEnumerable<ShoppingCartItem> cart, IList<AdditionalFeeItem> additionalFeeItems = null);

        Task<IDictionary<FeeType, decimal>> GetExchangeFeesAmountsAsync(Order order, IList<AdditionalFeeItem> additionalFeeItems = null);

        /// <summary>
        /// Prices a whole return case from the live fee rules — the single place that knows how an
        /// exchange fee is calculated.
        /// </summary>
        /// <remarks>
        /// Every line is priced on its own and nothing is summed across the case — a Per Units fee bands
        /// over one return request's quantity, which is what lets a line be executed the moment it is
        /// authorised. Call this only when pricing for the first time; readers want
        /// <see cref="GetExchangeFeeAmountsForCaseAsync"/>, which returns the frozen charges once the
        /// case has resolved.
        /// </remarks>
        /// <param name="returnCaseId">The case to price</param>
        Task<IList<ExchangeFeeAmount>> CalculateExchangeFeeAmountsForCaseAsync(int returnCaseId);

        /// <summary>
        /// Prices one returned item on its own, for a return raised before the case model existed and so
        /// having no case line to be priced with.
        /// </summary>
        Task<IList<ExchangeFeeAmount>> CalculateExchangeFeeAmountsForItemAsync(ReturnRequestItem returnRequestItem, ReturnRequest returnRequest = null);

        /// <summary>
        /// A case's fees as every reader should see them: the charges frozen when it was resolved, or
        /// live pricing while it is not.
        /// </summary>
        Task<IList<ExchangeFeeAmount>> GetExchangeFeeAmountsForCaseAsync(int returnCaseId);

        #endregion

        #region Exchange Ordering

        Task<IList<ShoppingCartItem>> BuildProductsListForReturnOrderAsync(ReturnRequest returnRequest, ReturnRequestItem returnRequestItem,
            bool useReturnedItems = false);

        Task<IList<ExchangeReturnMode>> DetermineEnabledReturnModesAsync(int storeId, int returnReasonId, int returnActionId);

        #endregion
    }
}
