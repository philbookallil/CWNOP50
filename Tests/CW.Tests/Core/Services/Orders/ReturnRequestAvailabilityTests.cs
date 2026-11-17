using AwesomeAssertions;
using Moq;
using Nop.Core.Domain.Catalog;
using Nop.Core.Domain.Orders;
using Nop.Data;
using Nop.Plugin.CW.Core.Services.Orders;
using NUnit.Framework;

namespace CW.Tests.Core.Services.Orders;

/// <summary>
/// What a customer may still return of each order item: nop's count of what other returns already ask for, without the
/// returns that were rejected or cancelled
/// </summary>
[TestFixture]
public class ReturnRequestAvailabilityTests
{
    private const int OrderId = 10;

    private List<OrderItem> _orderItems = null!;
    private List<Product> _products = null!;
    private List<ReturnRequest> _returnRequests = null!;
    private ReturnRequestOverrideService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _products = new List<Product> { new() { Id = 50 }, new() { Id = 51, NotReturnable = true } };
        _orderItems = new List<OrderItem>
        {
            new() { Id = 100, OrderId = OrderId, ProductId = 50, Quantity = 3 },
            new() { Id = 101, OrderId = OrderId, ProductId = 50, Quantity = 2 },
            new() { Id = 102, OrderId = OrderId, ProductId = 51, Quantity = 1 },
            new() { Id = 200, OrderId = 11, ProductId = 50, Quantity = 4 }
        };
        _returnRequests = new List<ReturnRequest>();

        var returnRequestRepository = new Mock<IRepository<ReturnRequest>>();
        returnRequestRepository.Setup(repository => repository.Table).Returns(() => _returnRequests.AsQueryable());
        var orderItemRepository = new Mock<IRepository<OrderItem>>();
        orderItemRepository.Setup(repository => repository.Table).Returns(() => _orderItems.AsQueryable());
        var productRepository = new Mock<IRepository<Product>>();
        productRepository.Setup(repository => repository.Table).Returns(() => _products.AsQueryable());

        _service = new ReturnRequestOverrideService(returnRequestRepository.Object,
            Mock.Of<IRepository<ReturnRequestAction>>(),
            Mock.Of<IRepository<ReturnRequestReason>>(),
            orderItemRepository.Object,
            productRepository.Object,
            new ReturnRequestSettings());
    }

    private void Returned(int orderItemId, int quantity, ReturnRequestStatus status) =>
        _returnRequests.Add(new ReturnRequest { Id = _returnRequests.Count + 1, OrderItemId = orderItemId, Quantity = quantity, ReturnRequestStatus = status });

    private async Task<Dictionary<int, int>> AvailableAsync() =>
        (await _service.GetReturnRequestAvailabilityAsync(OrderId)).ReturnableOrderItems
            .ToDictionary(item => item.OrderItem.Id, item => item.AvailableQuantityForReturn);

    [Test]
    public async Task The_units_of_a_rejected_return_can_be_returned_again()
    {
        Returned(100, 2, ReturnRequestStatus.RequestRejected);

        (await AvailableAsync())[100].Should().Be(3);
    }

    [Test]
    public async Task A_cancelled_return_gives_its_units_back_as_nop_does()
    {
        Returned(101, 2, ReturnRequestStatus.Cancelled);

        (await AvailableAsync())[101].Should().Be(2);
    }

    [TestCase(ReturnRequestStatus.Pending)]
    [TestCase(ReturnRequestStatus.Received)]
    [TestCase(ReturnRequestStatus.ReturnAuthorized)]
    [TestCase(ReturnRequestStatus.ItemsRepaired)]
    [TestCase(ReturnRequestStatus.ItemsRefunded)]
    public async Task Every_other_status_keeps_its_units(ReturnRequestStatus status)
    {
        Returned(100, 2, status);

        (await AvailableAsync())[100].Should().Be(1);
    }

    [Test]
    public async Task A_rejected_return_beside_a_live_one_frees_only_its_own_units()
    {
        Returned(100, 2, ReturnRequestStatus.RequestRejected);
        Returned(100, 1, ReturnRequestStatus.Pending);

        (await AvailableAsync())[100].Should().Be(2);
    }

    [Test]
    public async Task More_asked_for_than_ordered_leaves_nothing_never_less()
    {
        Returned(100, 2, ReturnRequestStatus.Pending);
        Returned(100, 2, ReturnRequestStatus.ReturnAuthorized);

        (await AvailableAsync())[100].Should().Be(0);
    }

    [Test]
    public async Task Another_orders_returns_and_non_returnable_products_are_left_as_nop_leaves_them()
    {
        Returned(200, 4, ReturnRequestStatus.Pending);

        var available = await AvailableAsync();

        available.Should().ContainKeys(100, 101).And.NotContainKey(102).And.NotContainKey(200);
        available[100].Should().Be(3);
    }
}
