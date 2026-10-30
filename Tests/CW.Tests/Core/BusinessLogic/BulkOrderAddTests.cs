using AwesomeAssertions;
using Moq;
using Nop.Core;
using Nop.Core.Domain.Catalog;
using Nop.Core.Domain.Customers;
using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Stores;
using Nop.Plugin.CW.Core.BusinessLogic;
using Nop.Plugin.CW.Core.BusinessLogic.Requests;
using Nop.Plugin.CW.Core.Services.BulkOrder;
using Nop.Plugin.CW.Core.Services.Orders;
using Nop.Services.Catalog;
using Nop.Services.Localization;
using Nop.Services.Logging;
using NUnit.Framework;

namespace CW.Tests.Core.BusinessLogic;

/// <summary>
/// The bulk ordering add: each line's options as the product takes them (one value for a single-choice attribute,
/// nothing the grid has no input for), lines for the same product and options added as one, each product's
/// visibility asked once, and the warnings returned as plain text
/// </summary>
[TestFixture]
public class BulkOrderAddTests
{
    private const int ProductId = 50;
    private const int SizeMappingId = 1;      //radio buttons: one value
    private const int ExtrasMappingId = 2;    //checkboxes: several values
    private const int NoteMappingId = 3;      //a text box: no input on the grid

    private static readonly Customer Customer = new() { Id = 5 };

    private BulkOrderLogic _logic = null!;
    private Mock<IBulkOrderService> _bulkOrderService = null!;
    private Mock<IShoppingCartOverrideService> _cartService = null!;
    private List<(string xml, int quantity)> _added = null!;
    private IList<string> _warnings = null!;

    [SetUp]
    public void SetUp()
    {
        _added = new List<(string, int)>();
        _warnings = new List<string>();

        _bulkOrderService = new Mock<IBulkOrderService>();
        _bulkOrderService.Setup(service => service.GetVisibleProductByIdAsync(ProductId, 1)).ReturnsAsync(new Product { Id = ProductId, Name = "Shirt" });
        _bulkOrderService.Setup(service => service.GetCartQuantitiesAsync(It.IsAny<Customer>(), 1)).ReturnsAsync(new Dictionary<int, int>());

        _cartService = new Mock<IShoppingCartOverrideService>();
        _cartService.Setup(service => service.AddToCartAsync(It.IsAny<Customer>(), It.IsAny<Product>(), ShoppingCartType.ShoppingCart, 1,
                It.IsAny<string>(), It.IsAny<decimal>(), null, null, It.IsAny<int>(), true, null))
            .Callback((Customer _, Product _, ShoppingCartType _, int _, string xml, decimal _, DateTime? _, DateTime? _, int quantity, bool _, int? _) =>
                _added.Add((xml, quantity)))
            .ReturnsAsync(() => _warnings);
        _cartService.Setup(service => service.GetShoppingCartAsync(It.IsAny<Customer>(), ShoppingCartType.ShoppingCart, 1, null, null, null, null))
            .ReturnsAsync(new List<ShoppingCartItem>());

        var mappings = new List<ProductAttributeMapping>
        {
            new() { Id = SizeMappingId, ProductId = ProductId, AttributeControlType = AttributeControlType.RadioList },
            new() { Id = ExtrasMappingId, ProductId = ProductId, AttributeControlType = AttributeControlType.Checkboxes },
            new() { Id = NoteMappingId, ProductId = ProductId, AttributeControlType = AttributeControlType.TextBox }
        };
        var attributeService = new Mock<IProductAttributeService>();
        attributeService.Setup(service => service.GetProductAttributeMappingsByProductIdAsync(ProductId)).ReturnsAsync(mappings);
        attributeService.Setup(service => service.GetProductAttributeValuesAsync(SizeMappingId))
            .ReturnsAsync(new List<ProductAttributeValue> { new() { Id = 11 }, new() { Id = 12 } });
        attributeService.Setup(service => service.GetProductAttributeValuesAsync(ExtrasMappingId))
            .ReturnsAsync(new List<ProductAttributeValue> { new() { Id = 21 }, new() { Id = 22 } });

        //a stand-in for nop's attribute XML: "mapping:value;" per value
        var parser = new Mock<IProductAttributeParser>();
        parser.Setup(p => p.AddProductAttribute(It.IsAny<string>(), It.IsAny<ProductAttributeMapping>(), It.IsAny<string>(), null))
            .Returns((string xml, ProductAttributeMapping mapping, string value, int? _) => $"{xml}{mapping.Id}:{value};");
        parser.Setup(p => p.IsConditionMetAsync(It.IsAny<ProductAttributeMapping>(), It.IsAny<string>())).ReturnsAsync((bool?)null);

        var localizationService = new Mock<ILocalizationService>();
        localizationService.Setup(service => service.GetResourceAsync(It.IsAny<string>())).ReturnsAsync((string key) => key);

        var storeContext = new Mock<IStoreContext>();
        storeContext.Setup(context => context.GetCurrentStoreAsync()).ReturnsAsync(new Store { Id = 1 });
        var workContext = new Mock<IWorkContext>();
        workContext.Setup(context => context.GetCurrentCustomerAsync()).ReturnsAsync(Customer);

        _logic = TestObject.Create<BulkOrderLogic>();
        TestObject.Set(_logic, "_bulkOrderService", _bulkOrderService.Object);
        TestObject.Set(_logic, "_shoppingCartService", _cartService.Object);
        TestObject.Set(_logic, "_productAttributeService", attributeService.Object);
        TestObject.Set(_logic, "_productAttributeParser", parser.Object);
        TestObject.Set(_logic, "_localizationService", localizationService.Object);
        TestObject.Set(_logic, "_storeContext", storeContext.Object);
        TestObject.Set(_logic, "_workContext", workContext.Object);
        TestObject.Set(_logic, "_customerActivityService", new Mock<ICustomerActivityService>().Object);
    }

    private static BulkOrderLine Line(int quantity, string cellKey, params BulkOrderSelection[] selections) =>
        new(ProductId, selections, quantity, cellKey);

    private static BulkOrderSelection Size(params int[] valueIds) => new(SizeMappingId, valueIds, null!);

    [Test]
    public async Task A_single_choice_attribute_takes_one_value()
    {
        //posted by hand: Small and Large on one line would add up both price adjustments and match no combination
        await _logic.AddToCartAsync(new BulkOrderAddRequest([Line(1, "a", Size(11, 12))]));

        _added.Should().Equal(("1:11;", 1));
    }

    [Test]
    public async Task A_checkbox_attribute_takes_every_value()
    {
        await _logic.AddToCartAsync(new BulkOrderAddRequest([Line(1, "a", Size(11), new BulkOrderSelection(ExtrasMappingId, [21, 22], null!))]));

        _added.Should().Equal(("1:11;2:21;2:22;", 1));
    }

    [Test]
    public async Task Text_posted_for_an_attribute_the_grid_has_no_input_for_is_not_written()
    {
        await _logic.AddToCartAsync(new BulkOrderAddRequest([Line(1, "a", Size(11), new BulkOrderSelection(NoteMappingId, [], "<free text>"))]));

        _added.Should().Equal(("1:11;", 1));
    }

    [Test]
    public async Task Lines_for_the_same_product_and_options_are_added_as_one()
    {
        var result = await _logic.AddToCartAsync(new BulkOrderAddRequest([Line(2, "a", Size(11)), Line(3, "b", Size(11)), Line(1, "c", Size(12))]));

        _added.Should().Equal(("1:11;", 5), ("1:12;", 1));
        result.Results.Select(line => line.LineIndex).Should().BeEquivalentTo([0, 1, 2], "each line the customer typed gets its outcome");
        result.AddedQuantity.Should().Be(6);
    }

    [Test]
    public async Task Each_products_visibility_is_asked_once()
    {
        await _logic.AddToCartAsync(new BulkOrderAddRequest([Line(2, "a", Size(11)), Line(3, "b", Size(12))]));

        _bulkOrderService.Verify(service => service.GetVisibleProductByIdAsync(ProductId, 1), Times.Once);
    }

    [Test]
    public async Task Warnings_come_back_as_plain_text()
    {
        //a required product's link and an encoded packname name, as nop and CW's cart write them
        _warnings.Add("Smith &amp; Co: ordering is closed.");
        _warnings.Add("This product requires <a href=\"/belt\">Belt &amp; Buckle</a>.");

        var result = await _logic.AddToCartAsync(new BulkOrderAddRequest([Line(1, "a", Size(11))]));

        result.Results.Single().Warnings.Should().Equal("Smith & Co: ordering is closed.", "This product requires Belt & Buckle.");
    }

    [Test]
    public async Task Too_many_lines_at_once_adds_nothing()
    {
        var lines = Enumerable.Range(0, 1001).Select(index => Line(1, $"cell{index}", Size(11))).ToList();

        var result = await _logic.AddToCartAsync(new BulkOrderAddRequest(lines));

        _added.Should().BeEmpty();
        result.Results.Should().ContainSingle().Which.LineIndex.Should().Be(-1);
        _bulkOrderService.Verify(service => service.GetVisibleProductByIdAsync(It.IsAny<int>(), It.IsAny<int>()), Times.Never);
    }
}
