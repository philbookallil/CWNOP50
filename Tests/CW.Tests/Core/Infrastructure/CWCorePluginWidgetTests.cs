using System.Runtime.CompilerServices;
using AwesomeAssertions;
using Microsoft.AspNetCore.Mvc;
using Nop.Plugin.CW.Core;
using Nop.Web.Framework.Infrastructure;
using NUnit.Framework;

namespace CW.Tests.Core.Infrastructure;

/// <summary>
/// The plugin as a widget plugin: every zone it declares is drawn by one of its view components, and the admin zones the
/// order, shipment, customer and product pages render and the storefront zones of the packname selector and info strip
/// are among them
/// </summary>
[TestFixture]
public class CWCorePluginWidgetTests
{
    /// <summary>
    /// The plugin without its services: the widget members read only the static zone table
    /// </summary>
    private static CWCorePlugin Plugin() => (CWCorePlugin)RuntimeHelpers.GetUninitializedObject(typeof(CWCorePlugin));

    [Test]
    public async Task Every_declared_zone_is_drawn_by_a_view_component_of_the_plugin()
    {
        var plugin = Plugin();

        var zones = await plugin.GetWidgetZonesAsync();

        zones.Should().NotBeEmpty().And.OnlyHaveUniqueItems();
        foreach (var zone in zones)
        {
            var component = plugin.GetWidgetViewComponent(zone);
            component.Should().NotBeNull($"zone {zone} is declared");
            component!.Assembly.Should().BeSameAs(typeof(CWCorePlugin).Assembly);
            typeof(ViewComponent).IsAssignableFrom(component).Should().BeTrue($"{component.Name} draws zone {zone}");
        }
    }

    [TestCase("OrderDetailsBlock", "PackNameOrderAdminDetailsViewComponent")]
    [TestCase("OrderShipmentAddButtons", "CWShipmentDestinationViewComponent")]
    [TestCase("OrderShipmentDetailsButtons", "CWShipmentDestinationViewComponent")]
    [TestCase("OrderListButtons", "OrderListBadgesViewComponent")]
    [TestCase("CustomerDetailsButtons", "OrderListBadgesViewComponent")]
    [TestCase("ProductDetailsBlock", "BulkOrderProductPageAdminViewComponent")]
    public async Task The_admin_zones_are_drawn(string adminZone, string component)
    {
        var plugin = Plugin();
        var zone = (string)typeof(AdminWidgetZones).GetProperty(adminZone)!.GetValue(null)!;

        (await plugin.GetWidgetZonesAsync()).Should().Contain(zone);
        plugin.GetWidgetViewComponent(zone)!.Name.Should().Be(component);
    }

    [TestCase("HeaderLinksAfter", "PackNameSelectorViewComponent")]
    [TestCase("ContentBefore", "PackNameInfoViewComponent")]
    [TestCase("HeaderLinksBefore", "BulkOrderLinkViewComponent")]
    [TestCase("ProductDetailsOverviewBottom", "BulkOrderProductPageViewComponent")]
    [TestCase("ProductDetailsBottom", "BulkOrderProductPageViewComponent")]
    [TestCase("ContentAfter", "BulkOrderProductPageViewComponent")]
    public async Task The_storefront_zones_are_drawn(string publicZone, string component)
    {
        var plugin = Plugin();
        var zone = (string)typeof(PublicWidgetZones).GetProperty(publicZone)!.GetValue(null)!;

        (await plugin.GetWidgetZonesAsync()).Should().Contain(zone);
        plugin.GetWidgetViewComponent(zone)!.Name.Should().Be(component);
    }

    [TestCase("exchange_fees_for_cart")]
    [TestCase("exchange_fees_for_order")]
    [TestCase("exchange_fees_for_order_admin")]
    public async Task The_exchange_fee_zones_are_drawn(string zone)
    {
        var plugin = Plugin();

        (await plugin.GetWidgetZonesAsync()).Should().Contain(zone);
        plugin.GetWidgetViewComponent(zone)!.Name.Should().Be("ExchangesWidgetsViewComponent");
    }

    [Test]
    public void A_zone_the_plugin_does_not_draw_has_no_component()
    {
        Plugin().GetWidgetViewComponent("no_such_zone").Should().BeNull();
    }

    [Test]
    public void The_plugin_is_hidden_on_the_widget_list()
    {
        Plugin().HideInWidgetList.Should().BeTrue();
    }

    [TestCase("CWOrderReturnRequests")]
    [TestCase("CreditNotesWidgets")]
    [TestCase("OrderSubsidyCredit")]
    public void The_components_the_order_page_invokes_by_name_exist(string name)
    {
        //CW's order page views call these with Component.InvokeAsync("name"); a missing one fails only when the page opens
        typeof(CWCorePlugin).Assembly.GetTypes()
            .Where(type => typeof(ViewComponent).IsAssignableFrom(type))
            .Select(type => type.GetCustomAttributes(typeof(ViewComponentAttribute), false).Cast<ViewComponentAttribute>().FirstOrDefault()?.Name
                ?? (type.Name.EndsWith("ViewComponent") ? type.Name[..^"ViewComponent".Length] : type.Name))
            .Should().Contain(name);
    }
}
