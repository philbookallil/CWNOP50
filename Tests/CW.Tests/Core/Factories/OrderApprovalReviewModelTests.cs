using System.Globalization;
using AwesomeAssertions;
using Moq;
using Nop.Core;
using Nop.Core.Domain.Catalog;
using Nop.Core.Domain.Common;
using Nop.Core.Domain.Customers;
using Nop.Core.Domain.Localization;
using Nop.Core.Domain.Orders;
using Nop.Plugin.CW.Core.Domain.Approvals;
using Nop.Plugin.CW.Core.Domain.Orders;
using Nop.Plugin.CW.Core.Domain.Orders.DTOs;
using Nop.Plugin.CW.Core.Domain.Orders.Views;
using Nop.Plugin.CW.Core.Domain.PackNames;
using Nop.Plugin.CW.Core.Factories;
using Nop.Plugin.CW.Core.Models.Approvals;
using Nop.Plugin.CW.Core.Services.Approvals;
using Nop.Plugin.CW.Core.Services.Orders;
using Nop.Plugin.CW.Core.Services.PackNames;
using Nop.Services.Catalog;
using Nop.Services.Common;
using Nop.Services.Customers;
using Nop.Services.Directory;
using Nop.Services.Helpers;
using Nop.Services.Localization;
using Nop.Services.Orders;
using NUnit.Framework;

namespace CW.Tests.Core.Factories;

/// <summary>
/// The page an approver opens from the e-mail: what was ordered for whom, and totals that add up.
/// </summary>
[TestFixture]
public class OrderApprovalReviewModelTests
{
    #region Fields

    private const string Token = "token";

    private Order _order = null!;
    private OrderApproval _approval = null!;
    private OrderApprovalRequest _request = null!;
    private OrderSettlement? _settlement;
    private Address? _address;
    private KeyValuePair<AddressField, string>[]? _formatFields;
    private List<OrderItem> _items = null!;
    private List<PackNameOrderGroupDTO> _groups = null!;
    private List<PackName> _packNames = null!;
    private OrderApprovalModelFactory _factory = null!;

    #endregion

    #region Utilities

    [SetUp]
    public void SetUp()
    {
        _order = new Order { Id = 1, CustomOrderNumber = "4014", CurrencyRate = 1, CustomerCurrencyCode = "AUD" };
        _approval = new OrderApproval { Id = 5, OrderId = 1, Status = OrderApprovalStatus.Pending };
        _request = new OrderApprovalRequest { ApproverName = "Ann" };
        _settlement = null;
        _address = null;
        _formatFields = null;
        _items = [];
        _groups = [];
        _packNames = [];

        var approvalService = new Mock<IOrderApprovalService>();
        approvalService.Setup(mock => mock.GetByTokenAsync(Token)).ReturnsAsync(() => (_approval, _request));

        var cwOrderService = new Mock<ICWOrderService>();
        cwOrderService.Setup(mock => mock.GetOrderSettlementAsync(1)).ReturnsAsync(() => _settlement);
        cwOrderService.Setup(mock => mock.GetPackNameOrderGroupsAsync(1)).ReturnsAsync(() => _groups);

        var orderService = new Mock<IOrderService>();
        orderService.Setup(mock => mock.GetOrderByIdAsync(1)).ReturnsAsync(() => _order);
        orderService.Setup(mock => mock.GetOrderItemsAsync(1, It.IsAny<bool?>(), It.IsAny<bool?>(), It.IsAny<int>())).ReturnsAsync(() => _items);

        var packNameService = new Mock<IPackNameService>();
        packNameService.Setup(mock => mock.GetPackNamesByIdsAsync(It.IsAny<int[]>()))
            .ReturnsAsync((int[] ids) => _packNames.Where(packName => ids.Contains(packName.Id)).ToList());

        var productService = new Mock<IProductService>();
        productService.Setup(mock => mock.GetProductByIdAsync(It.IsAny<int>())).ReturnsAsync((int id) => new Product { Id = id, Name = $"Product {id}" });

        var localizationService = new Mock<ILocalizationService>();
        localizationService.Setup(mock => mock.GetResourceAsync(It.IsAny<string>()))
            .ReturnsAsync((string key) => key.EndsWith("SharedShippingWith") ? "with {0}" : key);

        var currencyService = new Mock<ICurrencyService>();
        currencyService.Setup(mock => mock.ConvertCurrency(It.IsAny<decimal>(), It.IsAny<decimal>())).Returns((decimal amount, decimal rate) => amount * rate);

        var priceFormatter = new Mock<IPriceFormatter>();
        priceFormatter.Setup(mock => mock.FormatPriceAsync(It.IsAny<decimal>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<int>()))
            .ReturnsAsync((decimal price, bool _, string _, bool _, int _) => price.ToString("0.00", CultureInfo.InvariantCulture));

        var customerService = new Mock<ICustomerService>();
        customerService.Setup(mock => mock.GetCustomerByIdAsync(It.IsAny<int>())).ReturnsAsync(new Customer());
        customerService.Setup(mock => mock.GetCustomerFullNameAsync(It.IsAny<Customer>())).ReturnsAsync("Steve");

        var addressService = new Mock<IAddressService>();
        addressService.Setup(mock => mock.GetAddressByIdAsync(9)).ReturnsAsync(() => _address);
        addressService.Setup(mock => mock.FormatAddressAsync(It.IsAny<Address>(), 1, It.IsAny<string>(), false))
            .ReturnsAsync((Address address, int _, string _, bool _) => (string.Empty, _formatFields ??
            [
                //as nop answers: country first, and the fields that are empty or switched off
                new(AddressField.Country, "Australia"),
                new(AddressField.StateProvince, "New South Wales"),
                new(AddressField.City, address.City),
                new(AddressField.County, string.Empty),
                new(AddressField.Address1, address.Address1),
                new(AddressField.Address2, address.Address2 ?? string.Empty),
                new(AddressField.ZipPostalCode, address.ZipPostalCode)
            ]));

        var workContext = new Mock<IWorkContext>();
        workContext.Setup(mock => mock.GetWorkingLanguageAsync()).ReturnsAsync(new Language { Id = 1 });

        _factory = new OrderApprovalModelFactory(addressService.Object,
            currencyService.Object,
            customerService.Object,
            cwOrderService.Object,
            new Mock<IDateTimeHelper>().Object,
            localizationService.Object,
            approvalService.Object,
            orderService.Object,
            packNameService.Object,
            priceFormatter.Object,
            productService.Object,
            workContext.Object);
    }

    private Task<OrderApprovalReviewModel> PrepareAsync() => _factory.PrepareReviewModelAsync(Token);

    private void AddFor(int packNameId, string name, int quantity, decimal price, Action<PackNameOrderGroupDTO>? setUp = null)
    {
        var item = new PackNameFullOrderItem { Id = _items.Count + 1, ProductId = _items.Count + 1, Quantity = quantity, UnitPriceInclTax = price / quantity, PriceInclTax = price };
        _items.Add(item);

        var group = _groups.FirstOrDefault(existing => existing.PackNameId == packNameId);

        if (group == null)
        {
            group = new PackNameOrderGroupDTO { PackNameId = packNameId, PackName = name };
            _groups.Add(group);
        }

        group.Items.Add(item);
        setUp?.Invoke(group);
    }

    #endregion

    #region Groups

    [Test]
    public async Task Each_packname_has_its_own_products_count_and_total()
    {
        _packNames.Add(new PackName { Id = 1, Colour = "#f96539" });
        AddFor(1, "Steve", 1, 43.50m, group => { group.Branch = "Branch 1"; group.Department = "Department 1"; });
        AddFor(2, "Olivia", 2, 48m);
        AddFor(2, "Olivia", 1, 15m);

        var model = await PrepareAsync();

        model.PackNameCount.Should().Be(2);
        model.ItemCount.Should().Be(4);
        model.Groups.Select(group => group.PackName).Should().Equal("Steve", "Olivia");
        model.Groups.Select(group => group.ItemCount).Should().Equal(1, 3);
        model.Groups.Select(group => group.Total).Should().Equal("43.50", "63.00");
        model.Groups[0].Details.Should().Be("Branch 1 · Department 1");
        model.Groups[0].Colour.Should().Be("#f96539");
        model.Groups[1].Details.Should().BeEmpty();
        model.Groups[1].Colour.Should().StartWith("#", "a packname that is gone still has a colour to draw");
        model.Groups[1].Lines.Select(line => line.Total).Should().Equal("48.00", "15.00");
    }

    [Test]
    public async Task An_order_without_packnames_is_one_group_without_a_name()
    {
        _order.ShippingMethod = "Ground";
        _items.Add(new OrderItem { Id = 1, ProductId = 1, Quantity = 2, PriceInclTax = 20m });

        var model = await PrepareAsync();

        model.PackNameCount.Should().Be(0);
        model.Groups.Should().ContainSingle();
        model.Groups[0].HasPackName.Should().BeFalse();
        model.Groups[0].Delivery.Should().Be("Ground");
        model.ItemCount.Should().Be(2);
    }

    [TestCase("Ground", false, "Ground, Steve Gates, 12 Campbell Parade, Bondi, New South Wales, 2026")]
    [TestCase(null, false, "Steve Gates, 12 Campbell Parade, Bondi, New South Wales, 2026")]
    [TestCase("Pickup at the office", true, "Pickup at the office")]
    public async Task An_order_without_packnames_says_where_it_is_delivered(string? method, bool pickup, string delivery)
    {
        _order.ShippingMethod = method;
        _order.PickupInStore = pickup;
        _order.ShippingAddressId = 9;
        _address = new Address { FirstName = "Steve", LastName = "Gates", Address1 = "12 Campbell Parade", City = "Bondi", ZipPostalCode = "2026" };
        _items.Add(new OrderItem { Id = 1, ProductId = 1, Quantity = 1, PriceInclTax = 20m });

        (await PrepareAsync()).Groups[0].Delivery.Should().Be(delivery);
    }

    [TestCase("Steve", "Gates", "Steve Gates, 1 George Street, Level 5, Sydney, New South Wales, 2000")]
    [TestCase(null, "Gates", "Gates, 1 George Street, Level 5, Sydney, New South Wales, 2000")]
    [TestCase(null, null, "1 George Street, Level 5, Sydney, New South Wales, 2000")]
    public async Task A_delivery_has_its_second_address_line_and_no_gap_for_a_name_it_lacks(string? firstName, string? lastName, string delivery)
    {
        _order.ShippingAddressId = 9;
        _address = new Address { FirstName = firstName, LastName = lastName, Address1 = "1 George Street", Address2 = "Level 5", City = "Sydney", ZipPostalCode = "2000" };
        _items.Add(new OrderItem { Id = 1, ProductId = 1, Quantity = 1, PriceInclTax = 20m });

        (await PrepareAsync()).Groups[0].Delivery.Should().Be(delivery);
    }

    [Test]
    public async Task An_address_format_that_leaves_fields_out_leaves_no_gaps()
    {
        _formatFields = [new(AddressField.Address1, "1 George Street"), default, default];
        _order.ShippingMethod = "Ground";
        _order.ShippingAddressId = 9;
        _address = new Address();
        _items.Add(new OrderItem { Id = 1, ProductId = 1, Quantity = 1, PriceInclTax = 20m });

        (await PrepareAsync()).Groups[0].Delivery.Should().Be("Ground, 1 George Street");
    }

    [TestCase("Ground", "Ground")]
    [TestCase(null, null)]
    public async Task A_delivery_address_that_is_gone_is_left_out(string? method, string? delivery)
    {
        _order.ShippingMethod = method;
        _order.ShippingAddressId = 9;
        _items.Add(new OrderItem { Id = 1, ProductId = 1, Quantity = 1, PriceInclTax = 20m });

        (await PrepareAsync()).Groups[0].Delivery.Should().Be(delivery);
    }

    [Test]
    public async Task A_line_for_no_packname_is_not_left_out()
    {
        AddFor(1, "Steve", 1, 43.50m);
        _items.Add(new OrderItem { Id = 50, ProductId = 50, Quantity = 1, PriceInclTax = 5m });

        var model = await PrepareAsync();

        model.PackNameCount.Should().Be(1);
        model.Groups.Should().HaveCount(2);
        model.Groups[1].HasPackName.Should().BeFalse();
        model.Groups[1].Delivery.Should().BeNull("each person's block says where that person's products go");
        model.ItemCount.Should().Be(2);
    }

    [TestCase(5, true)]
    [TestCase(6, false)]
    public async Task An_order_for_many_is_not_opened_at_once(int people, bool open)
    {
        for (var id = 1; id <= people; id++)
            AddFor(id, $"Person {id}", 1, 10m);

        (await PrepareAsync()).GroupsOpen.Should().Be(open);
    }

    [Test]
    public async Task A_delivery_says_how_where_and_with_whom()
    {
        AddFor(1, "Steve", 1, 10m, group =>
        {
            group.Shipping = new OrderShipping { ShippingMethod = "Express" };
            group.AddressName = "Reception";
            group.AddressLine1 = "1 George Street";
            group.AddressLine2 = "Level 5";
            group.AddressCityStateZip = "Sydney";
            group.SharedWithPackNames = ["Ann", "Bob", "Cat"];
        });

        (await PrepareAsync()).Groups[0].Delivery.Should().Be("Express, Reception, 1 George Street, Level 5, Sydney (with Ann, Bob, Cat)");

        _groups[0].SharedWithPackNames = ["Ann", "Bob", "Cat", "Dan"];

        (await PrepareAsync()).Groups[0].Delivery.Should().Be("Express, Reception, 1 George Street, Level 5, Sydney");
    }

    [Test]
    public async Task A_delivery_collected_in_store_has_no_address()
    {
        AddFor(1, "Steve", 1, 10m, group =>
        {
            group.Shipping = new OrderShipping { ShippingMethod = "Pickup", PickupInStore = true };
            group.AddressLine1 = "1 George Street";
            group.SharedWithPackNames = ["Ann"];
        });

        (await PrepareAsync()).Groups[0].Delivery.Should().Be("Pickup");
    }

    [Test]
    public async Task A_packname_without_a_delivery_says_nothing_of_it()
    {
        AddFor(1, "Steve", 1, 10m);

        (await PrepareAsync()).Groups[0].Delivery.Should().BeNull();
    }

    #endregion

    #region Totals

    [Test]
    public async Task The_totals_add_up_to_what_is_left_to_pay()
    {
        AddFor(1, "Steve", 1, 43.50m);
        AddFor(2, "Olivia", 8, 192.56m);
        _order.OrderShippingInclTax = 34.90m;
        _order.OrderTotal = 75.95m;
        _settlement = new OrderSettlement { SettleableAmount = 270.96m, SubsidyStockAmount = 180m, SubsidyShippingAmount = 15.01m };

        var model = await PrepareAsync();

        model.Subtotal.Should().Be("236.06");
        model.Delivery.Should().Be("34.90");
        model.Subsidy.Should().Be("-195.01");
        model.StoreCredit.Should().BeNull();
        model.Other.Should().BeNull();
        model.Payable.Should().Be("75.95");
    }

    [TestCase(90, "-10.00")]
    [TestCase(104.5, "4.50")]
    [TestCase(100.01, null)]
    [TestCase(100, null)]
    public async Task Whatever_else_changes_the_total_has_its_row(double total, string? other)
    {
        AddFor(1, "Steve", 1, 120m);
        _order.OrderTotal = (decimal)total;
        _settlement = new OrderSettlement { CreditNoteAmount = 20m };

        var model = await PrepareAsync();

        model.StoreCredit.Should().Be("-20.00");
        model.Delivery.Should().BeNull();
        model.Other.Should().Be(other);
    }

    #endregion

    #region Decided

    [Test]
    public async Task A_link_that_no_longer_decides_does_not_show_what_was_ordered()
    {
        AddFor(1, "Steve", 1, 10m);
        _approval.Status = OrderApprovalStatus.Approved;

        var model = await PrepareAsync();

        model.CanDecide.Should().BeFalse();
        model.StatusClass.Should().Be("approved");
        model.Groups.Should().BeEmpty();
        model.Payable.Should().BeNull();
    }

    #endregion
}
