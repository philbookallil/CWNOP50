using AwesomeAssertions;
using Nop.Plugin.CW.Core.Helpers;
using Nop.Plugin.CW.Core.Services.Settlement;
using NUnit.Framework;

namespace CW.Tests.Core.Helpers;

/// <summary>
/// The settlement row kept on an order: built once from the plan the pipeline applied, carrying the plan's
/// exact money so that no reader re-values units or re-derives the gross from the ledgers.
/// </summary>
[TestFixture]
public class OrderSettlementHelperTests
{
    #region Utilities

    private static readonly DateTime Now = new(2026, 10, 2, 10, 0, 0, DateTimeKind.Utc);

    private static SettlementLine Subsidy(decimal amount, decimal shipping = 0m) => new()
    {
        SourceName = SettlementSourceNames.SUBSIDY,
        Priority = 1,
        Amount = amount,
        ShippingAmount = shipping,
    };

    private static SettlementLine CreditNotes(decimal amount) => new()
    {
        SourceName = SettlementSourceNames.CREDIT_NOTES,
        Priority = 2,
        Amount = amount,
    };

    private static SettlementPlan Plan(decimal settleable, params SettlementLine[] lines) => new()
    {
        SettleableAmount = settleable,
        Lines = lines,
    };

    #endregion

    [Test]
    public void NothingCovered_NoRow()
    {
        OrderSettlementHelper.FromPlan(1, Plan(100m), Now).Should().BeNull();
    }

    [Test]
    public void EmptyPlan_NoRow()
    {
        OrderSettlementHelper.FromPlan(1, new SettlementPlan(), Now).Should().BeNull();
    }

    [Test]
    public void NoOrder_NoRow()
    {
        OrderSettlementHelper.FromPlan(0, Plan(100m, Subsidy(100m)), Now).Should().BeNull();
    }

    [Test]
    public void NullPlan_Throws()
    {
        var act = () => OrderSettlementHelper.FromPlan(1, null, Now);

        act.Should().Throw<ArgumentNullException>();
    }

    [Test]
    public void SubsidyOnly_StockAndShippingSplit()
    {
        var row = OrderSettlementHelper.FromPlan(7, Plan(130m, Subsidy(110m, shipping: 10m)), Now);

        row.Should().NotBeNull();
        row!.OrderId.Should().Be(7);
        row.SettleableAmount.Should().Be(130m);
        row.SubsidyStockAmount.Should().Be(100m);
        row.SubsidyShippingAmount.Should().Be(10m);
        row.CreditNoteAmount.Should().Be(0m);
        row.CreatedOnUtc.Should().Be(Now);
    }

    [Test]
    public void SubsidyThenCreditNotes_BothRecorded()
    {
        var row = OrderSettlementHelper.FromPlan(7, Plan(100m, Subsidy(60m), CreditNotes(25m)), Now);

        row.Should().NotBeNull();
        row!.SubsidyStockAmount.Should().Be(60m);
        row.SubsidyShippingAmount.Should().Be(0m);
        row.CreditNoteAmount.Should().Be(25m);
        (row.SettleableAmount - row.SubsidyStockAmount - row.SubsidyShippingAmount - row.CreditNoteAmount).Should().Be(15m, "the balance due is what the row leaves");
    }

    [Test]
    public void CreditNotesOnly_PlainOrder()
    {
        var row = OrderSettlementHelper.FromPlan(7, Plan(40m, CreditNotes(40m)), Now);

        row.Should().NotBeNull();
        row!.SubsidyStockAmount.Should().Be(0m);
        row.SubsidyShippingAmount.Should().Be(0m);
        row.CreditNoteAmount.Should().Be(40m);
    }

    [Test]
    public void ThreeDecimalMoney_KeptExact()
    {
        var row = OrderSettlementHelper.FromPlan(7, Plan(30.003m, Subsidy(30.003m)), Now);

        row!.SubsidyStockAmount.Should().Be(30.003m);
    }

    #region LimitDiscount

    [Test]
    public void Discount_WithinBalance_Unchanged()
    {
        OrderSettlementHelper.LimitDiscount(10m, 9.09m, 40m).Should().Be((10m, 9.09m));
    }

    [Test]
    public void Discount_EqualToBalance_Unchanged()
    {
        OrderSettlementHelper.LimitDiscount(10m, 9.09m, 10m).Should().Be((10m, 9.09m));
    }

    [Test]
    public void Discount_OverBalance_LimitedToIt_ExclTaxInProportion()
    {
        // goods 100, subsidy 95: the customer owes 5, so only 5 of a 10 discount can apply
        var (inclTax, exclTax) = OrderSettlementHelper.LimitDiscount(10m, 8m, 5m);

        inclTax.Should().Be(5m);
        exclTax.Should().Be(4m);
    }

    [Test]
    public void NothingLeftToPay_NothingApplied()
    {
        OrderSettlementHelper.LimitDiscount(10m, 9.09m, 0m).Should().Be((0m, 0m));
    }

    [Test]
    public void NegativeBalance_NothingApplied()
    {
        OrderSettlementHelper.LimitDiscount(10m, 9.09m, -1m).Should().Be((0m, 0m));
    }

    [Test]
    public void NoDiscount_NothingApplied()
    {
        OrderSettlementHelper.LimitDiscount(0m, 0m, 50m).Should().Be((0m, 0m));
    }

    #endregion
}
