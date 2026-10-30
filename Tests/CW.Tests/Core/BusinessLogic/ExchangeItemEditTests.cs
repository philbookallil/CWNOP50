using AwesomeAssertions;
using Moq;
using Nop.Core.Domain.Catalog;
using Nop.Plugin.CW.Core.BusinessLogic;
using Nop.Plugin.CW.Core.Domain.Exchanges;
using Nop.Plugin.CW.Core.Domain.Products;
using Nop.Plugin.CW.Core.Services.Exchanges;
using Nop.Services.Catalog;
using NUnit.Framework;

namespace CW.Tests.Core.BusinessLogic;

/// <summary>
/// Changing a pending exchange's options from My Returns changes the options posted and keeps the others
/// </summary>
[TestFixture]
public class ExchangeItemEditTests
{
    private const int SizeMappingId = 1;
    private const int ColourMappingId = 2;

    private ExchangesLogic _logic = null!;
    private ReturnReplacementItem _replacement = null!;
    private ReturnReplacementItem? _saved;

    [SetUp]
    public void SetUp()
    {
        //the replacement is size M (11) in red (21)
        _replacement = new ReturnReplacementItem { Id = 3, ProductId = 50, AttributesXml = "1:11;2:21;" };
        _saved = null;

        var exchangeService = new Mock<IExchangeService>();
        exchangeService.Setup(service => service.GetReplacementItemAsync(7)).ReturnsAsync(_replacement);
        exchangeService.Setup(service => service.UpdateReplacementItemAsync(It.IsAny<ReturnReplacementItem>()))
            .Callback((ReturnReplacementItem item) => _saved = item)
            .Returns(Task.CompletedTask);

        var productService = new Mock<IProductService>();
        productService.Setup(service => service.GetProductByIdAsync(50)).ReturnsAsync(new Product { Id = 50 });

        var mappings = new List<ProductAttributeMapping> { new() { Id = SizeMappingId, ProductId = 50 }, new() { Id = ColourMappingId, ProductId = 50 } };
        var attributeService = new Mock<IProductAttributeService>();
        attributeService.Setup(service => service.GetProductAttributeMappingsByProductIdAsync(50)).ReturnsAsync(mappings);
        attributeService.Setup(service => service.GetProductAttributeValuesAsync(SizeMappingId))
            .ReturnsAsync(new List<ProductAttributeValue> { new() { Id = 11 }, new() { Id = 12 } });
        attributeService.Setup(service => service.GetProductAttributeValuesAsync(ColourMappingId))
            .ReturnsAsync(new List<ProductAttributeValue> { new() { Id = 21 }, new() { Id = 22 } });

        //a stand-in for nop's attribute XML: "mapping:value;" per option
        var parser = new Mock<IProductAttributeParser>();
        parser.Setup(p => p.ParseValues(It.IsAny<string>(), It.IsAny<int>()))
            .Returns((string xml, int mappingId) => (xml ?? string.Empty).Split(';', StringSplitOptions.RemoveEmptyEntries)
                .Select(pair => pair.Split(':')).Where(pair => pair[0] == mappingId.ToString()).Select(pair => pair[1]).ToList());
        parser.Setup(p => p.AddProductAttribute(It.IsAny<string>(), It.IsAny<ProductAttributeMapping>(), It.IsAny<string>(), null))
            .Returns((string xml, ProductAttributeMapping mapping, string value, int? _) => $"{xml}{mapping.Id}:{value};");

        var formatter = new Mock<IProductAttributeFormatter>();
        formatter.Setup(f => f.FormatAttributesAsync(It.IsAny<Product>(), It.IsAny<string>())).ReturnsAsync("formatted");

        _logic = TestObject.Create<ExchangesLogic>();
        TestObject.Set(_logic, "_exchangeService", exchangeService.Object);
        TestObject.Set(_logic, "_productService", productService.Object);
        TestObject.Set(_logic, "_productAttributeService", attributeService.Object);
        TestObject.Set(_logic, "_productAttributeParser", parser.Object);
        TestObject.Set(_logic, "_productAttributeFormatter", formatter.Object);
    }

    [Test]
    public async Task A_changed_colour_keeps_the_size()
    {
        await _logic.ModifyExchangedItemDetailsAsync(new ReturnRequestItem { Id = 7 },
            [new SelectedProductAttribute { AttributeId = ColourMappingId, AttributeValueId = 22 }]);

        _saved.Should().NotBeNull();
        _saved!.AttributesXml.Should().Be("1:11;2:22;", "the size the customer did not post stays M");
    }

    [Test]
    public async Task Both_options_changed_are_both_saved()
    {
        await _logic.ModifyExchangedItemDetailsAsync(new ReturnRequestItem { Id = 7 },
        [
            new SelectedProductAttribute { AttributeId = SizeMappingId, AttributeValueId = 12 },
            new SelectedProductAttribute { AttributeId = ColourMappingId, AttributeValueId = 22 }
        ]);

        _saved!.AttributesXml.Should().Be("1:12;2:22;");
    }
}
