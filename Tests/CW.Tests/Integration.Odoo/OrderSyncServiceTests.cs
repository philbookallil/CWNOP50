using AwesomeAssertions;
using Moq;
using Nop.Core;
using Nop.Core.Domain.Catalog;
using Nop.Core.Domain.Orders;
using Nop.Data;
using Nop.Plugin.CW.Core.Domain.Branches;
using Nop.Plugin.CW.Core.Domain.Exchanges;
using Nop.Plugin.CW.Core.Services.Branches;
using Nop.Plugin.CW.Core.Services.Exchanges;
using Nop.Plugin.CW.Core.Services.Orders;
using Nop.Plugin.CW.Core.Services.PackNames;
using Nop.Plugin.Integration.Odoo;
using Nop.Plugin.Integration.Odoo.Domain;
using Nop.Plugin.Integration.Odoo.Models.Responses;
using Nop.Plugin.Integration.Odoo.Services;
using Nop.Services.Catalog;
using Nop.Services.Common;
using Nop.Services.Directory;
using Nop.Services.Orders;
using NUnit.Framework;

namespace CW.Tests.Integration.Odoo;

/// <summary>
/// The rules of the order send that protect against a second Odoo order and against a status nop only assumed:
/// what is held, what a retry does, and that confirmed and cancelled are Odoo's word, not ours. Odoo is a fake
/// with one sale order (id 900) whose state the tests drive; a plain order with one line is sent.
/// </summary>
[TestFixture]
public class OrderSyncServiceTests
{
    #region Utilities

    private const int OdooOrderId = 900;

    private List<OdooOrderMapping> _mappings = null!;
    private Mock<IOdooApiService> _odoo = null!;
    private Mock<IBranchService> _branches = null!;
    private Order _order = null!;
    private Product _product = null!;
    private OrderSyncService _service = null!;

    // The fake Odoo order
    private string _odooState = null!;
    private decimal _odooUntaxed;
    private bool _confirmWorks;
    private bool _cancelWorks;
    private string? _confirmError;

    [SetUp]
    public void SetUp()
    {
        _mappings = new List<OdooOrderMapping>();
        _odooState = "draft";
        _odooUntaxed = 60m;
        _confirmWorks = true;
        _cancelWorks = true;
        _confirmError = null;

        _order = new Order { Id = 5, CustomOrderNumber = "1042", CustomerId = 9, OrderStatus = OrderStatus.Processing, PickupInStore = true };
        _product = new Product { Id = 3, Name = "Polo", ManufacturerPartNumber = "ODOO_77" };
        var items = new List<OrderItem> { new() { Id = 50, OrderId = 5, ProductId = 3, Quantity = 2, PriceExclTax = 60m } };

        var mappingRepository = new Mock<IRepository<OdooOrderMapping>>();
        mappingRepository.Setup(repository => repository.Table).Returns(() => _mappings.AsQueryable());
        mappingRepository.Setup(repository => repository.InsertAsync(It.IsAny<OdooOrderMapping>(), It.IsAny<bool>()))
            .Returns((OdooOrderMapping mapping, bool _) =>
            {
                mapping.Id = _mappings.Count + 1;
                _mappings.Add(mapping);
                return Task.CompletedTask;
            });

        var orders = new Mock<IOrderService>();
        orders.Setup(service => service.GetOrderByIdAsync(5)).ReturnsAsync(() => _order);
        orders.Setup(service => service.GetOrderItemsAsync(5, null, null, 0)).ReturnsAsync(items);

        var products = new Mock<IProductService>();
        products.Setup(service => service.GetProductByIdAsync(3)).ReturnsAsync(() => _product);

        var genericAttributes = new Mock<IGenericAttributeService>();
        genericAttributes
            .Setup(service => service.GetAttributeAsync<int>(It.IsAny<BaseEntity>(), OdooConstants.ProductVariantIdAttribute, 0, 0))
            .ReturnsAsync(61);

        _branches = new Mock<IBranchService>();
        _branches.Setup(service => service.GetBranchByCustomerIdAsync(9))
            .ReturnsAsync(new Branch { Id = 1, Name = "Head Office", CardCode = "31" });

        var exchanges = new Mock<IExchangeService>();
        exchanges.Setup(service => service.GetReturnRequestItemsForOrderItemsAsync(It.IsAny<int[]>()))
            .ReturnsAsync(new List<ReturnRequestItem>());

        _odoo = new Mock<IOdooApiService>();
        _odoo.Setup(api => api.CreateSaleOrderAsync(It.IsAny<IDictionary<string, object>>())).ReturnsAsync(OdooOrderId);
        _odoo.Setup(api => api.GetSaleOrderAsync(OdooOrderId))
            .ReturnsAsync(() => new OdooSaleOrderResponse { Id = OdooOrderId, Name = "S00900", State = _odooState, AmountUntaxed = _odooUntaxed });
        _odoo.Setup(api => api.ConfirmSaleOrderAsync(OdooOrderId)).Returns(() =>
        {
            if (_confirmError != null)
                throw new OdooApiException(_confirmError);
            if (_confirmWorks)
                _odooState = "sale";
            return Task.CompletedTask;
        });
        _odoo.Setup(api => api.CancelSaleOrderAsync(OdooOrderId)).Returns(() =>
        {
            if (_cancelWorks)
                _odooState = "cancel";
            return Task.CompletedTask;
        });

        _service = new OrderSyncService(
            Mock.Of<IAddressService>(),
            _branches.Object,
            Mock.Of<ICountryService>(),
            Mock.Of<ICWOrderService>(),
            exchanges.Object,
            genericAttributes.Object,
            _odoo.Object,
            Mock.Of<IOdooLogService>(),
            orders.Object,
            Mock.Of<IPackNameService>(),
            Mock.Of<IProductAttributeParser>(),
            products.Object,
            mappingRepository.Object,
            Mock.Of<IRepository<Order>>(),
            Mock.Of<IStateProvinceService>(),
            new OdooSettings());
    }

    private Task<OrderSyncResult> SendAsync() => _service.SendOrdersAsync(1, new[] { 5 });

    private OdooOrderMapping Mapping => _mappings.Single();

    #endregion

    [Test]
    public async Task A_sendable_order_is_created_confirmed_and_recorded()
    {
        IDictionary<string, object>? sent = null;
        _odoo.Setup(api => api.CreateSaleOrderAsync(It.IsAny<IDictionary<string, object>>()))
            .Callback((IDictionary<string, object> values) => sent = values)
            .ReturnsAsync(OdooOrderId);

        var result = await SendAsync();

        result.Should().Be(new OrderSyncResult(Created: 1, Updated: 0, Skipped: 0));
        Mapping.Status.Should().Be(OdooOrderStatus.Confirmed);
        Mapping.OdooOrderId.Should().Be(OdooOrderId);
        Mapping.OdooOrderName.Should().Be("S00900");
        Mapping.Message.Should().BeNull();

        sent.Should().NotBeNull();
        sent!["origin"].Should().Be("nop 1042");
        sent["partner_id"].Should().Be(31);
        // A pickup ships to the company itself
        sent["partner_shipping_id"].Should().Be(31);
    }

    [Test]
    public async Task An_order_Odoo_already_has_under_its_origin_is_not_created_again()
    {
        _odooState = "sale";
        _odoo.Setup(api => api.FindSaleOrderByOriginAsync("nop 1042"))
            .ReturnsAsync(new OdooSaleOrderResponse { Id = OdooOrderId, Name = "S00900", State = "sale", AmountUntaxed = 60m });

        var result = await SendAsync();

        result.Should().Be(new OrderSyncResult(Created: 0, Updated: 1, Skipped: 0));
        Mapping.Status.Should().Be(OdooOrderStatus.Confirmed);
        Mapping.OdooOrderId.Should().Be(OdooOrderId);
        _odoo.Verify(api => api.CreateSaleOrderAsync(It.IsAny<IDictionary<string, object>>()), Times.Never);
        _odoo.Verify(api => api.ConfirmSaleOrderAsync(It.IsAny<int>()), Times.Never);
    }

    [Test]
    public async Task A_failed_confirmation_keeps_the_Odoo_id_and_the_next_send_confirms_without_creating()
    {
        _confirmError = "Not enough stock";

        await SendAsync();

        Mapping.Status.Should().Be(OdooOrderStatus.Failed);
        Mapping.Message.Should().Be("Not enough stock");
        Mapping.OdooOrderId.Should().Be(OdooOrderId);

        _confirmError = null;
        var result = await SendAsync();

        result.Should().Be(new OrderSyncResult(Created: 0, Updated: 1, Skipped: 0));
        Mapping.Status.Should().Be(OdooOrderStatus.Confirmed);
        _odoo.Verify(api => api.CreateSaleOrderAsync(It.IsAny<IDictionary<string, object>>()), Times.Once);
    }

    [Test]
    public async Task An_order_Odoo_leaves_as_a_quotation_is_failed_not_confirmed()
    {
        _confirmWorks = false;

        await SendAsync();

        Mapping.Status.Should().Be(OdooOrderStatus.Failed);
        Mapping.Message.Should().Contain("did not confirm");
        Mapping.OdooOrderId.Should().Be(OdooOrderId);
    }

    [Test]
    public async Task A_customer_in_no_branch_holds_the_order()
    {
        _branches.Setup(service => service.GetBranchByCustomerIdAsync(9)).ReturnsAsync((Branch)null!);

        var result = await SendAsync();

        result.Should().Be(new OrderSyncResult(Created: 0, Updated: 0, Skipped: 1));
        Mapping.Status.Should().Be(OdooOrderStatus.Held);
        Mapping.Message.Should().Contain("no branch");
        _odoo.Verify(api => api.CreateSaleOrderAsync(It.IsAny<IDictionary<string, object>>()), Times.Never);
    }

    [Test]
    public async Task A_product_that_did_not_come_from_Odoo_holds_the_order()
    {
        _product.ManufacturerPartNumber = null;

        await SendAsync();

        Mapping.Status.Should().Be(OdooOrderStatus.Held);
        Mapping.Message.Should().Contain("'Polo' is not linked to an Odoo product variant");
        _odoo.Verify(api => api.CreateSaleOrderAsync(It.IsAny<IDictionary<string, object>>()), Times.Never);
    }

    [Test]
    public async Task A_total_that_differs_from_what_nop_charged_is_kept_as_a_warning()
    {
        _odooUntaxed = 59.98m;

        await SendAsync();

        Mapping.Status.Should().Be(OdooOrderStatus.Confirmed);
        Mapping.Message.Should().Contain("59.98").And.Contain("60.00");
    }

    [Test]
    public async Task A_cancel_is_recorded_only_when_Odoo_says_the_order_is_cancelled()
    {
        await SendAsync();
        _order.OrderStatus = OrderStatus.Cancelled;

        _cancelWorks = false;
        var refused = await _service.CancelOrdersAsync(1, new[] { 5 });

        refused.Should().Be(new OrderSyncResult(Created: 0, Updated: 0, Skipped: 1));
        Mapping.Status.Should().Be(OdooOrderStatus.Confirmed);
        Mapping.Message.Should().Contain("still 'sale'");

        _cancelWorks = true;
        var cancelled = await _service.CancelOrdersAsync(1, new[] { 5 });

        cancelled.Should().Be(new OrderSyncResult(Created: 0, Updated: 1, Skipped: 0));
        Mapping.Status.Should().Be(OdooOrderStatus.Cancelled);
        Mapping.Message.Should().BeNull();
    }
}
