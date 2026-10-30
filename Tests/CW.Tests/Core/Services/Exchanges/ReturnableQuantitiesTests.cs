using AwesomeAssertions;
using Moq;
using Nop.Core.Domain.Catalog;
using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Shipping;
using Nop.Data;
using Nop.Plugin.CW.Core.Services.Exchanges;
using Nop.Services.Catalog;
using Nop.Services.Orders;
using NUnit.Framework;

namespace CW.Tests.Core.Services.Exchanges;

/// <summary>
/// What a customer may still return of each order item: nop's limit (the ordered quantity less what other return
/// requests ask for, no non-returnable product) less the units not shipped yet
/// </summary>
[TestFixture]
public class ReturnableQuantitiesTests
{
    private static readonly Order Order = new() { Id = 10 };

    private ExchangeService _service = null!;
    private List<ReturnableOrderItem> _available = null!;
    private List<Shipment> _shipments = null!;
    private List<ShipmentItem> _shipmentItems = null!;
    private List<Product> _products = null!;

    [SetUp]
    public void SetUp()
    {
        _available = new List<ReturnableOrderItem>();
        _shipments = new List<Shipment> { new() { Id = 1, OrderId = Order.Id }, new() { Id = 2, OrderId = Order.Id }, new() { Id = 3, OrderId = 99 } };
        _shipmentItems = new List<ShipmentItem>();
        _products = new List<Product> { new() { Id = 50, IsShipEnabled = true }, new() { Id = 51, IsShipEnabled = false } };

        var returnRequestService = new Mock<IReturnRequestService>();
        returnRequestService.Setup(service => service.GetReturnRequestAvailabilityAsync(Order.Id))
            .ReturnsAsync(() => new ReturnRequestAvailability { ReturnableOrderItems = _available });

        var shipmentRepository = new Mock<IRepository<Shipment>>();
        shipmentRepository.Setup(repository => repository.Table).Returns(() => _shipments.AsQueryable());
        var shipmentItemRepository = new Mock<IRepository<ShipmentItem>>();
        shipmentItemRepository.Setup(repository => repository.Table).Returns(() => _shipmentItems.AsQueryable());

        var productService = new Mock<IProductService>();
        productService.Setup(service => service.GetProductsByIdsAsync(It.IsAny<int[]>()))
            .ReturnsAsync((int[] ids) => _products.Where(product => ids.Contains(product.Id)).ToList());

        _service = TestObject.Create<ExchangeService>();
        TestObject.Set(_service, "_returnRequestService", returnRequestService.Object);
        TestObject.Set(_service, "_shipmentRepository", shipmentRepository.Object);
        TestObject.Set(_service, "_shipmentItemRepository", shipmentItemRepository.Object);
        TestObject.Set(_service, "_productService", productService.Object);
    }

    private void Item(int id, int productId, int ordered, int available) =>
        _available.Add(new ReturnableOrderItem { OrderItem = new OrderItem { Id = id, ProductId = productId, Quantity = ordered }, AvailableQuantityForReturn = available });

    private void Shipped(int shipmentId, int orderItemId, int quantity) =>
        _shipmentItems.Add(new ShipmentItem { ShipmentId = shipmentId, OrderItemId = orderItemId, Quantity = quantity });

    [Test]
    public async Task Only_shipped_units_less_what_was_already_asked_for_can_be_returned()
    {
        //3 ordered, 2 shipped in two parcels, 1 already asked for: of the 2 shipped, 1 is left
        Item(100, 50, ordered: 3, available: 2);
        Shipped(1, 100, 1);
        Shipped(2, 100, 1);

        (await _service.GetReturnableQuantitiesAsync(Order)).Should().BeEquivalentTo(new Dictionary<int, int> { [100] = 1 });
    }

    [Test]
    public async Task An_item_never_shipped_cannot_be_returned()
    {
        Item(100, 50, ordered: 2, available: 2);

        (await _service.GetReturnableQuantitiesAsync(Order)).Should().BeEmpty();
    }

    [Test]
    public async Task Another_orders_parcels_do_not_count()
    {
        Item(100, 50, ordered: 2, available: 2);
        Shipped(3, 100, 2);

        (await _service.GetReturnableQuantitiesAsync(Order)).Should().BeEmpty();
    }

    [Test]
    public async Task An_item_that_needs_no_shipping_follows_nops_limit()
    {
        Item(101, 51, ordered: 2, available: 2);

        (await _service.GetReturnableQuantitiesAsync(Order)).Should().BeEquivalentTo(new Dictionary<int, int> { [101] = 2 });
    }

    [Test]
    public async Task An_item_nop_leaves_out_cannot_be_returned()
    {
        //nop leaves out non-returnable products and, unless allowed, downloads: they are not in its list
        Shipped(1, 102, 1);

        (await _service.GetReturnableQuantitiesAsync(Order)).Should().BeEmpty();
    }

    [Test]
    public async Task An_item_fully_asked_for_cannot_be_returned()
    {
        Item(100, 50, ordered: 2, available: 0);
        Shipped(1, 100, 2);

        (await _service.GetReturnableQuantitiesAsync(Order)).Should().BeEmpty();
    }
}
