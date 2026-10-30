using Nop.Core;
using Nop.Plugin.CW.Core.Domain.Settings;
using Nop.Plugin.CW.Core.Domain.Subsidies;
using Nop.Plugin.CW.Core.Domain.Subsidies.DTOs;
using Nop.Plugin.CW.Core.Services.Settings;
using Nop.Plugin.CW.Core.Services.Subsidies;

namespace Nop.Plugin.CW.Core.Services.Settlement;

/// <summary>
/// Subsidy as a settlement source. Priority 1 — the company's entitlement is spent before the customer's
/// own store credit: units and money alike, and the employer's subsidy
/// before a manager's own credit notes). The allocation itself lives in ISubsidyAllocationService; this
/// type only adapts it to the settlement contract and persists the rows on the apply path.
/// </summary>
public class SubsidySettlementSource : ISettlementSource
{
    #region Fields

    protected readonly ISubsidyAllocationService _subsidyAllocationService;
    protected readonly ISubsidyCreditService _subsidyCreditService;
    protected readonly ICWFeatureService _cwFeatureService;

    #endregion

    #region Ctor

    public SubsidySettlementSource(
        ISubsidyAllocationService subsidyAllocationService,
        ISubsidyCreditService subsidyCreditService,
        ICWFeatureService cwFeatureService)
    {
        _subsidyAllocationService = subsidyAllocationService;
        _subsidyCreditService = subsidyCreditService;
        _cwFeatureService = cwFeatureService;
    }

    #endregion

    #region Utilities

    protected virtual Task<SubsidyAllocation> AllocateAsync(SettlementContext context, decimal amount)
    {
        return _subsidyAllocationService.AllocateAsync(new SubsidyAllocationRequest
        {
            Customer = context.Customer,
            StoreId = context.StoreId,
            Cart = context.PackNameCart,
            ShippingPrices = context.ShippingPrices,
            BudgetInclTax = amount,
        });
    }

    protected virtual SettlementLine ToLine(SubsidyAllocation allocation) => new()
    {
        SourceName = Name,
        Priority = Priority,
        Amount = allocation.StockInclTax + allocation.ShippingInclTax,
        ShippingAmount = allocation.ShippingInclTax,
    };

    #endregion

    #region Methods

    public string Name => SettlementSourceNames.SUBSIDY;

    public int Priority => 1;

    public virtual async Task<bool> IsEnabledAsync(SettlementContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        // No packname cart means nothing to allocate against — and it is also what makes a
        // return-derived order safe. ICWFeatureService applies the Subsidy-requires-PackName implication
        // itself (CWFeatureSet.From), so do not read MultiStoreSettings directly here.
        if (context.PackNameCart.Count == 0)
            return false;

        return context.Order != null
            ? await _cwFeatureService.IsEnabledForOrderAsync(CWFeature.Subsidy, context.Order)
            : await _cwFeatureService.IsEnabledAsync(CWFeature.Subsidy, context.StoreId);
    }

    public virtual async Task<SettlementLine> QuoteAsync(SettlementContext context, decimal amount)
    {
        ArgumentNullException.ThrowIfNull(context);

        return ToLine(await AllocateAsync(context, amount));
    }

    public virtual async Task<SettlementLine> ApplyAsync(SettlementContext context, decimal amount)
    {
        ArgumentNullException.ThrowIfNull(context);

        var order = context.Order
            ?? throw new NopException($"The {Name} settlement source needs an order to apply against.");

        var allocation = await AllocateAsync(context, amount);

        foreach (var item in allocation.Items)
        {
            // SubsidyAllocation reuses SubsidyOrderItem.Id to carry the ShoppingCartItemId — the rows
            // are projections, not entities, until they are copied onto the order here.
            if (!context.OrderItemIdByCartItemId.TryGetValue(item.Id, out var orderItemId))
                throw new NopException($"Subsidy settlement: no order item was created for shopping cart item {item.Id}.");

            await _subsidyCreditService.InsertSubsidyOrderItemAsync(new SubsidyOrderItem
            {
                OrderId = order.Id,
                OrderItemId = orderItemId,
                SubsidyId = item.SubsidyId,
                ProductGroupId = item.ProductGroupId,
                Credit = item.Credit,
                CreditTopup = item.CreditTopup,
                ProductGroupCredit = item.ProductGroupCredit,
                ProductGroupCreditTopup = item.ProductGroupCreditTopup,
                Quantity = item.Quantity,
                QuantityTopup = item.QuantityTopup,
            });
        }

        foreach (var shipping in allocation.ShippingCredits)
        {
            await _subsidyCreditService.InsertSubsidyOrderShippingAsync(new SubsidyOrderShipping
            {
                OrderId = order.Id,
                PackNameId = shipping.PackNameId,
                SubsidyId = shipping.SubsidyId,
                Credit = shipping.Credit,
                CreditTopup = shipping.CreditTopup,
            });
        }

        return ToLine(allocation);
    }

    #endregion
}