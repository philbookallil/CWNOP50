using Nop.Core;
using Nop.Core.Caching;
using Nop.Core.Domain.Catalog;
using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Shipping;
using Nop.Data;
using Nop.Plugin.CW.Core.Domain.Exchanges;
using Nop.Plugin.CW.Core.Domain.Exchanges.DTOs;
using Nop.Plugin.CW.Core.Domain.Returns;
using Nop.Plugin.CW.Core.Helpers;
using Nop.Plugin.CW.Core.Services.Orders;
using Nop.Plugin.CW.Core.Services.Returns;
using Nop.Services.Catalog;
using Nop.Services.Orders;

namespace Nop.Plugin.CW.Core.Services.Exchanges;

public class ExchangeService : IExchangeService
{
    #region Fields

    protected readonly IRepository<ReturnReasonRule> _returnReasonRuleRepository;
    protected readonly IRepository<ReturnActionRule> _returnActionRuleRepository;
    protected readonly IRepository<ReturnStatusRule> _returnStatusRuleRepository;
    protected readonly IRepository<FeeRule> _feeRuleRepository;
    protected readonly IRepository<ReturnRequestItem> _returnRequestItemRepository;
    protected readonly IReturnRequestService _returnRequestService;
    protected readonly IRepository<ReasonActionRelationship> _reasonActionRelationshipRepository;
    protected readonly IRepository<ReturnRequest> _returnRequestRepository;
    protected readonly IProductService _productService;
    protected readonly IOrderService _orderService;
    protected readonly IRepository<ReturnRequestAction> _returnRequestActionRepository;
    protected readonly IRepository<ReturnRequestReason> _returnRequestReasonRepository;
    protected readonly IRepository<ReturnReplacementItem> _returnReplacementItemRepository;
    protected readonly IReturnCaseService _returnCaseService;
    protected readonly IRepository<OrderItem> _orderItemRepository;
    protected readonly IRepository<ShoppingCartItem> _shoppingCartItemRepository;
    protected readonly IShortTermCacheManager _shortTermCacheManager;
    protected readonly IRepository<Shipment> _shipmentRepository;
    protected readonly IRepository<ShipmentItem> _shipmentItemRepository;

    #endregion

    #region Ctor

    public ExchangeService(IRepository<ReturnReasonRule> returnReasonRuleRepository,
        IRepository<ReturnActionRule> returnActionRuleRepository,
        IRepository<ReturnStatusRule> returnStatusRuleRepository,
        IRepository<FeeRule> feeRuleRepository,
        IRepository<ReturnRequestItem> returnRequestItemRepository,
        IReturnRequestService returnRequestService,
        IRepository<ReasonActionRelationship> reasonActionRelationshipRepository,
        IRepository<ReturnRequest> returnRequestRepository,
        IProductService productService,
        IOrderService orderService,
        IRepository<ReturnRequestAction> returnRequestActionRepository,
        IRepository<ReturnRequestReason> returnRequestReasonRepository,
        IRepository<ReturnReplacementItem> returnReplacementItemRepository,
        IReturnCaseService returnCaseService,
        IRepository<OrderItem> orderItemRepository,
        IRepository<ShoppingCartItem> shoppingCartItemRepository,
        IShortTermCacheManager shortTermCacheManager,
        IRepository<Shipment> shipmentRepository,
        IRepository<ShipmentItem> shipmentItemRepository)
    {
        _returnReasonRuleRepository = returnReasonRuleRepository;
        _returnActionRuleRepository = returnActionRuleRepository;
        _returnStatusRuleRepository = returnStatusRuleRepository;
        _feeRuleRepository = feeRuleRepository;
        _returnRequestItemRepository = returnRequestItemRepository;
        _returnRequestService = returnRequestService;
        _reasonActionRelationshipRepository = reasonActionRelationshipRepository;
        _returnRequestRepository = returnRequestRepository;
        _productService = productService;
        _orderService = orderService;
        _returnRequestActionRepository = returnRequestActionRepository;
        _returnRequestReasonRepository = returnRequestReasonRepository;
        _returnReplacementItemRepository = returnReplacementItemRepository;
        _returnCaseService = returnCaseService;
        _orderItemRepository = orderItemRepository;
        _shoppingCartItemRepository = shoppingCartItemRepository;
        _shortTermCacheManager = shortTermCacheManager;
        _shipmentRepository = shipmentRepository;
        _shipmentItemRepository = shipmentItemRepository;
    }

    #endregion

    #region Utilities

    /// <summary>
    /// Folds priced fees into a per-type breakdown, itemising ad-hoc staff fees by name.
    /// </summary>
    /// <remarks>
    /// The only place a fee record becomes a dictionary entry. Live pricing and frozen charges both
    /// arrive here as the same records, so the two cannot drift in how they aggregate.
    /// </remarks>
    protected static void AddFeeAmountsToTarget(IDictionary<FeeType, decimal> target,
        IEnumerable<ExchangeFeeAmount> fees,
        IList<AdditionalFeeItem> additionalFeeItems = null)
    {
        foreach (var fee in fees)
        {
            //TryGetValue rather than GetValueOrDefault: target is IDictionary<,>, which does not
            //implement IReadOnlyDictionary<,>, so that extension is not in scope here.
            target.TryGetValue(fee.FeeType, out var current);
            target[fee.FeeType] = current + fee.Amount;

            if (fee.FeeType != FeeType.AdditionalFee || additionalFeeItems == null || string.IsNullOrEmpty(fee.Description))
                continue;

            var existing = additionalFeeItems.FirstOrDefault(x => x.FeeName.Trim().Equals(fee.Description.Trim(), StringComparison.OrdinalIgnoreCase));

            if (existing == null)
                additionalFeeItems.Add(new AdditionalFeeItem { FeeName = fee.Description, FeeAmount = fee.Amount });
            else
                existing.FeeAmount += fee.Amount;
        }
    }

    /// <summary>
    /// A frozen charge read back as the same record live pricing produces.
    /// </summary>
    protected static ExchangeFeeAmount ToFeeAmount(ReturnCharge charge) => new()
    {
        ReturnCaseLineId = charge.ReturnCaseLineId,
        FeeType = charge.FeeType,
        Amount = charge.Amount,
        Quantity = charge.Quantity,
        Description = charge.Description,
        SourceFeeRuleId = charge.SourceFeeRuleId
    };

    /// <summary>
    /// Adds the fees collected with a set of returned items to a breakdown.
    /// </summary>
    /// <remarks>
    /// Each case is priced once per walk and its lines are then read from that one result — a cache, not
    /// a rule. Every fee belongs to exactly one line, so a case with two lines in the same cart
    /// contributes each line's fees once and nothing has to remember that it did.
    /// </remarks>
    protected async Task AddReturnedItemsFeesToTargetAsync(IDictionary<FeeType, decimal> target,
        IList<ReturnRequestItem> returnedItems,
        IList<AdditionalFeeItem> additionalFeeItems = null)
    {
        if (returnedItems == null)
            return;

        var pricedCases = new Dictionary<int, IList<ExchangeFeeAmount>>();

        foreach (var returnRequestItem in returnedItems)
        {
            var caseLine = await _returnCaseService.GetReturnCaseLineByReturnRequestIdAsync(returnRequestItem.ReturnRequestId);

            if (caseLine == null)
            {
                //raised before the case model existed: priced on its own, exactly as it always was
                AddFeeAmountsToTarget(target, await CalculateExchangeFeeAmountsForItemAsync(returnRequestItem), additionalFeeItems);
                continue;
            }

            if (!pricedCases.TryGetValue(caseLine.ReturnCaseId, out var caseFees))
            {
                caseFees = await GetExchangeFeeAmountsForCaseAsync(caseLine.ReturnCaseId);
                pricedCases[caseLine.ReturnCaseId] = caseFees;
            }

            //only this line's fees. Every fee belongs to exactly one line, so a case with two lines in the
            //same cart contributes each line's own and nothing has to remember what it already counted
            AddFeeAmountsToTarget(target, caseFees.Where(fee => fee.ReturnCaseLineId == caseLine.Id), additionalFeeItems);
        }
    }

    protected bool IsDynamicAmountFee(FeeType feeType)
    {
        var dynamicFees = new List<FeeType> { FeeType.RepackagingFee, FeeType.ReShippingFee };
        return dynamicFees.Contains(feeType);
    }

    protected virtual async Task<IList<ExchangeFeeAmount>> GetExchangeFeeAmountsForItemAsync(ReturnRequestItem returnRequestItem)
    {
        if (returnRequestItem == null)
            return new List<ExchangeFeeAmount>();

        var caseLine = await _returnCaseService.GetReturnCaseLineByReturnRequestIdAsync(returnRequestItem.ReturnRequestId);

        if (caseLine == null)
            return await CalculateExchangeFeeAmountsForItemAsync(returnRequestItem);

        var caseFees = await GetExchangeFeeAmountsForCaseAsync(caseLine.ReturnCaseId);

        return caseFees.Where(fee => fee.ReturnCaseLineId == caseLine.Id).ToList();
    }

    #endregion

    #region Reason Rules

    /// <summary>
    /// Retrieves all Return Reason Rules for a given store.
    /// If a return reason rule is not defined for an existing return reason entity, it will be returned in the list with default values.
    /// </summary>
    /// <param name="storeId">Store Identifier</param>
    /// <param name="pageIndex">Page index</param>
    /// <param name="pageSize">Page size</param>
    /// <returns></returns>
    public async Task<IPagedList<ReturnReasonRuleDTO>> GetReturnReasonRulesAsync(int storeId, int pageIndex = 0, int pageSize = int.MaxValue)
    {
        var query = from rr in _returnRequestReasonRepository.Table
                    join rrr in _returnReasonRuleRepository.Table.Where(r => r.StoreId == storeId) on rr.Id equals rrr.ReturnReasonId into ruleGroup
                    from rrr in ruleGroup.DefaultIfEmpty()
                    orderby rr.DisplayOrder
                    select new ReturnReasonRuleDTO
                    {
                        Id = rrr != null ? rrr.Id : 0,
                        ReturnReasonId = rr.Id,
                        ReturnReason = rr.Name,
                        DisplayOrder = rr.DisplayOrder,
                        StoreId = storeId,
                        AllowStoreCredit = rrr != null && rrr.AllowStoreCredit,
                        AllowExchange = rrr != null && rrr.AllowExchange,
                        AllowRepackagingFee = rrr != null && rrr.AllowRepackagingFee,
                        AllowShippingFee = rrr != null && rrr.AllowShippingFee,
                        BackInStock = rrr != null && rrr.BackInStock,
                        IsDisabled = rrr == null || rrr.IsDisabled,
                    };

        var reasonRules = await query.ToPagedListAsync(pageIndex, pageSize);

        var actionRows = await (
            from rarRel in _reasonActionRelationshipRepository.Table
            join ra in _returnRequestActionRepository.Table on rarRel.ActionId equals ra.Id
            where rarRel.StoreId == storeId
                && reasonRules.Select(rr => rr.ReturnReasonId).Contains(rarRel.ReasonId)
            select new { rarRel.ReasonId, ra.Name }
        ).ToListAsync();

        var byReason = actionRows.GroupBy(x => x.ReasonId).ToDictionary(g => g.Key, g => g.ToList());

        foreach (var rr in reasonRules)
        {
            if (byReason.TryGetValue(rr.ReturnReasonId, out var actions))
            {
                rr.LinkedActionsCount = actions.Count;
                rr.LinkedActionsNames = string.Join(", ", actions.Select(a => a.Name));
            }
        }

        return reasonRules;
    }

    /// <summary>
    /// Determines whether a return reason rule exists for the specified return reason and store.
    /// </summary>
    /// <param name="returnReasonId">The ID of the return reason to check.</param>
    /// <param name="storeId">The ID of the store associated with the return reason rule.</param>
    /// <returns>A task representing the asynchronous operation. The task result contains <c>true</c> if a matching return reason rule exists; otherwise, <c>false</c>.</returns>
    public async Task<bool> ReturnReasonRuleExistByReasonIdAsync(int returnReasonId, int storeId)
    {
        return await _returnReasonRuleRepository.Table
            .Where(r => r.ReturnReasonId == returnReasonId && r.StoreId == storeId)
            .AnyAsync();
    }

    /// <summary>
    /// Retrieves the Return Reason Rule for the specified return reason and store.
    /// </summary>
    /// <param name="returnReasonId">The ID of the return reason to retrieve the rule for.</param>
    /// <param name="storeId">The ID of the store associated with the return reason rule.</param>
    /// <returns>A task representing the asynchronous operation. The task result contains the <see cref="ReturnReasonRule"/> entity if found; otherwise, <c>null</c>.</returns>
    public async Task<ReturnReasonRule> GetReturnReasonRuleByReasonIdAsync(int returnReasonId, int storeId)
    {
        return await _returnReasonRuleRepository.Table
            .Where(r => r.ReturnReasonId == returnReasonId && r.StoreId == storeId)
            .FirstOrDefaultAsync();
    }

    public async Task InsertReturnReasonRuleAsync(ReturnReasonRule rule)
    {
        await _returnReasonRuleRepository.InsertAsync(rule);
    }

    public async Task UpdateReturnReasonRuleAsync(ReturnReasonRule rule)
    {
        await _returnReasonRuleRepository.UpdateAsync(rule);
    }

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
    public async Task<IPagedList<ReturnActionRuleDTO>> GetReturnActionRulesAsync(int storeId, int pageIndex = 0, int pageSize = int.MaxValue)
    {
        var query = from ra in _returnRequestActionRepository.Table
                    join rar in _returnActionRuleRepository.Table.Where(r => r.StoreId == storeId) on ra.Id equals rar.ReturnActionId into ruleGroup
                    from rar in ruleGroup.DefaultIfEmpty()
                    orderby ra.DisplayOrder
                    select new ReturnActionRuleDTO
                    {
                        Id = rar != null ? rar.Id : 0,
                        ReturnActionId = ra.Id,
                        ReturnAction = ra.Name,
                        DisplayOrder = ra.DisplayOrder,
                        StoreId = storeId,
                        AllowStoreCredit = rar != null && rar.AllowStoreCredit,
                        AllowExchange = rar != null && rar.AllowExchange,
                        AllowRepackagingFee = rar != null && rar.AllowRepackagingFee,
                        AllowShippingFee = rar != null && rar.AllowShippingFee,
                        BackInStock = rar != null && rar.BackInStock,
                        IsDisabled = rar == null || rar.IsDisabled,
                    };

        return await query.ToPagedListAsync(pageIndex, pageSize);
    }

    /// <summary>
    /// Determines whether a return action rule exists for the specified return action and store.
    /// </summary>
    /// <param name="returnActionId">The ID of the return action to check.</param>
    /// <param name="storeId">The ID of the store associated with the return action rule.</param>
    /// <returns>A task representing the asynchronous operation. The task result contains <c>true</c> if a matching return action rule exists; otherwise, <c>false</c>.</returns>
    public async Task<bool> ReturnActionRuleExistByActionIdAsync(int returnActionId, int storeId)
    {
        return await _returnActionRuleRepository.Table
            .Where(r => r.ReturnActionId == returnActionId && r.StoreId == storeId)
            .AnyAsync();
    }

    /// <summary>
    /// Retrieves the Return Action Rule for the specified return action and store.
    /// </summary>
    /// <param name="returnActionId">The ID of the return action to retrieve the rule for.</param>
    /// <param name="storeId">The ID of the store associated with the return action rule.</param>
    /// <returns>A task representing the asynchronous operation. The task result contains the <see cref="ReturnActionRule"/> entity if found; otherwise, <c>null</c>.</returns>
    public async Task<ReturnActionRule> GetReturnActionRuleByActionIdAsync(int returnActionId, int storeId)
    {
        return await _returnActionRuleRepository.Table
            .Where(r => r.ReturnActionId == returnActionId && r.StoreId == storeId)
            .FirstOrDefaultAsync();
    }

    public async Task InsertReturnActionRuleAsync(ReturnActionRule rule)
    {
        await _returnActionRuleRepository.InsertAsync(rule);
    }

    public async Task UpdateReturnActionRuleAsync(ReturnActionRule rule)
    {
        await _returnActionRuleRepository.UpdateAsync(rule);
    }

    #endregion

    #region Return Status Rules

    public async Task<IList<ReturnStatusRule>> GetReturnStatusRulesAsync(int storeId)
    {
        return await _returnStatusRuleRepository.Table
            .Where(r => r.StoreId == storeId)
            .ToListAsync();
    }

    public async Task<ReturnStatusRule> GetRuleByStatusIdAsync(int returnStatusId, int storeId)
    {
        return await _returnStatusRuleRepository.Table
            .Where(r => r.ReturnStatusId == returnStatusId && r.StoreId == storeId)
            .FirstOrDefaultAsync();
    }

    /// <summary>
    /// Determines whether a return status rule exists for the specified return status and store.
    /// </summary>
    /// <param name="returnStatusId">The ID of the return status to check.</param>
    /// <param name="storeId">The ID of the store associated with the return status rule.</param>
    /// <returns>A task representing the asynchronous operation. The task result contains <c>true</c> if a matching return status rule exists; otherwise, <c>false</c>.</returns>
    public async Task<bool> ReturnStatusRuleExistByStatusIdAsync(int returnStatusId, int storeId)
    {
        return await _returnStatusRuleRepository.Table
            .Where(r => r.ReturnStatusId == returnStatusId && r.StoreId == storeId)
            .AnyAsync();
    }

    public IList<ReturnRequestStatus> GetAllReturnStatuses()
    {
        return Enum.GetValues(typeof(ReturnRequestStatus)).Cast<ReturnRequestStatus>().ToList();
    }

    public async Task InsertReturnStatusRuleAsync(ReturnStatusRule rule)
    {
        await _returnStatusRuleRepository.InsertAsync(rule);
    }

    public async Task UpdateReturnStatusRuleAsync(ReturnStatusRule rule)
    {
        await _returnStatusRuleRepository.UpdateAsync(rule);
    }

    #endregion

    #region Fee Rules

    public async Task<IList<FeeRule>> GetFeeRulesAsync(int storeId)
    {
        return await _feeRuleRepository.Table
            .Where(r => r.StoreId == storeId)
            .ToListAsync();
    }

    public async Task<FeeRule> GetFeeRuleByIdAsync(int ruleId)
    {
        var query = _feeRuleRepository.Table;
        query = query.Where(r => r.Id == ruleId);

        return await query.FirstOrDefaultAsync();
    }

    public async Task<FeeRule> GetRuleByFeeTypeAsync(int feeTypeId, int storeId, bool fallbackToDefault = false)
    {
        var query = _feeRuleRepository.Table;
        query = query.Where(r => r.FeeTypeId == feeTypeId && r.StoreId == storeId);
        var rule = await query.FirstOrDefaultAsync();
        if (rule == null && fallbackToDefault)
        {
            return await GetRuleByFeeTypeAsync(feeTypeId, 0);
        }
        return rule;
    }

    public async Task InsertFeeRuleAsync(FeeRule rule)
    {
        await _feeRuleRepository.InsertAsync(rule);
    }

    public async Task UpdateFeeRuleAsync(FeeRule rule)
    {
        await _feeRuleRepository.UpdateAsync(rule);
    }

    public async Task DeleteFeeRuleAsync(FeeRule rule)
    {
        await _feeRuleRepository.DeleteAsync(rule);
    }

    #endregion

    #region Return Request Items

    public async Task RegisterReturnRequestItemAsync(ReturnRequest returnRequest, ReturnRequestItem item, ReturnReplacementItem replacementItem = null)
    {
        ArgumentNullException.ThrowIfNull(returnRequest);
        ArgumentNullException.ThrowIfNull(item);

        item.ReturnRequestId = returnRequest.Id;

        await _returnRequestItemRepository.InsertAsync(item);
        _shortTermCacheManager.RemoveByPrefix(OrderServicesDefaults.CartReturnRequestItemsPrefix);
        _shortTermCacheManager.RemoveByPrefix(OrderServicesDefaults.PackNameCartSubTotalPrefix);

        if (replacementItem != null)
        {
            replacementItem.ReturnRequestItemId = item.Id;
            await _returnReplacementItemRepository.InsertAsync(replacementItem);
        }
    }

    /// <summary>
    /// Gets the replacement product chosen for a return request item.
    /// </summary>
    public virtual async Task<ReturnReplacementItem> GetReplacementItemAsync(int returnRequestItemId)
    {
        if (returnRequestItemId <= 0)
            return null;

        return await _returnReplacementItemRepository.Table
            .Where(r => r.ReturnRequestItemId == returnRequestItemId)
            .FirstOrDefaultAsync();
    }

    public virtual async Task UpdateReplacementItemAsync(ReturnReplacementItem replacementItem)
    {
        ArgumentNullException.ThrowIfNull(replacementItem);

        await _returnReplacementItemRepository.UpdateAsync(replacementItem);
    }

    public async Task<ReturnRequestItem> GetReturnRequestItemAsync(int returnRequestId)
    {
        var query = _returnRequestItemRepository.Table;
        return await query.Where(r => r.ReturnRequestId == returnRequestId).FirstOrDefaultAsync();
    }

    public async Task<IList<FeeRule>> GetFeeRulesForActionAndReasonAsync(int actionId, int reasonId, int storeId)
    {
        IList<FeeRule> feeRules = new List<FeeRule>();

        var actionRule = await GetReturnActionRuleByActionIdAsync(actionId, storeId);
        var reasonRule = await GetReturnReasonRuleByReasonIdAsync(reasonId, storeId);

        // No rule row configured for this store means "not allowed" — fail closed, don't throw.
        if (actionRule == null || reasonRule == null)
            return feeRules;

        var feesInRule = new List<FeeType>();

        if (actionRule.AllowShippingFee && reasonRule.AllowShippingFee)
        {
            feesInRule.Add(FeeType.ReShippingFee);
        }

        if (actionRule.AllowRepackagingFee && reasonRule.AllowRepackagingFee)
        {
            feesInRule.Add(FeeType.RepackagingFee);
        }

        foreach (var feeType in feesInRule)
        {
            var rule = await GetRuleByFeeTypeAsync((int)feeType, storeId, true);
            if (rule != null)
            {
                feeRules.Add(rule);
            }
        }

        return feeRules;
    }

    public async Task UpdateReturnRequestItemAsync(ReturnRequestItem replacementItem)
    {
        ArgumentNullException.ThrowIfNull(replacementItem);
        await _returnRequestItemRepository.UpdateAsync(replacementItem);
        _shortTermCacheManager.RemoveByPrefix(OrderServicesDefaults.CartReturnRequestItemsPrefix);
        _shortTermCacheManager.RemoveByPrefix(OrderServicesDefaults.PackNameCartSubTotalPrefix);
    }

    public async Task<ReturnRequestReason> GetSelectedReasonAsync(ReturnRequestItem returnRequestItem)
    {
        if (returnRequestItem is not { ReturnReasonId: > 0 })
            return null;

        return await _returnRequestService.GetReturnRequestReasonByIdAsync(returnRequestItem.ReturnReasonId);
    }

    public async Task<ReturnRequestAction> GetSelectedActionAsync(ReturnRequestItem returnRequestItem)
    {
        if (returnRequestItem is not { ReturnActionId: > 0 })
            return null;

        return await _returnRequestService.GetReturnRequestActionByIdAsync(returnRequestItem.ReturnActionId);
    }

    public async Task AssignCartItemToReturnRequestItemAsync(ShoppingCartItem cartItem, ReturnRequestItem returnRequestItem)
    {
        returnRequestItem.ShoppingCartItemId = cartItem.Id;
        await _returnRequestItemRepository.UpdateAsync(returnRequestItem);
        _shortTermCacheManager.RemoveByPrefix(OrderServicesDefaults.CartReturnRequestItemsPrefix);
        _shortTermCacheManager.RemoveByPrefix(OrderServicesDefaults.PackNameCartSubTotalPrefix);
    }

    public async Task SetExchangeRequestCompletedAsync(ReturnRequestItem returnRequestItem)
    {
        // Only do this when we are exchanging an item
        if (returnRequestItem.ExchangeMode == ExchangeReturnMode.Exchange)
        {
            var returnedOrderItem = await _orderService.GetOrderItemByIdAsync(returnRequestItem.ReturnedOrderItemId);
            var originalOrder = await _orderService.GetOrderByIdAsync(returnedOrderItem.OrderId);
            // Add a Note on the Order telling that a Exchange related to this Order has been completed
            await _orderService.InsertOrderNoteAsync(new()
            {
                OrderId = originalOrder.Id,
                DisplayToCustomer = false,
                Note = $"Item Exchange linked to Return Request #{returnRequestItem.ReturnRequestId} has been completed",
                CreatedOnUtc = DateTime.UtcNow
            });

            // Also add a Note on the Order that contains the Exchange item
            if (returnRequestItem.OrderItemId.HasValue)
            {
                var exchangeOrderItem = await _orderService.GetOrderItemByIdAsync(returnRequestItem.OrderItemId.Value);

                var exchangeOrder = await _orderService.GetOrderByIdAsync(exchangeOrderItem.OrderId);

                await _orderService.InsertOrderNoteAsync(new()
                {
                    OrderId = exchangeOrder.Id,
                    CreatedOnUtc = DateTime.UtcNow,
                    DisplayToCustomer = false,
                    Note = $"This Order completes the Exchange linked to Return Request #{returnRequestItem.ReturnRequestId} which belongs to Order #{originalOrder.Id}"
                });
            }
        }

        returnRequestItem.NeedsCompletion = false;
        returnRequestItem.IsCompleted = true;
        await _returnRequestItemRepository.UpdateAsync(returnRequestItem);
        _shortTermCacheManager.RemoveByPrefix(OrderServicesDefaults.CartReturnRequestItemsPrefix);
        _shortTermCacheManager.RemoveByPrefix(OrderServicesDefaults.PackNameCartSubTotalPrefix);
    }

    public async Task SetExchangeRequestNeedsCompletionAsync(ReturnRequestItem returnRequestItem)
    {
        returnRequestItem.NeedsCompletion = true;
        returnRequestItem.IsCompleted = false;
        await _returnRequestItemRepository.UpdateAsync(returnRequestItem);
        _shortTermCacheManager.RemoveByPrefix(OrderServicesDefaults.CartReturnRequestItemsPrefix);
        _shortTermCacheManager.RemoveByPrefix(OrderServicesDefaults.PackNameCartSubTotalPrefix);
    }

    public async Task<ReturnRequestItem> GetReturnRequestItemForCartItemAsync(int cartItemId)
    {
        //The oldest, the same row GetCartReturnRequestItemsAsync gives for the line
        var query = _returnRequestItemRepository.Table.OrderBy(r => r.Id);
        return await query.FirstOrDefaultAsync(r => r.ShoppingCartItemId == cartItemId);
    }

    public virtual async Task<IReadOnlyDictionary<int, ReturnRequestItem>> GetCartReturnRequestItemsAsync(int customerId)
    {
        if (customerId <= 0)
            return new Dictionary<int, ReturnRequestItem>();

        return await _shortTermCacheManager.GetAsync<IReadOnlyDictionary<int, ReturnRequestItem>>(async () =>
        {
            var items = await (from item in _returnRequestItemRepository.Table
                               join sci in _shoppingCartItemRepository.Table on item.ShoppingCartItemId equals (int?)sci.Id
                               where sci.CustomerId == customerId
                               orderby item.Id
                               select item)
                .ToListAsync();

            //One row per cart line, the oldest when several return request items share the line
            return items
                .GroupBy(item => item.ShoppingCartItemId!.Value)
                .ToDictionary(group => group.Key, group => group.First());
        }, OrderServicesDefaults.CartReturnRequestItemsCacheKey, customerId);
    }

    public async Task<IList<ReturnRequestItem>> GetReturnRequestItemsForCartItemsAsync(int[] cartItemsIds)
    {
        var query = _returnRequestItemRepository.Table;
        query = query.Where(r => r.ShoppingCartItemId.HasValue && cartItemsIds.Contains(r.ShoppingCartItemId.Value));
        return await query.ToListAsync();
    }

    public async Task<IList<ReturnRequestItem>> GetReturnRequestItemsForOrderItemsAsync(int[] orderItemsIds)
    {
        var query = _returnRequestItemRepository.Table;
        query = query.Where(r => r.OrderItemId.HasValue ? orderItemsIds.Contains(r.OrderItemId.Value) : false);
        return await query.ToListAsync();
    }

    public virtual async Task<IList<OrderReturnFlags>> GetOrderReturnFlagsAsync(int[] orderIds)
    {
        if (orderIds == null || orderIds.Length == 0)
            return [];

        var ids = orderIds.Distinct().ToArray();

        //E, as 4.1: the order holds the replacement of an exchange. A line's OrderItemId is set only once its
        //replacement order exists — placed at approval, or paid for at checkout
        var exchangeOrderIds = await (from returnRequestItem in _returnRequestItemRepository.Table
                                      join orderItem in _orderItemRepository.Table on returnRequestItem.OrderItemId equals (int?)orderItem.Id
                                      where returnRequestItem.ExchangeModeId == (int)ExchangeReturnMode.Exchange
                                            && ids.Contains(orderItem.OrderId)
                                      select orderItem.OrderId)
            .Distinct()
            .ToListAsync();

        //R: an item of the order was returned and the return authorised — or repaired or refunded, which come
        //after authorising. Pending, received, rejected and cancelled returns do not count
        var returnedStatusIds = new[]
        {
            (int)ReturnRequestStatus.ReturnAuthorized,
            (int)ReturnRequestStatus.ItemsRepaired,
            (int)ReturnRequestStatus.ItemsRefunded
        };

        var returnOrderIds = await (from returnRequest in _returnRequestRepository.Table
                                    join orderItem in _orderItemRepository.Table on returnRequest.OrderItemId equals orderItem.Id
                                    where returnedStatusIds.Contains(returnRequest.ReturnRequestStatusId)
                                          && ids.Contains(orderItem.OrderId)
                                    select orderItem.OrderId)
            .Distinct()
            .ToListAsync();

        return ids
            .Where(id => exchangeOrderIds.Contains(id) || returnOrderIds.Contains(id))
            .Select(id => new OrderReturnFlags(id, exchangeOrderIds.Contains(id), returnOrderIds.Contains(id)))
            .ToList();
    }

    #endregion

    #region Reason and Action linking

    /// <summary>
    /// Synchronizes the relationship between a reason and a collection of actions for a specific store.
    /// </summary>
    /// <param name="reasonId">The ID of the reason to link to actions</param>
    /// <param name="storeId">The ID of the store context for the relationship</param>
    /// <param name="actionsIds">The collection of action IDs that should be associated with the reason</param>
    /// <returns>A task representing the asynchronous operation</returns>
    public async Task LinkReasonToActionsAsync(int reasonId, int storeId, IList<int> actionsIds)
    {
        var existingRelationships = await _reasonActionRelationshipRepository.Table
            .Where(r => r.ReasonId == reasonId && r.StoreId == storeId)
            .ToListAsync();

        var existingActionIds = existingRelationships.Select(r => r.ActionId).ToHashSet();
        var requestedActionIds = actionsIds.ToHashSet();

        // Rows that exist but aren't in the new set — remove
        var toRemove = existingRelationships
            .Where(r => !requestedActionIds.Contains(r.ActionId))
            .ToList();

        // Ids that are requested but don't already exist — add
        var toAdd = requestedActionIds
            .Where(actionId => !existingActionIds.Contains(actionId))
            .Select(actionId => new ReasonActionRelationship
            {
                ReasonId = reasonId,
                ActionId = actionId,
                StoreId = storeId
            })
            .ToList();

        if (toRemove.Count > 0)
            await _reasonActionRelationshipRepository.DeleteAsync(toRemove);

        if (toAdd.Count > 0)
            await _reasonActionRelationshipRepository.InsertAsync(toAdd);
    }

    /// <summary>
    /// Retrieves all return request action mappings associated with a specific return reason and store.
    /// </summary>
    /// <param name="returnReasonId">The ID of the return reason to filter actions by.</param>
    /// <param name="storeId">The ID of the store to filter actions by.</param>
    /// <returns>A list of <see cref="ReasonActionRelationship"/> objects that match the specified reason and store criteria.</returns>
    public async Task<IList<ReasonActionRelationship>> GetActionMappingsByReasonIdAsync(int returnReasonId, int storeId)
    {
        return await _reasonActionRelationshipRepository.Table.Where(rar => rar.ReasonId == returnReasonId && rar.StoreId == storeId).ToListAsync();
    }

    public virtual async Task<IList<int>> GetActiveReasonIdsAsync(int storeId)
    {
        //as for actions: no rule row means "not configured", which means "not offered"
        return await _returnReasonRuleRepository.Table
            .Where(rule => rule.StoreId == storeId && !rule.IsDisabled)
            .Select(rule => rule.ReturnReasonId)
            .ToListAsync();
    }

    public async Task<IList<ReturnRequestAction>> GetActiveActionsByReasonIdAsync(int returnReasonId, int storeId)
    {
        // An action is only offered when this store has an enabled rule row for it.
        // No rule row means "not configured", which means "not allowed" - see reference doc 5.2.
        var enabledActions = await (from ra in _returnRequestActionRepository.Table
                                    join rar in _returnActionRuleRepository.Table.Where(r => r.StoreId == storeId)
                                        on ra.Id equals rar.ReturnActionId
                                    where !rar.IsDisabled
                                    orderby ra.DisplayOrder
                                    select ra).ToListAsync();

        if (returnReasonId <= 0 || enabledActions.Count == 0)
            return enabledActions;

        var linkedActionIds = await _reasonActionRelationshipRepository.Table
            .Where(r => r.ReasonId == returnReasonId && r.StoreId == storeId)
            .Select(r => r.ActionId)
            .ToListAsync();

        // Narrow only when the store has actually configured links for this reason. A reason with no
        // links offers everything rather than nothing, so a configuration gap cannot strand a customer.
        return linkedActionIds.Count > 0
            ? enabledActions.Where(a => linkedActionIds.Contains(a.Id)).ToList()
            : enabledActions;
    }

    #endregion

    #region Return Requests

    public async Task<Order> GetOrderForReturnRequestAsync(ReturnRequest returnRequest)
    {
        var orderItem = await _orderService.GetOrderItemByIdAsync(returnRequest.OrderItemId);
        if (orderItem != null)
        {
            var order = await _orderService.GetOrderByIdAsync(orderItem.OrderId);
            return order;
        }
        return null;
    }

    public async Task<Product> GetReturnedProductAsync(ReturnRequest returnRequest)
    {
        var orderItem = await _orderService.GetOrderItemByIdAsync(returnRequest.OrderItemId);
        if (orderItem != null)
        {
            var product = await _productService.GetProductByIdAsync(orderItem.ProductId);
            return product;
        }
        return null;
    }

    public async Task<IPagedList<ReturnRequest>> GetReturnsForOrderAsync(Order order, int pageIndex = 0,
    int pageSize = int.MaxValue)
    {
        var query = _returnRequestRepository.Table;
        var orderItems = await _orderService.GetOrderItemsAsync(order.Id, isNotReturnable: false);
        int[] orderItemsIds = orderItems.Select(i => i.Id).ToArray();
        query = query.Where(r => orderItemsIds.Contains(r.OrderItemId));
        return await query.ToPagedListAsync(pageIndex, pageSize);
    }

    public IList<ReturnRequest> GetReturnRequestsById(int[] ids)
    {
        var query = _returnRequestRepository.Table;
        return query.Where(r => ids.Contains(r.Id)).ToList();
    }

    /// <summary>
    /// Gets a customer's own return requests among the given identifiers
    /// </summary>
    /// <param name="customerId">Customer identifier</param>
    /// <param name="ids">Return request identifiers</param>
    /// <returns>The customer's return requests among them, in identifier order; the others are left out</returns>
    public virtual async Task<IList<ReturnRequest>> GetCustomerReturnRequestsByIdsAsync(int customerId, int[] ids)
    {
        if (ids == null || ids.Length == 0)
            return new List<ReturnRequest>();

        return await _returnRequestRepository.Table
            .Where(returnRequest => returnRequest.CustomerId == customerId && ids.Contains(returnRequest.Id))
            .OrderBy(returnRequest => returnRequest.Id)
            .ToListAsync();
    }

    /// <summary>
    /// Gets how many units of each of an order's items the customer may still return
    /// </summary>
    /// <remarks>
    /// nop's limit (the ordered quantity less what other return requests already ask for, without non-returnable
    /// products and, unless allowed, downloads) less the units not shipped yet, for an item that needs shipping
    /// </remarks>
    /// <param name="order">Order</param>
    /// <returns>Order item id -> units the customer may return; an item that cannot be returned is left out</returns>
    public virtual async Task<IDictionary<int, int>> GetReturnableQuantitiesAsync(Order order)
    {
        ArgumentNullException.ThrowIfNull(order);

        var result = new Dictionary<int, int>();

        var availability = await _returnRequestService.GetReturnRequestAvailabilityAsync(order.Id);
        if (availability?.ReturnableOrderItems == null || availability.ReturnableOrderItems.Count == 0)
            return result;

        var shippedQuery = from shipmentItem in _shipmentItemRepository.Table
                           join shipment in _shipmentRepository.Table on shipmentItem.ShipmentId equals shipment.Id
                           where shipment.OrderId == order.Id
                           group shipmentItem.Quantity by shipmentItem.OrderItemId into grouped
                           select new { OrderItemId = grouped.Key, Quantity = grouped.Sum() };
        var shipped = await shippedQuery.ToDictionaryAsync(row => row.OrderItemId, row => row.Quantity);

        var productIds = availability.ReturnableOrderItems.Select(item => item.OrderItem.ProductId).Distinct().ToArray();
        var shipEnabled = (await _productService.GetProductsByIdsAsync(productIds))
            .ToDictionary(product => product.Id, product => product.IsShipEnabled);

        foreach (var returnable in availability.ReturnableOrderItems)
        {
            var orderItem = returnable.OrderItem;
            //a product no longer found counts as one that needs shipping
            var notShipped = shipEnabled.GetValueOrDefault(orderItem.ProductId, true)
                ? Math.Max(orderItem.Quantity - shipped.GetValueOrDefault(orderItem.Id), 0)
                : 0;
            var quantity = returnable.AvailableQuantityForReturn - notShipped;

            if (quantity > 0)
                result[orderItem.Id] = quantity;
        }

        return result;
    }

    /// <summary>
    /// Determines the number of units that have been returned for a given Order Items list.
    /// Returns a dictionary mapping Order Item Ids to the number of units returned. Items with no returns are absent from the dictionary.
    /// </summary>
    /// <param name="orderItemIds">Order item identifiers</param>
    /// <returns>Order item id -> units returned. Items with no returns are absent.</returns>
    public async Task<IReadOnlyDictionary<int, int>> CountReturnedUnitsAsync(int[] orderItemIds)
    {
        if (orderItemIds == null || orderItemIds.Length == 0)
            return new Dictionary<int, int>();

        var query = from rri in _returnRequestItemRepository.Table
                    join rr in _returnRequestRepository.Table on rri.ReturnRequestId equals rr.Id
                    where orderItemIds.Contains(rri.ReturnedOrderItemId)
                    group rr.Quantity by rri.ReturnedOrderItemId into grouped
                    select new { OrderItemId = grouped.Key, Units = grouped.Sum() };

        return await query.ToDictionaryAsync(row => row.OrderItemId, row => row.Units);
    }

    public bool IsReturnRequestAlreadyPerformed(int returnedOrderItemId, Guid returnRequestGroupId, out int returnRequestId)
    {
        var query = _returnRequestItemRepository.Table;

        query = query.Where(x => x.ReturnedOrderItemId == returnedOrderItemId
                                    && (x.ReturnRequestGroup.HasValue && (x.ReturnRequestGroup.Value == returnRequestGroupId)));

        var returnRequestItem = query.FirstOrDefault();

        returnRequestId = 0;

        if (returnRequestItem != null)
        {
            returnRequestId = returnRequestItem.ReturnRequestId;
        }

        return returnRequestItem != null;
    }

    #endregion

    #region Exchange Fees Amounts

    /// <summary>
    /// Prices a whole return case from the live fee rules, one line at a time.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Every fee is priced over its own line and nothing is summed across the case. This is deliberate:
    /// a line is executed the moment it is authorised, so its fee has to be final at
    /// that moment, and a band taken over siblings that have not been decided yet cannot be.
    /// </para>
    /// <para>
    /// The band itself is intact — CalculateFeeAmount is ceil(quantity / unitsQuantity) x amount, so
    /// "$5 per 4 units" charges $5 for four units of one product on one return request. What is NOT done
    /// here is spreading that band across several return requests of the same parcel: Sales Support zero
    /// the redelivery quantity on the lines that should not carry it, which is how the business works and
    /// is the flexibility it asked to keep.
    /// </para>
    /// Rejected lines are free and are skipped entirely.
    /// </remarks>
    public virtual async Task<IList<ExchangeFeeAmount>> CalculateExchangeFeeAmountsForCaseAsync(int returnCaseId)
    {
        var amounts = new List<ExchangeFeeAmount>();

        if (returnCaseId <= 0)
            return amounts;

        var lines = await _returnCaseService.GetReturnCaseLinesAsync(returnCaseId);

        //Rejected lines charge nothing. Pending lines are priced because the
        //approval screen quotes them — and because each line's figure is its own, a pending sibling
        //cannot move it either way.
        foreach (var line in lines.Where(l => l.Disposition != ReturnLineDisposition.Rejected))
        {
            if (line.ReturnRequestId is not > 0)
                continue;

            var item = await GetReturnRequestItemAsync(line.ReturnRequestId.Value);

            if (item == null)
                continue;

            //One line, priced on its own. The only thing added here is the
            //line id, which is what lets a reader attribute a charge without re-deriving it.
            foreach (var fee in await CalculateExchangeFeeAmountsForItemAsync(item))
            {
                fee.ReturnCaseLineId = line.Id;
                amounts.Add(fee);
            }
        }

        return amounts;
    }

    /// <summary>
    /// Prices one returned item on its own — the fallback for a return raised before the case model
    /// existed, which has no case to be priced with.
    /// </summary>
    public virtual async Task<IList<ExchangeFeeAmount>> CalculateExchangeFeeAmountsForItemAsync(ReturnRequestItem returnRequestItem, ReturnRequest returnRequest = null)
    {
        var amounts = new List<ExchangeFeeAmount>();

        if (returnRequestItem == null)
            return amounts;

        returnRequest ??= await _returnRequestService.GetReturnRequestByIdAsync(returnRequestItem.ReturnRequestId);

        if (returnRequest == null)
            return amounts;

        var returnAction = await GetSelectedActionAsync(returnRequestItem);
        var returnReason = await GetSelectedReasonAsync(returnRequestItem);

        if (returnAction != null && returnReason != null)
        {
            var feeRules = await GetFeeRulesForActionAndReasonAsync(returnAction.Id, returnReason.Id, returnRequest.StoreId);

            foreach (var feeRule in feeRules ?? new List<FeeRule>())
            {
                if (!IsDynamicAmountFee(feeRule.FeeType))
                    continue;

                var quantity = ExchangesHelper.DetermineFeeRuleQuantity(feeRule, returnRequest, returnRequestItem);
                var amount = ExchangesHelper.CalculateFeeAmount(feeRule, quantity);

                if (amount <= decimal.Zero)
                    continue;

                amounts.Add(new ExchangeFeeAmount
                {
                    ReturnCaseLineId = null,
                    FeeType = feeRule.FeeType,
                    Amount = amount,
                    Quantity = quantity,
                    Description = ExchangesHelper.DetermineFeeTypeName(feeRule.FeeType),
                    SourceFeeRuleId = feeRule.Id
                });
            }
        }

        if (returnRequestItem.AdditionalFeeAmount.HasValue)
        {
            amounts.Add(new ExchangeFeeAmount
            {
                ReturnCaseLineId = null,
                FeeType = FeeType.AdditionalFee,
                Amount = returnRequestItem.AdditionalFeeAmount.Value,
                Quantity = returnRequest.Quantity,
                Description = returnRequestItem.AdditionalFeeName,
                SourceFeeRuleId = null
            });
        }

        return amounts;
    }

    /// <summary>
    /// A case's fees as every reader should see them: frozen where it is resolved, live where it is not.
    /// </summary>
    /// <remarks>
    /// A resolved case answers for itself even when the answer is "no fees" — falling back to live rules
    /// on an empty charge set would re-price a case that legitimately resolved free.
    /// </remarks>
    public virtual async Task<IList<ExchangeFeeAmount>> GetExchangeFeeAmountsForCaseAsync(int returnCaseId)
    {
        if (returnCaseId <= 0)
            return new List<ExchangeFeeAmount>();

        var resolution = await _returnCaseService.GetResolutionByCaseIdAsync(returnCaseId);

        if (resolution == null)
            return await CalculateExchangeFeeAmountsForCaseAsync(returnCaseId);

        //Resolved is resolved: the frozen charges win and nothing re-prices them.
        //That is the freeze working — see the note on re-pricing a resolved case.
        return (await _returnCaseService.GetChargesByResolutionIdAsync(resolution.Id)).Select(ToFeeAmount).ToList();
    }

    public async Task<decimal> CalculateExchangeFeesAmountAsync(ReturnRequestItem returnRequestItem)
    {
        var fees = await GetExchangeFeeAmountsForItemAsync(returnRequestItem);

        return fees.Sum(fee => fee.Amount);
    }

    public async Task<IDictionary<FeeType, decimal>> GetExchangeFeesAmountsAsync(ReturnRequestItem returnRequestItem)
    {
        var exchangeFeesForReturnedItem = new Dictionary<FeeType, decimal>();

        AddFeeAmountsToTarget(exchangeFeesForReturnedItem, await GetExchangeFeeAmountsForItemAsync(returnRequestItem));

        return exchangeFeesForReturnedItem;
    }

    public async Task<IDictionary<FeeType, decimal>> GetExchangeFeesAmountsAsync(IEnumerable<ShoppingCartItem> cart, IList<AdditionalFeeItem> additionalFeeItems = null)
    {
        var cartExchangeFees = new Dictionary<FeeType, decimal>();

        // Retrieve all Return Request items that are associated to items on the cart
        var returnedItemsInCart = await GetReturnRequestItemsForCartItemsAsync(cart.Select(ci => ci.Id).ToArray());

        await AddReturnedItemsFeesToTargetAsync(cartExchangeFees, returnedItemsInCart, additionalFeeItems);

        return cartExchangeFees;
    }

    public async Task<IDictionary<FeeType, decimal>> GetExchangeFeesAmountsAsync(Order order, IList<AdditionalFeeItem> additionalFeeItems = null)
    {
        var orderExchangeFees = new Dictionary<FeeType, decimal>();

        // Retrieve all Return Request items that are associated to items on the Order
        var orderItems = await _orderService.GetOrderItemsAsync(order.Id, isNotReturnable: false);
        var returnedItemsInOrder = await GetReturnRequestItemsForOrderItemsAsync(orderItems.Select(oi => oi.Id).ToArray());

        await AddReturnedItemsFeesToTargetAsync(orderExchangeFees, returnedItemsInOrder, additionalFeeItems);

        return orderExchangeFees;
    }

    #endregion

    #region Exchange Ordering

    public async Task<IList<ShoppingCartItem>> BuildProductsListForReturnOrderAsync(ReturnRequest returnRequest, ReturnRequestItem returnRequestItem, bool useReturnedItems = false)
    {
        IList<ShoppingCartItem> productsList = new List<ShoppingCartItem>();
        Product product;
        string attributesXml;

        if (returnRequestItem.ExchangeType == ExchangeType.Exact || useReturnedItems)
        {
            var orderItem = await _orderService.GetOrderItemByIdAsync(returnRequest.OrderItemId);
            attributesXml = orderItem.AttributesXml;
            product = await GetReturnedProductAsync(returnRequest);
        }
        else
        {
            var replacementItem = await GetReplacementItemAsync(returnRequestItem.Id) ?? throw new NopException($"Return request item {returnRequestItem.Id} is an exchange but has no replacement item recorded");
            product = await _productService.GetProductByIdAsync(replacementItem.ProductId);
            attributesXml = replacementItem.AttributesXml;
        }

        DateTime now = DateTime.UtcNow;

        var item = new ShoppingCartItem
        {
            ShoppingCartType = ShoppingCartType.ShoppingCart,
            StoreId = returnRequest.StoreId,
            ProductId = product.Id,
            AttributesXml = attributesXml,
            CustomerEnteredPrice = decimal.Zero,
            Quantity = returnRequest.Quantity,
            CreatedOnUtc = now,
            UpdatedOnUtc = now,
            CustomerId = returnRequest.CustomerId
        };

        productsList.Add(item);
        return productsList;
    }

    public async Task<IList<ExchangeReturnMode>> DetermineEnabledReturnModesAsync(int storeId, int returnReasonId, int returnActionId)
    {
        IList<ExchangeReturnMode> modes = new List<ExchangeReturnMode>();

        var returnReasonRule = await GetReturnReasonRuleByReasonIdAsync(returnReasonId, storeId);
        var returnActionRule = await GetReturnActionRuleByActionIdAsync(returnActionId, storeId);

        if (returnReasonRule != null && returnActionRule != null)
        {
            if (returnReasonRule.AllowExchange && returnActionRule.AllowExchange)
            {
                modes.Add(ExchangeReturnMode.Exchange);
            }
            if (returnReasonRule.AllowStoreCredit && returnActionRule.AllowStoreCredit)
            {
                modes.Add(ExchangeReturnMode.CreditNote);
            }
        }

        return modes;
    }

    #endregion
}
