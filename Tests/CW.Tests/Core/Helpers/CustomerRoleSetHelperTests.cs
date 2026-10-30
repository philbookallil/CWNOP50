using AwesomeAssertions;
using Nop.Plugin.CW.Core.Helpers;
using NUnit.Framework;

namespace CW.Tests.Core.Helpers;

/// <summary>
/// The role set the catalogue sees while a customer orders for a packname.
/// </summary>
/// <remarks>
/// <para>
/// <b>Visibility follows the packname.</b> Its own roles plus its department's role decide what is shown; the
/// roles of the person signed in must never make anything visible.
/// </para>
/// <para>
/// <b>The signed-in roles still reach the cache key.</b> nop keys the product price cache on this set while
/// computing the price from the signed-in customer, so two customers seeing the same packname roles but holding
/// different roles themselves must produce different sets. They ride along as negative ids, which no ACL record
/// or role limit can match.
/// </para>
/// </remarks>
[TestFixture]
public class CustomerRoleSetHelperTests
{
    #region Utilities

    private const int Registered = 3;
    private const int Wholesale = 7;
    private const int Uniforms = 12;
    private const int DepartmentA = 40;

    private static readonly int[] NoRoles = [];

    //What nop's ACL consumers do with the set: intersect it with the roles an entity is mapped to.
    private static bool Sees(int[] roleSet, params int[] aclRoleIds) => aclRoleIds.Intersect(roleSet).Any();

    #endregion

    #region Visibility

    [Test]
    public void Department_role_is_added_to_the_packname_roles()
    {
        var roleSet = CustomerRoleSetHelper.ComposeCatalogRoleIds([Registered, Uniforms], DepartmentA, NoRoles);

        roleSet.Should().Equal(Registered, Uniforms, DepartmentA);
    }

    [TestCase(null)]
    [TestCase(0)]
    public void No_department_role_adds_nothing(int? departmentRoleId)
    {
        var roleSet = CustomerRoleSetHelper.ComposeCatalogRoleIds([Registered], departmentRoleId, NoRoles);

        roleSet.Should().Equal(Registered);
    }

    [Test]
    public void An_entity_mapped_to_the_department_only_is_seen()
    {
        var roleSet = CustomerRoleSetHelper.ComposeCatalogRoleIds([Registered], DepartmentA, [Registered, Wholesale]);

        Sees(roleSet, DepartmentA).Should().BeTrue();
    }

    [Test]
    public void An_entity_mapped_to_another_department_only_is_not_seen()
    {
        var roleSet = CustomerRoleSetHelper.ComposeCatalogRoleIds([Registered], DepartmentA, [Registered]);

        Sees(roleSet, DepartmentA + 1).Should().BeFalse();
    }

    [Test]
    public void A_role_only_the_signed_in_customer_holds_makes_nothing_visible()
    {
        var roleSet = CustomerRoleSetHelper.ComposeCatalogRoleIds([Registered], DepartmentA, [Registered, Wholesale]);

        Sees(roleSet, Wholesale).Should().BeFalse();
    }

    [Test]
    public void A_role_the_packname_holds_is_seen_whatever_the_signed_in_customer_holds()
    {
        var roleSet = CustomerRoleSetHelper.ComposeCatalogRoleIds([Registered, Uniforms], null, [Registered]);

        Sees(roleSet, Uniforms).Should().BeTrue();
    }

    #endregion

    #region Cache key

    [Test]
    public void Signed_in_roles_follow_as_negative_ids()
    {
        var roleSet = CustomerRoleSetHelper.ComposeCatalogRoleIds([Registered], DepartmentA, [Wholesale, Registered]);

        roleSet.Should().Equal(Registered, DepartmentA, -Registered, -Wholesale);
    }

    [Test]
    public void Same_packname_roles_with_different_signed_in_roles_give_different_sets()
    {
        var manager = CustomerRoleSetHelper.ComposeCatalogRoleIds([Registered], DepartmentA, [Registered, Wholesale]);
        var employee = CustomerRoleSetHelper.ComposeCatalogRoleIds([Registered], DepartmentA, [Registered]);

        manager.Should().NotEqual(employee);
    }

    [Test]
    public void Same_inputs_in_any_order_give_the_same_set()
    {
        var first = CustomerRoleSetHelper.ComposeCatalogRoleIds([Uniforms, Registered, Uniforms], DepartmentA, [Wholesale, Registered]);
        var second = CustomerRoleSetHelper.ComposeCatalogRoleIds([Registered, Uniforms], DepartmentA, [Registered, Wholesale, Wholesale]);

        first.Should().Equal(second);
    }

    [Test]
    public void Department_role_already_held_by_the_packname_is_not_repeated()
    {
        var roleSet = CustomerRoleSetHelper.ComposeCatalogRoleIds([Registered, DepartmentA], DepartmentA, NoRoles);

        roleSet.Should().Equal(Registered, DepartmentA);
    }

    [Test]
    public void Ids_that_are_not_roles_are_ignored()
    {
        var roleSet = CustomerRoleSetHelper.ComposeCatalogRoleIds([0, -5, Registered], DepartmentA, [0, -Wholesale]);

        roleSet.Should().Equal(Registered, DepartmentA);
    }

    [Test]
    public void A_restriction_marker_comes_last()
    {
        var roleSet = CustomerRoleSetHelper.ComposeCatalogRoleIds([Registered], DepartmentA, [Wholesale], int.MinValue + 42);

        roleSet.Should().Equal(Registered, DepartmentA, -Wholesale, int.MinValue + 42);
    }

    [Test]
    public void A_restriction_marker_makes_nothing_visible()
    {
        var roleSet = CustomerRoleSetHelper.ComposeCatalogRoleIds([Registered], null, [Registered], int.MinValue + 42);

        Sees(roleSet, int.MinValue + 41, -1, 0).Should().BeFalse();
    }

    [Test]
    public void Without_a_marker_the_set_is_unchanged()
    {
        CustomerRoleSetHelper.ComposeCatalogRoleIds([Registered], DepartmentA, [Wholesale], null)
            .Should().Equal(CustomerRoleSetHelper.ComposeCatalogRoleIds([Registered], DepartmentA, [Wholesale]));
    }

    [Test]
    public void Null_role_lists_are_refused()
    {
        var noPackNameRoles = () => CustomerRoleSetHelper.ComposeCatalogRoleIds(null, DepartmentA, NoRoles);
        var noSignedInRoles = () => CustomerRoleSetHelper.ComposeCatalogRoleIds(NoRoles, DepartmentA, null);

        noPackNameRoles.Should().Throw<ArgumentNullException>();
        noSignedInRoles.Should().Throw<ArgumentNullException>();
    }

    #endregion
}
