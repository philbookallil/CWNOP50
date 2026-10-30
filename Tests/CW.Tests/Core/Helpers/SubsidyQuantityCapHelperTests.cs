using AwesomeAssertions;
using Nop.Plugin.CW.Core.Domain.Subsidies.DTOs;
using Nop.Plugin.CW.Core.Helpers;
using NUnit.Framework;

namespace CW.Tests.Core.Helpers;

/// <summary>
/// The allocated-quantity cap as a rule: which subsidies decide, what lifts the cap, and what the cart
/// already takes.
/// </summary>
/// <remarks>
/// <para>
/// <b>Only the subsidies containing the product decide.</b> The cap used to be lifted by any unrestricted
/// subsidy of the packname, even one that did not contain the product, and applied whatever credit remained.
/// </para>
/// <para>
/// <b>The whole cart counts.</b> The cap used to compare only the same line: with a group of two products
/// and a quantity of 2, two of each were accepted — four units against an allocation of two.
/// </para>
/// </remarks>
[TestFixture]
public class SubsidyQuantityCapHelperTests
{
    #region Utilities

    private const int ProductA = 101;
    private const int ProductB = 102;
    private const int ProductC = 103;

    private const int ShirtsGroup = 1;
    private const int PantsGroup = 2;

    private static readonly IReadOnlyDictionary<(int SubsidyId, int ProductGroupId), int> EmptyCart =
        new Dictionary<(int, int), int>();

    private static ProductGroupCredit Group(int groupId, int quantity, params int[] productIds) => new()
    {
        ProductGroupId = groupId,
        Quantity = quantity,
        AvailableQuantity = quantity,
        ProductIds = productIds.ToList()
    };

    private static SubsidyCredit Subsidy(int subsidyId, bool restrict, params ProductGroupCredit[] groups) => new()
    {
        SubsidyId = subsidyId,
        RestrictToAllocatedQty = restrict,
        ProductGroupCredits = groups.ToList()
    };

    private static PackNameCredit Credit(params SubsidyCredit[] subsidies) => new()
    {
        PackNameId = 1,
        SubsidyCredits = subsidies.ToList()
    };

    private static IReadOnlyDictionary<(int SubsidyId, int ProductGroupId), int> Cart(int subsidyId, int groupId, int units) =>
        new Dictionary<(int, int), int> { [(subsidyId, groupId)] = units };

    #endregion

    #region Which subsidies decide

    [Test]
    public void ProductInNoGroup_NoCap()
    {
        var credit = Credit(Subsidy(10, restrict: true, Group(ShirtsGroup, 2, ProductA)));

        SubsidyQuantityCapHelper.GetQuantityLeft(credit, ProductC, EmptyCart).Should().BeNull();
    }

    [Test]
    public void NoSubsidies_NoCap()
    {
        SubsidyQuantityCapHelper.GetQuantityLeft(Credit(), ProductA, EmptyCart).Should().BeNull();
    }

    [Test]
    public void UnrestrictedSubsidyContainingTheProduct_LiftsTheCap()
    {
        var credit = Credit(
            Subsidy(10, restrict: true, Group(ShirtsGroup, 2, ProductA)),
            Subsidy(20, restrict: false, Group(PantsGroup, 5, ProductA)));

        SubsidyQuantityCapHelper.GetQuantityLeft(credit, ProductA, EmptyCart).Should().BeNull();
    }

    /// <summary>Gap analysis S-08, second example: an unrestricted subsidy for other products lifts nothing.</summary>
    [Test]
    public void UnrestrictedSubsidyNotContainingTheProduct_DoesNotLiftTheCap()
    {
        var credit = Credit(
            Subsidy(10, restrict: true, Group(ShirtsGroup, 2, ProductA)),
            Subsidy(20, restrict: false, Group(PantsGroup, 5, ProductC)));

        SubsidyQuantityCapHelper.GetQuantityLeft(credit, ProductA, EmptyCart).Should().Be(2);
    }

    #endregion

    #region Credit

    /// <summary>Gap analysis S-08, first example: group credit left means the extra units are paid from credit.</summary>
    [Test]
    public void GroupCreditAvailable_LiftsTheCap()
    {
        var shirts = Group(ShirtsGroup, 2, ProductA);
        shirts.AvailableCredit = 50m;

        SubsidyQuantityCapHelper.GetQuantityLeft(Credit(Subsidy(10, restrict: true, shirts)), ProductA, EmptyCart).Should().BeNull();
    }

    [Test]
    public void GroupCreditTopupAvailable_LiftsTheCap()
    {
        var shirts = Group(ShirtsGroup, 2, ProductA);
        shirts.AvailableCreditTopup = 10m;

        SubsidyQuantityCapHelper.GetQuantityLeft(Credit(Subsidy(10, restrict: true, shirts)), ProductA, EmptyCart).Should().BeNull();
    }

    [Test]
    public void SubsidyCreditAvailable_LiftsTheCap()
    {
        var subsidy = Subsidy(10, restrict: true, Group(ShirtsGroup, 2, ProductA));
        subsidy.AvailableCreditTopup = 25m;

        SubsidyQuantityCapHelper.GetQuantityLeft(Credit(subsidy), ProductA, EmptyCart).Should().BeNull();
    }

    [Test]
    public void CreditSpent_TheCapReturns()
    {
        var shirts = Group(ShirtsGroup, 2, ProductA);
        shirts.Credit = 50m;
        shirts.AvailableCredit = 0m;

        SubsidyQuantityCapHelper.GetQuantityLeft(Credit(Subsidy(10, restrict: true, shirts)), ProductA, EmptyCart).Should().Be(2);
    }

    [Test]
    public void CreditOfAGroupNotContainingTheProduct_DoesNotLiftTheCap()
    {
        var pants = Group(PantsGroup, 1, ProductC);
        pants.AvailableCredit = 50m;

        var credit = Credit(Subsidy(10, restrict: true, Group(ShirtsGroup, 2, ProductA), pants));

        SubsidyQuantityCapHelper.GetQuantityLeft(credit, ProductA, EmptyCart).Should().Be(2);
    }

    #endregion

    #region What the cart already takes

    /// <summary>Gap analysis S-07: 2×A in the cart, then 2×B from the same group of quantity 2.</summary>
    [Test]
    public void SameGroupOtherProductInCart_IsCounted()
    {
        var credit = Credit(Subsidy(10, restrict: true, Group(ShirtsGroup, 2, ProductA, ProductB)));

        SubsidyQuantityCapHelper.GetQuantityLeft(credit, ProductA, EmptyCart).Should().Be(2);
        SubsidyQuantityCapHelper.GetQuantityLeft(credit, ProductB, Cart(10, ShirtsGroup, 2)).Should().Be(0);
    }

    [Test]
    public void TopupQuantity_IsPartOfTheAllocation()
    {
        var shirts = Group(ShirtsGroup, 2, ProductA);
        shirts.AvailableQuantityTopup = 3;

        SubsidyQuantityCapHelper.GetQuantityLeft(Credit(Subsidy(10, restrict: true, shirts)), ProductA, Cart(10, ShirtsGroup, 1)).Should().Be(4);
    }

    [Test]
    public void ProductInTwoRestrictedGroups_BothCount_EachAgainstItsOwnCartUnits()
    {
        var credit = Credit(
            Subsidy(10, restrict: true, Group(ShirtsGroup, 2, ProductA)),
            Subsidy(20, restrict: true, Group(PantsGroup, 3, ProductA)));

        var cart = new Dictionary<(int, int), int> { [(10, ShirtsGroup)] = 2, [(20, PantsGroup)] = 1 };

        SubsidyQuantityCapHelper.GetQuantityLeft(credit, ProductA, cart).Should().Be(2);
    }

    [Test]
    public void UnitsOfTheSameGroupInAnotherSubsidy_AreNotCounted()
    {
        var credit = Credit(
            Subsidy(10, restrict: true, Group(ShirtsGroup, 2, ProductA)),
            Subsidy(20, restrict: true, Group(PantsGroup, 1, ProductC)));

        SubsidyQuantityCapHelper.GetQuantityLeft(credit, ProductA, Cart(20, ShirtsGroup, 5)).Should().Be(2);
    }

    [Test]
    public void AlreadyOverspent_IsZeroNotNegative()
    {
        var shirts = Group(ShirtsGroup, 2, ProductA);
        shirts.AvailableQuantity = -1;

        SubsidyQuantityCapHelper.GetQuantityLeft(Credit(Subsidy(10, restrict: true, shirts)), ProductA, Cart(10, ShirtsGroup, 3)).Should().Be(0);
    }

    #endregion
}
