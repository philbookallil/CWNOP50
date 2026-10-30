using Nop.Plugin.CW.Core.Domain.Orders;
using Nop.Plugin.CW.Core.Services.Settlement;

namespace Nop.Plugin.CW.Core.Helpers;

/// <summary>
/// Turns a settlement plan into the row kept on the order. Pure — no database.
/// </summary>
public static class OrderSettlementHelper
{
    /// <summary>
    /// The settlement row for a plan the pipeline applied, or null when no pool covered anything.
    /// </summary>
    /// <param name="orderId">The order the plan was applied to</param>
    /// <param name="plan">The applied plan</param>
    /// <param name="createdOnUtc">When the row is written</param>
    /// <returns>The row, or null when there is nothing to record</returns>
    public static OrderSettlement FromPlan(int orderId, SettlementPlan plan, DateTime createdOnUtc)
    {
        ArgumentNullException.ThrowIfNull(plan);

        if (orderId <= 0 || plan.TotalSettled <= decimal.Zero)
            return null;

        var subsidyShipping = plan.ShippingAmountFor(SettlementSourceNames.SUBSIDY);

        return new OrderSettlement
        {
            OrderId = orderId,
            SettleableAmount = plan.SettleableAmount,
            SubsidyStockAmount = plan.AmountFor(SettlementSourceNames.SUBSIDY) - subsidyShipping,
            SubsidyShippingAmount = subsidyShipping,
            CreditNoteAmount = plan.AmountFor(SettlementSourceNames.CREDIT_NOTES),
            CreatedOnUtc = createdOnUtc,
        };
    }

    /// <summary>
    /// Limits a discount to the balance it comes off. The excluding-tax figure keeps the same proportion to the
    /// including-tax one.
    /// </summary>
    /// <param name="discountInclTax">The full discount, including tax</param>
    /// <param name="discountExclTax">The full discount, excluding tax</param>
    /// <param name="balanceDue">What is left to pay before the discount, including tax</param>
    /// <returns>The discount applied, including and excluding tax; zero when nothing is left to pay</returns>
    public static (decimal InclTax, decimal ExclTax) LimitDiscount(decimal discountInclTax, decimal discountExclTax, decimal balanceDue)
    {
        if (discountInclTax <= decimal.Zero || balanceDue <= decimal.Zero)
            return (decimal.Zero, decimal.Zero);

        if (discountInclTax <= balanceDue)
            return (discountInclTax, discountExclTax);

        return (balanceDue, discountExclTax * balanceDue / discountInclTax);
    }
}