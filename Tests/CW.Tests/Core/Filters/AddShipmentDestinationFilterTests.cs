using System.Net;
using System.Reflection;
using AwesomeAssertions;
using Moq;
using Nop.Plugin.CW.Core.Domain.Orders.Views;
using Nop.Plugin.CW.Core.Filters;
using Nop.Plugin.CW.Core.Services.Orders;
using Nop.Services.Localization;
using Nop.Services.Messages;
using Nop.Web.Areas.Admin.Models.Orders;
using NUnit.Framework;

namespace CW.Tests.Core.Filters;

/// <summary>
/// The Add shipment page of a packname order says who each line is for, so two identical products going to different
/// people can be told apart; lines without a packname are left as nop built them.
/// </summary>
[TestFixture]
public class AddShipmentDestinationFilterTests
{
    private const int OrderId = 4013;

    private static async Task<ShipmentModel> AddForLinesAsync(IList<PackNameFullOrderItem> packNameItems, params ShipmentItemModel[] items)
    {
        var orderService = new Mock<ICWOrderService>();
        orderService.Setup(service => service.GetPackNameFullOrderItemsAsync(OrderId)).ReturnsAsync(packNameItems);

        var localizationService = new Mock<ILocalizationService>();
        localizationService.Setup(service => service.GetResourceAsync("Plugins.CW.Core.Admin.Orders.Shipments.Product.For")).ReturnsAsync("For: {0}");

        var filter = new AddShipmentDestinationFilter(orderService.Object, localizationService.Object, new Mock<INotificationService>().Object);
        var model = new ShipmentModel { OrderId = OrderId, Items = items.ToList() };

        var method = typeof(AddShipmentDestinationFilter).GetMethod("AddPackNameForLinesAsync", BindingFlags.NonPublic | BindingFlags.Instance)!;
        await (Task)method.Invoke(filter, [model])!;

        return model;
    }

    [Test]
    public async Task Each_packname_line_gets_who_it_is_for_under_its_attributes()
    {
        var model = await AddForLinesAsync(
            [
                new PackNameFullOrderItem { Id = 1, PackName = "Steve Gates", Department = "Department 1", CostCenter = "Cost Center 1" },
                new PackNameFullOrderItem { Id = 2, PackName = "Brenda Lindgren" }
            ],
            new ShipmentItemModel { OrderItemId = 1, AttributeInfo = "Size: 2X" },
            new ShipmentItemModel { OrderItemId = 2 });

        //the page shows the decoded text: the encoder writes the "·" separator as an entity
        WebUtility.HtmlDecode(model.Items[0].AttributeInfo).Should().Be("Size: 2X<br />For: Steve Gates · Department 1 · Cost Center 1");
        WebUtility.HtmlDecode(model.Items[1].AttributeInfo).Should().Be("For: Brenda Lindgren");
    }

    [Test]
    public async Task The_packnames_own_text_is_encoded()
    {
        var model = await AddForLinesAsync(
            [new PackNameFullOrderItem { Id = 1, PackName = "Tom & Jerry" }],
            new ShipmentItemModel { OrderItemId = 1 });

        model.Items[0].AttributeInfo.Should().Be("For: Tom &amp; Jerry");
    }

    [Test]
    public async Task A_plain_order_and_a_line_without_a_packname_are_left_alone()
    {
        var plain = await AddForLinesAsync([], new ShipmentItemModel { OrderItemId = 1, AttributeInfo = "Size: S" });
        plain.Items[0].AttributeInfo.Should().Be("Size: S");

        var mixed = await AddForLinesAsync(
            [new PackNameFullOrderItem { Id = 1, PackName = "Steve Gates" }],
            new ShipmentItemModel { OrderItemId = 2, AttributeInfo = "Size: S" });
        mixed.Items[0].AttributeInfo.Should().Be("Size: S");
    }
}
