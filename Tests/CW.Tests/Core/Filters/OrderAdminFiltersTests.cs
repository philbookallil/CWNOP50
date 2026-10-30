using System.Reflection;
using AwesomeAssertions;
using Nop.Plugin.CW.Core.Filters;
using Nop.Web.Areas.Admin.Controllers;
using Nop.Web.Framework.Controllers;
using NUnit.Framework;

namespace CW.Tests.Core.Filters;

/// <summary>
/// The two order filters recognise nop's order actions by method name and by form-button name. A nop upgrade that renames
/// one would switch the filter off without any error, so these pin the names to nop's OrderController.
/// </summary>
[TestFixture]
public class OrderAdminFiltersTests
{
    private static readonly MethodInfo[] OrderActions = typeof(OrderController).GetMethods(BindingFlags.Public | BindingFlags.Instance);

    private static string[] PrivateList(string field) =>
        (string[])typeof(PackNameOrderAdminGuardFilter).GetField(field, BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;

    [Test]
    public void Every_action_the_guard_blocks_exists_on_nops_order_controller()
    {
        var blocked = PrivateList("BlockedMethods");

        blocked.Should().NotBeEmpty();
        foreach (var method in blocked)
            OrderActions.Should().Contain(action => action.Name == method, $"the guard blocks {method} on packname orders");
    }

    [Test]
    public void The_item_buttons_the_guard_reads_are_the_ones_nop_posts()
    {
        var buttonNames = OrderActions
            .Where(action => action.Name is "EditOrderItem" or "DeleteOrderItem")
            .SelectMany(action => action.GetCustomAttributes<FormValueRequiredAttribute>())
            .SelectMany(attribute => (string[])typeof(FormValueRequiredAttribute)
                .GetField("_submitButtonNames", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(attribute)!)
            .ToList();

        buttonNames.Should().BeEquivalentTo(PrivateList("OrderItemButtonPrefixes"));
    }

    [Test]
    public void The_shipment_filter_targets_an_action_of_nops_order_controller()
    {
        OrderActions.Should().Contain(action => action.Name == "AddShipment");
    }
}
