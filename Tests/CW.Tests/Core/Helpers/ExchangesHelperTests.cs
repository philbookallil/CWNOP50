using AwesomeAssertions;
using Nop.Core.Domain.Orders;
using Nop.Plugin.CW.Core.Domain.Exchanges;
using Nop.Plugin.CW.Core.Helpers;
using Nop.Plugin.CW.Core.Models.Exchanges.FeeRule;
using NUnit.Framework;

namespace CW.Tests.Core.Helpers;

/// <summary>
/// The fee arithmetic, which is the only place in the system that turns a rule into money.
/// </summary>
/// <remarks>
/// Worth testing above everything else for two reasons. It is frozen: ReturnResolutionLogic writes the
/// result into ReturnCharge rows at resolution, so a wrong figure outlives the mistake and is what the
/// customer is charged. And it is duplicated three ways — the FeeRule overload, the FeeConceptItem
/// overload, and a hand-written mirror in _CreateOrUpdate.FeesAndCharges.cshtml's JavaScript — so the
/// two C# copies are pinned against each other here.
/// </remarks>
[TestFixture]
public class ExchangesHelperTests
{
    #region Utilities

    private static FeeRule PerUnits(decimal amount, int unitsQuantity, FeeType feeType = FeeType.ReShippingFee)
        => new() { Amount = amount, Operator = FeeRuleOperator.PerUnitsQuantity, UnitsQuantity = unitsQuantity, FeeType = feeType };

    private static FeeRule FixedPrice(decimal amount, FeeType feeType = FeeType.ReShippingFee)
        => new() { Amount = amount, Operator = FeeRuleOperator.FixedPrice, FeeType = feeType };

    #endregion

    #region CalculateFeeAmount - banding

    /// <summary>
    /// The worked example from Exchanges_Target_Design.md §3: Redelivery $5.00, Per Units, band of 4.
    /// </summary>
    [TestCase(0, 0.00)]
    [TestCase(1, 5.00)]
    [TestCase(4, 5.00)]
    [TestCase(5, 10.00)]
    [TestCase(8, 10.00)]
    [TestCase(9, 15.00)]
    public void CalculateFeeAmount_PerUnits_RoundsUpToTheNextBand(int quantity, double expected)
    {
        //double, not decimal: a [TestCase] argument is an attribute constant, and decimal is not a legal
        //attribute constant type. Cast at the assertion instead.
        ExchangesHelper.CalculateFeeAmount(PerUnits(5.00m, 4), quantity)
            .Should().Be((decimal)expected);
    }

    /// <summary>
    /// A band of 1 is linear, which is why repackaging (decision 9, per garment) never diverges between
    /// the per-line charge and the customer's per-case estimate. Only bands above 1 do — see §7.3.
    /// </summary>
    [TestCase(1, 3.00)]
    [TestCase(2, 6.00)]
    [TestCase(5, 15.00)]
    public void CalculateFeeAmount_PerUnitsWithBandOfOne_IsLinear(int quantity, double expected)
    {
        ExchangesHelper.CalculateFeeAmount(PerUnits(3.00m, 1, FeeType.RepackagingFee), quantity)
            .Should().Be((decimal)expected);
    }

    /// <summary>
    /// Decision 30 rests on this: pricing per line can only ever be greater than or equal to pricing the
    /// same units once over the whole case, because ceiling division is subadditive. That is what makes
    /// the per-line figure a safe PRE-ADJUSTMENT starting point for Sales Support to bring down, and why
    /// the customer's per-case estimate can never exceed what they are charged.
    /// </summary>
    [TestCase(1, 1)]
    [TestCase(2, 2)]
    [TestCase(2, 3)]
    [TestCase(3, 5)]
    [TestCase(7, 9)]
    public void CalculateFeeAmount_PerUnits_PerLineIsNeverCheaperThanPerCase(int lineA, int lineB)
    {
        var rule = PerUnits(5.00m, 4);

        var perLine = ExchangesHelper.CalculateFeeAmount(rule, lineA)
                    + ExchangesHelper.CalculateFeeAmount(rule, lineB);
        var perCase = ExchangesHelper.CalculateFeeAmount(rule, lineA + lineB);

        perLine.Should().BeGreaterThanOrEqualTo(perCase);
    }

    #endregion

    #region CalculateFeeAmount - the zero-band guard

    /// <summary>
    /// A rule with no band is not a band. Unguarded, Math.Ceiling over a zero divisor yields a figure
    /// that would be written into a ReturnCharge row and outlive the mistake — this guard is the reason
    /// that cannot happen, so it is pinned rather than left to the comment beside it.
    /// </summary>
    [TestCase(0)]
    [TestCase(-1)]
    [TestCase(int.MinValue)]
    public void CalculateFeeAmount_PerUnitsWithNoBand_ChargesNothing(int unitsQuantity)
    {
        ExchangesHelper.CalculateFeeAmount(PerUnits(5.00m, unitsQuantity), 10)
            .Should().Be(decimal.Zero);
    }

    #endregion

    #region CalculateFeeAmount - fixed price

    [TestCase(0, 0.00)]
    [TestCase(1, 7.50)]
    [TestCase(99, 7.50)]
    public void CalculateFeeAmount_FixedPrice_ChargesOnceForAnyQuantityAboveZero(int quantity, double expected)
    {
        ExchangesHelper.CalculateFeeAmount(FixedPrice(7.50m), quantity)
            .Should().Be((decimal)expected);
    }

    /// <summary>
    /// Zeroing the quantity box is how Sales Support drop a fee from a line (decision 30). It has to work
    /// for a fixed-price rule too, not only a banded one.
    /// </summary>
    [Test]
    public void CalculateFeeAmount_FixedPriceAtZeroQuantity_IsHowStaffDropAFee()
    {
        ExchangesHelper.CalculateFeeAmount(FixedPrice(7.50m), 0).Should().Be(decimal.Zero);
    }

    #endregion

    #region The two overloads must agree

    /// <summary>
    /// FeeConceptItem is built from a FeeRule and priced by a second, separately written copy of the same
    /// arithmetic. The customer's wizard summary uses that copy; the charge they actually pay uses the
    /// other. They must never disagree.
    /// </summary>
    [TestCase(FeeRuleOperator.PerUnitsQuantity, 5.00, 4, 5)]
    [TestCase(FeeRuleOperator.PerUnitsQuantity, 5.00, 4, 9)]
    [TestCase(FeeRuleOperator.PerUnitsQuantity, 3.00, 1, 5)]
    [TestCase(FeeRuleOperator.PerUnitsQuantity, 5.00, 0, 5)]
    [TestCase(FeeRuleOperator.FixedPrice, 7.50, 0, 3)]
    [TestCase(FeeRuleOperator.FixedPrice, 7.50, 0, 0)]
    public void CalculateFeeAmount_FeeRuleAndFeeConceptItem_PriceIdentically(
        FeeRuleOperator op, double amount, int unitsQuantity, int quantity)
    {
        var rule = new FeeRule
        {
            Amount = (decimal)amount,
            Operator = op,
            UnitsQuantity = unitsQuantity,
            FeeType = FeeType.ReShippingFee
        };

        var concept = new FeeConceptItem(rule, "Redelivery Fee");

        ExchangesHelper.CalculateFeeAmount(concept, quantity)
            .Should().Be(ExchangesHelper.CalculateFeeAmount(rule, quantity));
    }

    #endregion

    #region DetermineFeeRuleQuantity

    /// <summary>
    /// With no staff override the fee is priced over the return request's own quantity.
    /// </summary>
    [Test]
    public void DetermineFeeRuleQuantity_NoStaffOverride_UsesTheReturnedQuantity()
    {
        var request = new ReturnRequest { Quantity = 3 };
        var item = new ReturnRequestItem();

        ExchangesHelper.DetermineFeeRuleQuantity(PerUnits(5m, 4, FeeType.ReShippingFee), request, item).Should().Be(3);
        ExchangesHelper.DetermineFeeRuleQuantity(PerUnits(3m, 1, FeeType.RepackagingFee), request, item).Should().Be(3);
    }

    /// <summary>
    /// Each fee type reads its OWN staff box, and only its own. This is the mechanism decision 30 depends
    /// on — staff bring the per-line figure down to the real one by editing these.
    /// </summary>
    [Test]
    public void DetermineFeeRuleQuantity_StaffOverride_AppliesToItsOwnFeeTypeOnly()
    {
        var request = new ReturnRequest { Quantity = 3 };
        var item = new ReturnRequestItem { StaffShipQty = 0, StaffRepackQty = 2 };

        ExchangesHelper.DetermineFeeRuleQuantity(PerUnits(5m, 4, FeeType.ReShippingFee), request, item)
            .Should().Be(0, "the shipping box is what zeroes a redelivery fee on this line");

        ExchangesHelper.DetermineFeeRuleQuantity(PerUnits(3m, 1, FeeType.RepackagingFee), request, item)
            .Should().Be(2, "the repackaging box is separate and was set to 2");
    }

    /// <summary>
    /// A box left unset falls back to the returned quantity rather than to the other box.
    /// </summary>
    [Test]
    public void DetermineFeeRuleQuantity_OnlyOneBoxSet_LeavesTheOtherFeeOnTheReturnedQuantity()
    {
        var request = new ReturnRequest { Quantity = 3 };
        var item = new ReturnRequestItem { StaffShipQty = 1, StaffRepackQty = null };

        ExchangesHelper.DetermineFeeRuleQuantity(PerUnits(3m, 1, FeeType.RepackagingFee), request, item)
            .Should().Be(3);
    }

    /// <summary>
    /// The ad-hoc fee has no staff quantity box — it is an amount staff type — so it is always priced
    /// over the returned quantity whatever the other boxes say.
    /// </summary>
    [Test]
    public void DetermineFeeRuleQuantity_AdditionalFee_IgnoresBothStaffBoxes()
    {
        var request = new ReturnRequest { Quantity = 3 };
        var item = new ReturnRequestItem { StaffShipQty = 0, StaffRepackQty = 0 };

        ExchangesHelper.DetermineFeeRuleQuantity(FixedPrice(10m, FeeType.AdditionalFee), request, item)
            .Should().Be(3);
    }

    #endregion
}
