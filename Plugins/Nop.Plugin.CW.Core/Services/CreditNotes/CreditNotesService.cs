using Nop.Core;
using Nop.Core.Caching;
using Nop.Core.Domain.Customers;
using Nop.Core.Domain.Orders;
using Nop.Data;
using Nop.Plugin.CW.Core.Constants;
using Nop.Plugin.CW.Core.Domain.CreditNotes;
using Nop.Plugin.CW.Core.Domain.CreditNotes.Views;
using Nop.Services.Catalog;
using Nop.Services.Customers;
using Nop.Services.Directory;
using Nop.Services.Logging;
using Nop.Services.Orders;

namespace Nop.Plugin.CW.Core.Services.CreditNotes;

public class CreditNotesService : ICreditNotesService
{
    #region Fields

    protected readonly IWorkContext _workContext;
    protected readonly ICurrencyService _currencyService;
    protected readonly IPriceFormatter _priceFormatter;
    protected readonly IRepository<CreditNote> _creditNotesRepository;
    protected readonly IRepository<CreditNoteBalance> _creditNoteBalanceRepository;
    protected readonly IRepository<CreditNoteHistory> _creditNoteHistoriesRepository;
    protected readonly IRepository<CreditNoteHistoryBalance> _creditNoteHistoryBalanceRepository;
    protected readonly ICustomerActivityService _customerActivityService;
    protected readonly ICustomerService _customerService;
    protected readonly IOrderService _orderService;
    protected readonly ILocker _locker;
    protected readonly IShortTermCacheManager _shortTermCacheManager;
    protected readonly IStaticCacheManager _staticCacheManager;

    #endregion

    #region Ctor

    public CreditNotesService(
        IWorkContext workContext,
        ICurrencyService currencyService,
        IPriceFormatter priceFormatter,
        IRepository<CreditNote> creditNotesRepository,
        IRepository<CreditNoteBalance> creditNoteBalanceRepository,
        IRepository<CreditNoteHistory> creditNoteHistoriesRepository,
        IRepository<CreditNoteHistoryBalance> creditNoteHistoryBalanceRepository,
        ICustomerActivityService customerActivityService,
        ICustomerService customerService,
        IOrderService orderService,
        ILocker locker,
        IShortTermCacheManager shortTermCacheManager,
        IStaticCacheManager staticCacheManager)
    {
        _workContext = workContext;
        _currencyService = currencyService;
        _priceFormatter = priceFormatter;
        _creditNotesRepository = creditNotesRepository;
        _creditNoteBalanceRepository = creditNoteBalanceRepository;
        _creditNoteHistoriesRepository = creditNoteHistoriesRepository;
        _creditNoteHistoryBalanceRepository = creditNoteHistoryBalanceRepository;
        _customerActivityService = customerActivityService;
        _customerService = customerService;
        _orderService = orderService;
        _locker = locker;
        _shortTermCacheManager = shortTermCacheManager;
        _staticCacheManager = staticCacheManager;
    }

    #endregion

    #region Utilities

    protected static CreditNote MapToCreditNote(CreditNoteBalance v) => new()
    {
        Id = v.Id,
        CustomerId = v.CustomerId,
        OrderId = v.OrderId,
        CreatedOnUtc = v.CreatedOnUtc,
        Reference = v.Reference,
        Name = v.Name,
        Amount = v.Amount,
        Remain = v.Remain,
        ReturnReason = v.ReturnReason,
        StoreId = v.StoreId,
        ReturnRequestId = v.ReturnRequestId,
        CreatedByCustomerId = v.CreatedByCustomerId
    };

    protected static CreditNoteHistory MapToCreditNoteHistory(CreditNoteHistoryBalance v) => new()
    {
        Id = v.Id,
        CreditNoteId = v.CreditNoteId,
        CustomerId = v.CustomerId,
        OrderId = v.OrderId,
        CreatedOnUtc = v.CreatedOnUtc,
        Amount = v.Amount,
        Remain = v.Remain
    };

    /// <summary>
    /// Reads open credit notes straight from the view, bypassing the per-request cache.
    /// Only for use inside the apply lock: the cached read may have been populated by an earlier quote
    /// in the same request, before the lock was taken.
    /// </summary>
    protected virtual async Task<IList<CreditNote>> GetOpenCreditNotesForApplyAsync(int customerId)
    {
        var query = _creditNoteBalanceRepository.Table
                        .Where(c => c.CustomerId == customerId && c.Remain > 0)
                        .OrderBy(c => c.CreatedOnUtc);

        return (await query.ToListAsync()).Select(MapToCreditNote).ToList();
    }

    #endregion

    #region Methods

    public async Task<CreditNote?> GetCreditNoteByIdAsync(int creditNoteId)
    {
        return await _shortTermCacheManager.GetAsync(async () =>
        {
            var creditNoteBalance = await _creditNoteBalanceRepository.Table.FirstOrDefaultAsync(c => c.Id == creditNoteId);
            return creditNoteBalance == null ? null : MapToCreditNote(creditNoteBalance);
        }, CreditNotesServicesDefaults.CreditNoteByIdCacheKey, creditNoteId);
    }

    public async Task<IList<CreditNote>> GetOpenCreditNotesByCustomerIdAsync(int customerId)
    {
        return await _shortTermCacheManager.GetAsync(async () =>
        {
            var query = _creditNoteBalanceRepository.Table
                            .Where(c => c.CustomerId == customerId && c.Remain > 0)
                            .OrderBy(c => c.CreatedOnUtc);

            return (IList<CreditNote>)(await query.ToListAsync()).Select(MapToCreditNote).ToList();
        }, CreditNotesServicesDefaults.CreditNoteBalanceByCustomerIdCacheKey, customerId);
    }

    public async Task<IPagedList<CreditNote>> SearchCreditNotesByCustomerIdAsync(int customerId, int pageIndex = 0, int pageSize = int.MaxValue)
    {
        var query = _creditNoteBalanceRepository.Table
            .Where(c => c.CustomerId == customerId)
            .OrderBy(c => c.CreatedOnUtc);

        var paged = await query.ToPagedListAsync(pageIndex, pageSize);
        return new PagedList<CreditNote>(paged.Select(MapToCreditNote).ToList(), paged.PageIndex, paged.PageSize, paged.TotalCount);
    }

    public async Task<IList<CreditNoteHistory>> GetCreditNoteHistoriesByOrderIdAsync(int orderId)
    {
        var query = _creditNoteHistoryBalanceRepository.Table.Where(c => c.OrderId == orderId);
        return (await query.ToListAsync()).Select(MapToCreditNoteHistory).ToList();
    }

    public async Task<IList<CreditNoteHistory>> GetCreditNoteHistoriesByCustomerIdAsync(int customerId)
    {
        var query = _creditNoteHistoryBalanceRepository.Table.Where(c => c.CustomerId == customerId);
        return (await query.ToListAsync()).Select(MapToCreditNoteHistory).ToList();
    }

    public async Task<IList<CreditNoteHistory>> GetCreditNoteHistoriesByCreditNoteAsync(int creditNoteId)
    {
        var query = _creditNoteHistoryBalanceRepository.Table.Where(c => c.CreditNoteId == creditNoteId);
        return (await query.ToListAsync()).Select(MapToCreditNoteHistory).ToList();
    }

    public async Task<(IDictionary<int, decimal> Applied, decimal AppliedTotal)> CalculateAppliedCreditNotesAsync(int customerId, decimal orderTotal, bool bypassCache = false)
    {
        var appliedCreditNotesAmountDic = new Dictionary<int, decimal>();
        var originalTotal = orderTotal;
        var neededTotal = orderTotal;
        var creditNotes = bypassCache
                    ? await GetOpenCreditNotesForApplyAsync(customerId)
                    : await GetOpenCreditNotesByCustomerIdAsync(customerId);

        foreach (var creditNote in creditNotes)
        {
            if (creditNote.Remain >= neededTotal)
            {
                appliedCreditNotesAmountDic.Add(creditNote.Id, neededTotal);
                neededTotal = 0;
                break;
            }
            neededTotal -= creditNote.Remain;
            appliedCreditNotesAmountDic.Add(creditNote.Id, creditNote.Remain);
        }

        var appliedCreditNotesTotal = originalTotal - neededTotal;
        return (appliedCreditNotesAmountDic, appliedCreditNotesTotal);
    }

    /// <summary>
    /// Applies the customer's open credit notes to an order, up to <paramref name="settleableAmount"/>.
    /// </summary>
    /// <param name="settleableAmount">
    /// The amount still to settle, incl tax. Always supplied by the caller — an order's own OrderTotal is
    /// the payable balance after settlement, not the amount to settle with.
    /// </param>
    public async Task<(decimal AppliedTotal, decimal PendingOrderBalance)> ApplyCreditNotesForOrderAsync(Order order, decimal settleableAmount)
    {
        ArgumentNullException.ThrowIfNull(order);

        var appliedCreditNotesTotal = 0m;

        // Remain is computed live from the view, so two concurrent orders for the same customer
        // could otherwise both read the same "available" snapshot before either writes its
        // CreditNoteHistory row and over-spend the note. Serialize apply-credit per customer.
        var lockAcquired = await _locker.PerformActionWithLockAsync(
            $"creditnotes_apply_customer_{order.CustomerId}",
            TimeSpan.FromSeconds(30),
            async () =>
            {
                var (creditNotesAmount, appliedTotal) = await CalculateAppliedCreditNotesAsync(order.CustomerId, settleableAmount, bypassCache: true);
                appliedCreditNotesTotal = appliedTotal;

                var insertedCreditNoteHistory = creditNotesAmount
                    .Select(amount => new CreditNoteHistory
                    {
                        Amount = amount.Value,
                        CreatedOnUtc = DateTime.UtcNow,
                        CreditNoteId = amount.Key,
                        OrderId = order.Id,
                        CustomerId = order.CustomerId
                    })
                    .ToList();

                if (insertedCreditNoteHistory.Count > 0)
                    await _creditNoteHistoriesRepository.InsertAsync(insertedCreditNoteHistory);
            });

        if (!lockAcquired)
            throw new NopException($"Could not apply credit notes for customer {order.CustomerId}: another operation is already applying this customer's credit notes. Please try again.");

        var pendingOrderBalance = settleableAmount - appliedCreditNotesTotal;
        return (appliedCreditNotesTotal, pendingOrderBalance);
    }

    public async Task CreateCreditNotesForOrderAsync(Order order, decimal returnForCredit, string returnReason, int? returnRequestId = null, int? createdByCustomerId = null)
    {
        var returnAmount = await _currencyService.ConvertFromPrimaryStoreCurrencyAsync(returnForCredit, await _workContext.GetWorkingCurrencyAsync());

        var creditNote = new CreditNote
        {
            CustomerId = order.CustomerId,
            OrderId = order.Id,
            CreatedOnUtc = DateTime.UtcNow,
            Reference = null,
            Name = $"Credit Note for transaction amount {await _priceFormatter.FormatPriceAsync(returnAmount, true, false)} from returned sales order {order.Id}",
            Amount = returnForCredit,
            ReturnReason = returnReason,
            StoreId = order.StoreId,
            ReturnRequestId = returnRequestId,
            CreatedByCustomerId = createdByCustomerId
        };

        await _creditNotesRepository.InsertAsync(creditNote);
    }

    public async Task CreateManualCreditNoteAsync(int customerId, int orderId, decimal amount, string reason, Customer? createdByUser = null)
    {
        var editedCustomer = await _customerService.GetCustomerByIdAsync(customerId) ?? throw new NopException($"Customer with StoreId {customerId} does not exist");
        var order = await _orderService.GetOrderByIdAsync(orderId) ?? throw new NopException($"Order with StoreId {orderId} does not exist");

        createdByUser ??= await _workContext.GetCurrentCustomerAsync();

        var creditNote = new CreditNote
        {
            CustomerId = editedCustomer.Id,
            OrderId = orderId,
            CreatedOnUtc = DateTime.UtcNow,
            Reference = null,
            Name = $"Manual Credit Note for amount {await _priceFormatter.FormatPriceAsync(amount, true, false)} on Order #{orderId}",
            Amount = amount,
            ReturnReason = reason,
            ReturnRequestId = null,
            StoreId = order.StoreId,
            CreatedByCustomerId = createdByUser?.Id
        };

        await _creditNotesRepository.InsertAsync(creditNote);

        await _customerActivityService.InsertActivityAsync(
            createdByUser,
            "CreditNotesManual",
            $"Manual Credit Note has been created with StoreId {creditNote.Id} and amount {amount} for customer StoreId: {editedCustomer.Id}",
            creditNote);
    }

    public virtual async Task<bool> CustomerHasCreditNotesAsync(int customerId)
    {
        if (customerId <= 0)
            return false;

        var cacheKey = _staticCacheManager.PrepareKeyForDefaultCache(CreditNotesServicesDefaults.CustomerHasCreditNotesCacheKey, customerId);

        return await _staticCacheManager.GetAsync(cacheKey, async () =>
        {
            if (await _creditNotesRepository.Table.AnyAsync(cn => cn.CustomerId == customerId))
                return true;

            return await _creditNoteHistoriesRepository.Table.AnyAsync(h => h.CustomerId == customerId);
        });
    }

    /// <summary>
    /// Deletes a Credit Note that was created by a Return Request, together with its full history.
    /// Used only when the Return Request itself is being undone/deleted entirely — not the same
    /// situation as an order being cancelled.
    /// </summary>
    public async Task UndoCreditNoteForReturnRequestAsync(int returnRequestId)
    {
        var creditNote = await _creditNotesRepository.Table
            .Where(c => c.ReturnRequestId == returnRequestId)
            .OrderByDescending(x => x.Id)
            .FirstOrDefaultAsync();

        if (creditNote != null)
        {
            var creditNotesHistories = await _creditNoteHistoriesRepository.GetAllAsync(q => q.Where(h => h.CreditNoteId == creditNote.Id));
            await _creditNoteHistoriesRepository.DeleteAsync(creditNotesHistories);
            await _creditNotesRepository.DeleteAsync(creditNote);
            await _customerActivityService.InsertActivityAsync(
                CustomerActivityTypes.CREDIT_NOTES_UNDO.Key,
                $"Credit Note with ID '{creditNote.Id}' was undone for Return Request ID '{returnRequestId}'",
                creditNote);
        }
    }

    public async Task<CreditNote?> GetCreditNoteByReturnRequestIdAsync(int returnRequestId)
    {
        if (returnRequestId <= 0)
            return null;

        //Ordered the same way the undo orders it: a case that was undone and re-approved leaves the older
        //row behind, and the live one is the last written.
        return await _creditNotesRepository.Table
            .Where(c => c.ReturnRequestId == returnRequestId)
            .OrderByDescending(x => x.Id)
            .FirstOrDefaultAsync();
    }

    public async Task<IList<CreditNote>> GetCreditNotesByStoreAsync(int storeId)
    {
        var query = _creditNoteBalanceRepository.Table.Where(c => c.StoreId == storeId);
        return (await query.ToListAsync()).Select(MapToCreditNote).ToList();
    }

    public async Task<bool> CustomerHasCreditNotesBalanceAsync(int customerId)
    {
        var creditNotes = await GetOpenCreditNotesByCustomerIdAsync(customerId);
        return creditNotes.Count > 0;
    }

    public async Task<bool> CreditNoteHasHistoryAsync(int creditNoteId)
    {
        return await _creditNoteHistoriesRepository.Table.AnyAsync(h => h.CreditNoteId == creditNoteId);
    }

    public async Task DeleteCreditNoteAsync(CreditNote creditNote)
    {
        if (creditNote != null)
        {
            var history = await _creditNoteHistoriesRepository.GetAllAsync(q => q.Where(h => h.CreditNoteId == creditNote.Id));
            await _creditNoteHistoriesRepository.DeleteAsync(history);
            await _creditNotesRepository.DeleteAsync(creditNote);
        }
    }

    public async Task UpdateCreditNoteAsync(CreditNote creditNote)
    {
        // Remain is [NotColumn] / computed — LinqToDB skips it on write. Every other field
        // (Name, ReturnReason, etc.) is written normally.
        await _creditNotesRepository.UpdateAsync(creditNote);
    }

    #endregion
}