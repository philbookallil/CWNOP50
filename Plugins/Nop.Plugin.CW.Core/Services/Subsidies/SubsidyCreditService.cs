using Nop.Core;
using Nop.Core.Caching;
using Nop.Data;
using Nop.Plugin.CW.Core.Domain.Orders.Views;
using Nop.Plugin.CW.Core.Domain.Subsidies;
using Nop.Plugin.CW.Core.Domain.Subsidies.DTOs;
using Nop.Plugin.CW.Core.Domain.Subsidies.Views;
using Nop.Services.Orders;

namespace Nop.Plugin.CW.Core.Services.Subsidies;

public class SubsidyCreditService : ISubsidyCreditService
{
    #region Fields

    protected readonly ISubsidyService _subsidyService;
    protected readonly IOrderService _orderService;
    protected readonly IRepository<SubsidyPackNameCredit> _subsidyPackNameCreditRepository;
    protected readonly IRepository<ProductGroupPackNameCredit> _productGroupPackNameCreditRepository;
    protected readonly IRepository<SubsidyOrderItem> _subsidyOrderItemRepository;
    protected readonly IRepository<SubsidyOrderShipping> _subsidyOrderShippingRepository;
    protected readonly IRepository<SubsidyRestoreItem> _subsidyRestoreItemRepository;
    protected readonly ILocker _locker;

    /// <summary>
    /// The credit of each packname read in this request. The service is scoped, so this lives for one request:
    /// the credit views are read at most once per packname per request, and restoring or un-restoring a returned
    /// item empties it so the rest of the request sees the new balance.
    /// </summary>
    protected readonly Dictionary<int, PackNameCredit> _packNameCredits = new();

    #endregion

    #region Ctor

    public SubsidyCreditService(ISubsidyService subsidyService,
        IOrderService orderService,
        IRepository<SubsidyPackNameCredit> subsidyPackNameCreditRepository,
        IRepository<ProductGroupPackNameCredit> productGroupPackNameCreditRepository,
        IRepository<SubsidyOrderItem> subsidyOrderItemRepository,
        IRepository<SubsidyOrderShipping> subsidyOrderShippingRepository,
        IRepository<SubsidyRestoreItem> subsidyRestoreItemRepository,
        ILocker locker)
    {
        _subsidyService = subsidyService;
        _orderService = orderService;
        _subsidyPackNameCreditRepository = subsidyPackNameCreditRepository;
        _productGroupPackNameCreditRepository = productGroupPackNameCreditRepository;
        _subsidyOrderItemRepository = subsidyOrderItemRepository;
        _subsidyOrderShippingRepository = subsidyOrderShippingRepository;
        _subsidyRestoreItemRepository = subsidyRestoreItemRepository;
        _locker = locker;
    }

    #endregion

    #region Methods

    public async Task<IList<SubsidyPackNameCredit>> GetSubsidyPackNameCreditsByPackNameIdAsync(int packNameId)
    {
        var query = from spc in _subsidyPackNameCreditRepository.Table
                    where spc.PackNameId == packNameId
                    select spc;

        return await query.ToListAsync();
    }

    public async Task<IList<ProductGroupPackNameCredit>> GetProductGroupPackNameCreditByPackNameIdsAsync(int packNameId)
    {
        var query = from pgc in _productGroupPackNameCreditRepository.Table
                    where pgc.PackNameId == packNameId
                    select pgc;

        return await query.ToListAsync();
    }

    public async Task<PackNameCredit> GetPackNameSubsidyCreditAsync(int packNameId)
    {
        return (await GetPackNamesSubsidyCreditAsync([packNameId])).First();
    }

    /// <remarks>
    /// The packnames not read yet in this request are read together: one query to each credit view for all of
    /// them, not two per packname. Each one returned is a deep clone, so a caller may spend it as it allocates.
    /// </remarks>
    public async Task<IList<PackNameCredit>> GetPackNamesSubsidyCreditAsync(IList<int> packNameIds)
    {
        ArgumentNullException.ThrowIfNull(packNameIds);

        await LoadPackNameCreditsAsync(packNameIds);

        return packNameIds
            .Select(id => (PackNameCredit)_packNameCredits[id].Clone())
            .ToList();
    }

    /// <summary>
    /// Reads the credit of the given packnames that this request has not read yet, in one query to each credit
    /// view, so the reads of one packname after it are answered without a query
    /// </summary>
    /// <param name="packNameIds">PackName identifiers</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    public async Task LoadPackNameCreditsAsync(IList<int> packNameIds)
    {
        ArgumentNullException.ThrowIfNull(packNameIds);

        var missingIds = packNameIds.Distinct().Where(id => !_packNameCredits.ContainsKey(id)).ToList();

        if (missingIds.Count > 0)
        {
            var subsidyCredits = await _subsidyPackNameCreditRepository.Table
                .Where(spc => missingIds.Contains(spc.PackNameId))
                .ToListAsync();

            var productGroupCredits = await _productGroupPackNameCreditRepository.Table
                .Where(pgc => missingIds.Contains(pgc.PackNameId))
                .ToListAsync();

            var productsByGroup = new Dictionary<int, List<int>>();

            foreach (var productGroupId in productGroupCredits.Select(x => x.ProductGroupId).Distinct())
                productsByGroup[productGroupId] = (await _subsidyService.GetProductsByProductGroupIdAsync(productGroupId))
                    .Select(p => p.ProductId)
                    .ToList();

            foreach (var packNameId in missingIds)
            {
                var packNameCredit = new PackNameCredit
                {
                    PackNameId = packNameId
                };

                foreach (var subsidyPackNameCredit in subsidyCredits.Where(x => x.PackNameId == packNameId))
                {
                    var subsidyCredit = new SubsidyCredit
                    {
                        SubsidyId = subsidyPackNameCredit.SubsidyId,
                        Credit = subsidyPackNameCredit.Credit,
                        CreditTopup = subsidyPackNameCredit.CreditTopup,
                        AvailableCredit = subsidyPackNameCredit.Credit - subsidyPackNameCredit.UsedCredit,
                        AvailableCreditTopup = subsidyPackNameCredit.CreditTopup - subsidyPackNameCredit.UsedCreditTopup,
                        OnlyAllocatedProducts = subsidyPackNameCredit.OnlyAllocatedProducts,
                        RestrictToAllocatedQty = subsidyPackNameCredit.RestrictToAllocatedQty
                    };

                    foreach (var productGroupPackNameCredit in productGroupCredits.Where(x => x.PackNameId == packNameId && x.SubsidyId == subsidyPackNameCredit.SubsidyId))
                    {
                        var productGroupCredit = new ProductGroupCredit
                        {
                            ProductGroupId = productGroupPackNameCredit.ProductGroupId,
                            Name = productGroupPackNameCredit.ProductGroupName,
                            DisplayOrder = productGroupPackNameCredit.DisplayOrder,
                            Credit = productGroupPackNameCredit.Credit,
                            CreditTopup = productGroupPackNameCredit.CreditTopup,
                            Quantity = productGroupPackNameCredit.Quantity,
                            QuantityTopup = productGroupPackNameCredit.QuantityTopup,
                            AvailableCredit = productGroupPackNameCredit.Credit - productGroupPackNameCredit.UsedCredit,
                            AvailableCreditTopup = productGroupPackNameCredit.CreditTopup - productGroupPackNameCredit.UsedCreditTopup,
                            AvailableQuantity = productGroupPackNameCredit.Quantity - productGroupPackNameCredit.UsedQuantity,
                            AvailableQuantityTopup = productGroupPackNameCredit.QuantityTopup - productGroupPackNameCredit.UsedQuantityTopup,
                            ProductIds = productsByGroup.GetValueOrDefault(productGroupPackNameCredit.ProductGroupId) ?? new List<int>(),
                        };

                        subsidyCredit.ProductGroupCredits.Add(productGroupCredit);
                    }

                    packNameCredit.SubsidyCredits.Add(subsidyCredit);
                }

                _packNameCredits[packNameId] = packNameCredit;
            }
        }
    }

    public int ApplyPackNameSubsidyQuantity(PackNameCredit packNameCredit, PackNameShoppingCartItem shoppingCartItem, int quantiy, IList<SubsidyOrderItem> orderItems)
    {
        if (packNameCredit != null)
        {
            foreach (var subsidyCredit in packNameCredit.SubsidyCredits)
            {
                foreach (var productGroupCredit in subsidyCredit.ProductGroupCredits.Where(x => x.ProductIds.Contains(shoppingCartItem.ProductId)))
                {
                    if (productGroupCredit.AvailableQuantity > 0)
                    {
                        var orderItem = orderItems.FirstOrDefault(x => x.SubsidyId == subsidyCredit.SubsidyId && x.ProductGroupId == productGroupCredit.ProductGroupId && x.Id == shoppingCartItem.Id);

                        if (orderItem == null)
                        {
                            orderItem = new SubsidyOrderItem
                            {
                                SubsidyId = subsidyCredit.SubsidyId,
                                ProductGroupId = productGroupCredit.ProductGroupId,
                                Id = shoppingCartItem.Id
                            };

                            orderItems.Add(orderItem);
                        }

                        if (productGroupCredit.AvailableQuantity >= quantiy)
                        {
                            productGroupCredit.AvailableQuantity -= quantiy;
                            orderItem.Quantity += quantiy;
                            return 0;
                        }
                        else
                        {
                            quantiy -= productGroupCredit.AvailableQuantity;
                            orderItem.Quantity += productGroupCredit.AvailableQuantity;
                            productGroupCredit.AvailableQuantity = 0;
                        }
                    }

                    if (productGroupCredit.AvailableQuantityTopup > 0)
                    {
                        var orderItem = orderItems.FirstOrDefault(x => x.SubsidyId == subsidyCredit.SubsidyId && x.ProductGroupId == productGroupCredit.ProductGroupId && x.Id == shoppingCartItem.Id);

                        if (orderItem == null)
                        {
                            orderItem = new SubsidyOrderItem
                            {
                                SubsidyId = subsidyCredit.SubsidyId,
                                ProductGroupId = productGroupCredit.ProductGroupId,
                                Id = shoppingCartItem.Id
                            };

                            orderItems.Add(orderItem);
                        }

                        if (productGroupCredit.AvailableQuantityTopup >= quantiy)
                        {
                            productGroupCredit.AvailableQuantityTopup -= quantiy;
                            orderItem.QuantityTopup += quantiy;
                            return 0;
                        }
                        else
                        {
                            quantiy -= productGroupCredit.AvailableQuantityTopup;
                            orderItem.QuantityTopup += productGroupCredit.AvailableQuantityTopup;
                            productGroupCredit.AvailableQuantityTopup = 0;
                        }
                    }
                }

                if (quantiy == 0)
                    return 0;
            }
        }

        return quantiy;
    }

    public decimal ApplyPackNameSubsidyCredit(PackNameCredit packNameCredit, PackNameShoppingCartItem shoppingCartItem, decimal subTotalInclTax, IList<SubsidyOrderItem> orderItems)
    {
        if (packNameCredit != null)
        {
            foreach (var subsidyCredit in packNameCredit.SubsidyCredits)
            {
                foreach (var productGroupCredit in subsidyCredit.ProductGroupCredits.Where(x => x.ProductIds.Contains(shoppingCartItem.ProductId)))
                {
                    if (productGroupCredit.AvailableCredit > 0)
                    {
                        var orderItem = orderItems.FirstOrDefault(x => x.SubsidyId == subsidyCredit.SubsidyId && x.ProductGroupId == productGroupCredit.ProductGroupId && x.Id == shoppingCartItem.Id);

                        if (orderItem == null)
                        {
                            orderItem = new SubsidyOrderItem
                            {
                                SubsidyId = subsidyCredit.SubsidyId,
                                ProductGroupId = productGroupCredit.ProductGroupId,
                                Id = shoppingCartItem.Id
                            };

                            orderItems.Add(orderItem);
                        }

                        if (productGroupCredit.AvailableCredit >= subTotalInclTax)
                        {
                            productGroupCredit.AvailableCredit -= subTotalInclTax;
                            orderItem.ProductGroupCredit += subTotalInclTax;

                            return 0;
                        }
                        else
                        {
                            subTotalInclTax -= productGroupCredit.AvailableCredit;
                            orderItem.ProductGroupCredit += productGroupCredit.AvailableCredit;
                            productGroupCredit.AvailableCredit = 0;
                        }
                    }

                    if (productGroupCredit.AvailableCreditTopup > 0)
                    {
                        var orderItem = orderItems.FirstOrDefault(x => x.SubsidyId == subsidyCredit.SubsidyId && x.ProductGroupId == productGroupCredit.ProductGroupId && x.Id == shoppingCartItem.Id);

                        if (orderItem == null)
                        {
                            orderItem = new SubsidyOrderItem
                            {
                                SubsidyId = subsidyCredit.SubsidyId,
                                ProductGroupId = productGroupCredit.ProductGroupId,
                                Id = shoppingCartItem.Id
                            };

                            orderItems.Add(orderItem);
                        }

                        if (productGroupCredit.AvailableCreditTopup >= subTotalInclTax)
                        {
                            productGroupCredit.AvailableCreditTopup -= subTotalInclTax;
                            orderItem.ProductGroupCreditTopup += subTotalInclTax;

                            return 0;
                        }
                        else
                        {
                            subTotalInclTax -= productGroupCredit.AvailableCreditTopup;
                            orderItem.ProductGroupCreditTopup += productGroupCredit.AvailableCreditTopup;
                            productGroupCredit.AvailableCreditTopup = 0;
                        }
                    }
                }

                //If the packName can order only allocated products, then we should not allow to use the available credit for products outside the product groups
                if (subsidyCredit.OnlyAllocatedProducts && !subsidyCredit.ProductGroupCredits.Where(x => x.ProductIds.Contains(shoppingCartItem.ProductId)).Any())
                    continue;

                if (subsidyCredit.AvailableCredit > 0)
                {
                    var orderItem = orderItems.FirstOrDefault(x => x.SubsidyId == subsidyCredit.SubsidyId && x.ProductGroupId == null && x.Id == shoppingCartItem.Id);

                    if (orderItem == null)
                    {
                        orderItem = new SubsidyOrderItem
                        {
                            SubsidyId = subsidyCredit.SubsidyId,
                            ProductGroupId = null,
                            Id = shoppingCartItem.Id
                        };

                        orderItems.Add(orderItem);
                    }

                    if (subsidyCredit.AvailableCredit >= subTotalInclTax)
                    {
                        subsidyCredit.AvailableCredit -= subTotalInclTax;
                        orderItem.Credit += subTotalInclTax;

                        return 0;
                    }
                    else
                    {
                        subTotalInclTax -= subsidyCredit.AvailableCredit;
                        orderItem.Credit += subsidyCredit.AvailableCredit;
                        subsidyCredit.AvailableCredit = 0;
                    }
                }

                if (subsidyCredit.AvailableCreditTopup > 0)
                {
                    var orderItem = orderItems.FirstOrDefault(x => x.SubsidyId == subsidyCredit.SubsidyId && x.ProductGroupId == null && x.Id == shoppingCartItem.Id);

                    if (orderItem == null)
                    {
                        orderItem = new SubsidyOrderItem
                        {
                            SubsidyId = subsidyCredit.SubsidyId,
                            ProductGroupId = null,
                            Id = shoppingCartItem.Id
                        };

                        orderItems.Add(orderItem);
                    }

                    if (subsidyCredit.AvailableCreditTopup >= subTotalInclTax)
                    {
                        subsidyCredit.AvailableCreditTopup -= subTotalInclTax;
                        orderItem.CreditTopup += subTotalInclTax;

                        return 0;
                    }
                    else
                    {
                        subTotalInclTax -= subsidyCredit.AvailableCreditTopup;
                        orderItem.CreditTopup += subsidyCredit.AvailableCreditTopup;
                        subsidyCredit.AvailableCreditTopup = 0;
                    }
                }

                if (subTotalInclTax == 0)
                    return 0;
            }
        }

        return subTotalInclTax;
    }

    public decimal GetPackNameShippingRemaining(int packNameId, decimal packNameShippingTotalInclTaxt, IList<PackNameCredit> packNameCredits, IList<SubsidyOrderShipping> subsidyOrderShippings)
    {
        if (packNameShippingTotalInclTaxt == 0)
            return packNameShippingTotalInclTaxt;

        var packNameCredit = packNameCredits.FirstOrDefault(x => x.PackNameId == packNameId);

        if (packNameCredit != null)
        {
            foreach (var subsidyCredit in packNameCredit.SubsidyCredits)
            {
                if (packNameShippingTotalInclTaxt == 0)
                    return packNameShippingTotalInclTaxt;

                var orderShipping = new SubsidyOrderShipping
                {
                    PackNameId = packNameId,
                    SubsidyId = subsidyCredit.SubsidyId,
                };

                //If the subsidy credit is not used, we can use it to pay for the shipping
                if (subsidyCredit.AvailableCredit > 0)
                {
                    if (subsidyCredit.AvailableCredit >= packNameShippingTotalInclTaxt)
                    {
                        subsidyCredit.AvailableCredit -= packNameShippingTotalInclTaxt;
                        orderShipping.Credit = packNameShippingTotalInclTaxt;
                        packNameShippingTotalInclTaxt = 0;
                    }
                    else
                    {
                        packNameShippingTotalInclTaxt -= subsidyCredit.AvailableCredit;
                        orderShipping.Credit = subsidyCredit.AvailableCredit;
                        subsidyCredit.AvailableCredit = 0;
                    }
                }

                if (subsidyCredit.AvailableCreditTopup > 0)
                {
                    if (subsidyCredit.AvailableCreditTopup >= packNameShippingTotalInclTaxt)
                    {
                        subsidyCredit.AvailableCreditTopup -= packNameShippingTotalInclTaxt;
                        orderShipping.CreditTopup = packNameShippingTotalInclTaxt;
                        packNameShippingTotalInclTaxt = 0;
                    }
                    else
                    {
                        packNameShippingTotalInclTaxt -= subsidyCredit.AvailableCreditTopup;
                        orderShipping.CreditTopup = subsidyCredit.AvailableCreditTopup;
                        subsidyCredit.AvailableCreditTopup = 0;
                    }
                }

                if (subsidyOrderShippings != null && (orderShipping.Credit > 0 || orderShipping.CreditTopup > 0))
                    subsidyOrderShippings.Add(orderShipping);
            }
        }

        return packNameShippingTotalInclTaxt;
    }

    public async Task<IList<SubsidyOrderItem>> GetSubsidyOrderItemsByOrderIdAsync(int orderId)
    {
        return await _subsidyOrderItemRepository.GetAllAsync(q => q.Where(x => x.OrderId == orderId));
    }

    public async Task InsertSubsidyOrderItemAsync(SubsidyOrderItem subsidyOrderItem)
    {
        ArgumentNullException.ThrowIfNull(subsidyOrderItem);

        await _subsidyOrderItemRepository.InsertAsync(subsidyOrderItem);

        _packNameCredits.Clear();
    }

    public async Task<IList<SubsidyOrderShipping>> GetSubsidyOrderShippingsAsync(int orderId)
    {
        var query = from oi in _subsidyOrderShippingRepository.Table
                    where oi.OrderId == orderId
                    select oi;

        return await query.ToListAsync();
    }

    public async Task InsertSubsidyOrderShippingAsync(SubsidyOrderShipping subsidyOrderShipping)
    {
        ArgumentNullException.ThrowIfNull(subsidyOrderShipping);

        await _subsidyOrderShippingRepository.InsertAsync(subsidyOrderShipping);

        _packNameCredits.Clear();
    }

    /// <summary>
    /// Forgets the packname credit read so far in this request, so the next read goes to the database
    /// </summary>
    public void ClearPackNameCredits()
    {
        _packNameCredits.Clear();
    }

    /// <summary>
    /// Restores one returned order item's subsidy consumption, bucket for bucket.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Two guarantees govern this: never restore more than was consumed, and never restore the same thing
    /// twice. Both are enforced per bucket rather than against one money total, because a money total
    /// cannot express units.
    /// </para>
    /// <para>
    /// The arithmetic is a <b>cumulative target</b>, not an increment: the target for each column is
    /// what the whole returned quantity is owed, and the row written is that target minus what has
    /// already gone back. Incrementing per return would round each partial separately and strand a cent
    /// on the last one. When the cumulative quantity reaches the ordered quantity the target is the
    /// consumed figure exactly, so a fully returned item always restores to precisely zero net.
    /// </para>
    /// <para>
    /// Targets are worked out in two passes over the buckets in <b>consumption order</b> (ordered by Id,
    /// which is the order the allocator filled them: a product group's units, then its credit, then
    /// global credit), each pass spending one shared budget. Pass one hands back whole units — price is
    /// irrelevant to a unit, so a partial return owed two thirds of a unit gets a
    /// whole one. Pass two hands back money, but only up to the value those whole units did NOT already
    /// cover. Both budgets are shared across every bucket, which is what stops two unit buckets from
    /// each restoring the full returned quantity, and what stops a rounded-up unit being paid for twice
    /// — once in kind and again pro-rata in cash.
    /// </para>
    /// <para>
    /// The money equivalent on the result values a unit at the order item's unit price incl tax, which
    /// is how the order screens value a unit the order consumed. Because both budgets derive from
    /// that same unit price, the equivalent can never exceed the value of what came back.
    /// </para>
    /// The packname credit read in this request is dropped after writing, so an add-to-cart in the same
    /// request (the replacement being registered right after this) sees the restored units.
    /// </remarks>
    public virtual async Task<SubsidyRestoreResult> RestoreSubsidyForReturnedItemAsync(SubsidyRestoreRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        //Restoration is keyed on the case line. Without one there is nothing to make it idempotent,
        //and a re-approval would restore a second time.
        if (request.ReturnCaseLineId <= 0 || request.OrderedQuantity <= 0 || request.ReturnedQuantity <= 0)
            return SubsidyRestoreResult.Empty;

        var written = 0;
        var unitsRestored = 0;
        var creditRestored = decimal.Zero;
        var unitPriceInclTax = decimal.Zero;

        //Serialised per order item, for the same reason ApplyCreditNotesForOrderAsync is serialised per
        //customer: what has already been restored is read from rows, so two authorisations against the
        //same order item would both read the pre-write state and both restore. Keyed on the order item
        //rather than the customer because that is the narrowest thing two returns can contend over.
        var lockAcquired = await _locker.PerformActionWithLockAsync(
            $"subsidy_restore_orderitem_{request.OrderItemId}",
            TimeSpan.FromSeconds(30),
            async () =>
            {
                //A line can execute more than once — undo then re-approve, or a re-opened case.
                //Clearing this line's own rows first is what makes the run below idempotent, and it
                //is why the cumulative sums that follow do not count this line twice.
                await DeleteSubsidyRestoreItemsForCaseLineAsync(request.ReturnCaseLineId);

                //Ordered by Id: that is the order the allocator wrote them, so it is the order they were
                //consumed in, and both passes below walk it.
                var consumptions = (await _subsidyOrderItemRepository
                        .GetAllAsync(query => query.Where(x => x.OrderItemId == request.OrderItemId)))
                    .OrderBy(x => x.Id)
                    .ToList();

                if (consumptions.Count == 0)
                    return;

                var orderItem = await _orderService.GetOrderItemByIdAsync(request.OrderItemId);

                unitPriceInclTax = orderItem?.UnitPriceInclTax ?? decimal.Zero;

                var existing = await _subsidyRestoreItemRepository
                    .GetAllAsync(query => query.Where(x => x.OrderItemId == request.OrderItemId));

                //Grouped by case line, never summed across rows. One return writes one row per
                //consumption bucket and every one of them carries the same ReturnedQuantity, so an order
                //item funded from two buckets — a product group's credit running out and global credit
                //taking the remainder, which is the ordinary shape — would otherwise count its returned
                //quantity once per bucket, and the second partial return would read as the whole item
                //coming back.
                var previouslyReturnedQuantity = existing
                    .GroupBy(x => x.ReturnCaseLineId)
                    .Sum(caseLine => caseLine.Max(row => row.ReturnedQuantity));

                var cumulativeQuantity = Math.Min(
                    previouslyReturnedQuantity + request.ReturnedQuantity,
                    request.OrderedQuantity);

                var isWholeItem = cumulativeQuantity >= request.OrderedQuantity;

                //--- pass one: whole units, in consumption order, against one shared budget ---------
                var unitTargets = new (int Quantity, int QuantityTopup)[consumptions.Count];
                var unitBudget = cumulativeQuantity;

                for (var i = 0; i < consumptions.Count; i++)
                {
                    var consumption = consumptions[i];

                    if (isWholeItem)
                    {
                        unitTargets[i] = (consumption.Quantity, consumption.QuantityTopup);
                        continue;
                    }

                    var quantity = Math.Max(0, Math.Min(consumption.Quantity, unitBudget));
                    unitBudget -= quantity;

                    var quantityTopup = Math.Max(0, Math.Min(consumption.QuantityTopup, unitBudget));
                    unitBudget -= quantityTopup;

                    unitTargets[i] = (quantity, quantityTopup);
                }

                //--- pass two: money, limited to the value the whole units did not already cover -----
                var unitsTargeted = unitTargets.Sum(target => target.Quantity + target.QuantityTopup);

                //Floored at zero: rounding a unit up can cover MORE than the returned quantity is owed,
                //and when it does there is nothing left for the money buckets.
                var valueBudget = isWholeItem
                    ? decimal.Zero
                    : Math.Max(
                        decimal.Zero,
                        Math.Round(
                            (cumulativeQuantity - unitsTargeted) * unitPriceInclTax,
                            2,
                            MidpointRounding.AwayFromZero));

                decimal TakeMoney(decimal consumed)
                {
                    var take = Math.Max(decimal.Zero, Math.Min(consumed, valueBudget));
                    valueBudget -= take;
                    return take;
                }

                var moneyTargets =
                    new (decimal Credit, decimal CreditTopup, decimal ProductGroupCredit, decimal ProductGroupCreditTopup)[consumptions.Count];

                for (var i = 0; i < consumptions.Count; i++)
                {
                    var consumption = consumptions[i];

                    if (isWholeItem)
                    {
                        moneyTargets[i] = (
                            consumption.Credit,
                            consumption.CreditTopup,
                            consumption.ProductGroupCredit,
                            consumption.ProductGroupCreditTopup);
                        continue;
                    }

                    //Same order the allocator spends them in: the product group's credit, then its
                    //topup, then global credit, then the global topup.
                    var productGroupCredit = TakeMoney(consumption.ProductGroupCredit);
                    var productGroupCreditTopup = TakeMoney(consumption.ProductGroupCreditTopup);
                    var credit = TakeMoney(consumption.Credit);
                    var creditTopup = TakeMoney(consumption.CreditTopup);

                    moneyTargets[i] = (credit, creditTopup, productGroupCredit, productGroupCreditTopup);
                }

                //--- write the difference between each target and what has already gone back ---------
                for (var i = 0; i < consumptions.Count; i++)
                {
                    var consumption = consumptions[i];

                    var done = existing.Where(x => x.SubsidyOrderItemId == consumption.Id).ToList();

                    var restore = new SubsidyRestoreItem
                    {
                        OrderId = consumption.OrderId,
                        OrderItemId = consumption.OrderItemId,
                        SubsidyId = consumption.SubsidyId,
                        ProductGroupId = consumption.ProductGroupId,
                        SubsidyOrderItemId = consumption.Id,
                        ReturnCaseLineId = request.ReturnCaseLineId,
                        ReturnRequestId = request.ReturnRequestId,
                        ReturnedQuantity = request.ReturnedQuantity,
                        CreatedByCustomerId = request.ActingCustomerId,
                        CreatedOnUtc = DateTime.UtcNow,

                        Quantity = Math.Max(0, unitTargets[i].Quantity - done.Sum(x => x.Quantity)),
                        QuantityTopup = Math.Max(0, unitTargets[i].QuantityTopup - done.Sum(x => x.QuantityTopup)),

                        Credit = Math.Max(decimal.Zero, moneyTargets[i].Credit - done.Sum(x => x.Credit)),
                        CreditTopup = Math.Max(decimal.Zero, moneyTargets[i].CreditTopup - done.Sum(x => x.CreditTopup)),
                        ProductGroupCredit = Math.Max(decimal.Zero, moneyTargets[i].ProductGroupCredit - done.Sum(x => x.ProductGroupCredit)),
                        ProductGroupCreditTopup = Math.Max(decimal.Zero, moneyTargets[i].ProductGroupCreditTopup - done.Sum(x => x.ProductGroupCreditTopup)),
                    };

                    //Nothing left in this bucket — an earlier partial return already gave it all back.
                    //ReturnedQuantity therefore goes unrecorded for a line whose every bucket is spent.
                    //That is safe: a lower cumulative can only lower a target, and every column floors
                    //at zero, so it can only under-restore something that is already fully restored.
                    if (restore.Quantity == 0 && restore.QuantityTopup == 0 &&
                        restore.Credit == decimal.Zero && restore.CreditTopup == decimal.Zero &&
                        restore.ProductGroupCredit == decimal.Zero && restore.ProductGroupCreditTopup == decimal.Zero)
                        continue;

                    await _subsidyRestoreItemRepository.InsertAsync(restore);

                    written++;
                    unitsRestored += restore.Quantity + restore.QuantityTopup;
                    creditRestored += restore.Credit + restore.CreditTopup + restore.ProductGroupCredit + restore.ProductGroupCreditTopup;
                }

                if (written > 0)
                    _packNameCredits.Clear();
            });

        if (!lockAcquired)
            throw new NopException($"Could not restore subsidy for order item {request.OrderItemId}: another operation is already restoring it. Please try again.");

        return new SubsidyRestoreResult
        {
            RowsWritten = written,
            UnitsRestored = unitsRestored,
            CreditRestored = creditRestored,
            MoneyEquivalent = creditRestored + unitsRestored * unitPriceInclTax,
        };
    }

    public virtual async Task DeleteSubsidyRestoreItemsForCaseLineAsync(int returnCaseLineId)
    {
        if (returnCaseLineId <= 0)
            return;

        var rows = await _subsidyRestoreItemRepository
            .GetAllAsync(query => query.Where(x => x.ReturnCaseLineId == returnCaseLineId));

        if (rows.Count == 0)
            return;

        await _subsidyRestoreItemRepository.DeleteAsync(rows);

        _packNameCredits.Clear();
    }

    #endregion
}
