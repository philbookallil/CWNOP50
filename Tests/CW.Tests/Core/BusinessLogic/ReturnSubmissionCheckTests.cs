using AwesomeAssertions;
using Moq;
using Nop.Core;
using Nop.Core.Domain.Catalog;
using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Stores;
using Nop.Plugin.CW.Core.BusinessLogic;
using Nop.Plugin.CW.Core.Domain.Exchanges;
using Nop.Plugin.CW.Core.Domain.Orders;
using Nop.Plugin.CW.Core.Domain.PackNames;
using Nop.Plugin.CW.Core.Domain.Settings;
using Nop.Plugin.CW.Core.Models.Exchanges;
using Nop.Plugin.CW.Core.Services.Exchanges;
using Nop.Plugin.CW.Core.Services.Orders;
using Nop.Plugin.CW.Core.Services.PackNames;
using Nop.Plugin.CW.Core.Services.Settings;
using Nop.Services.Catalog;
using Nop.Services.Localization;
using Nop.Services.Orders;
using Nop.Services.Security;
using Nop.Services.Stores;
using NUnit.Framework;

namespace CW.Tests.Core.BusinessLogic;

/// <summary>
/// The server checks of a wizard submission: whatever the browser posts, only returnable units, an action offered for
/// the reason, a mode offered for both, and a replacement that fits its exchange type go on
/// </summary>
[TestFixture]
public class ReturnSubmissionCheckTests
{
    private const int ReasonId = 1;
    private const int ActionId = 2;
    private const int OrderItemId = 100;
    private const int ReturnedProductId = 50;
    private const int OtherProductId = 60;
    private const int HiddenProductId = 70;
    private const int GiftCardProductId = 80;
    private const int OptionsProductId = 90;
    private const int SizeMappingId = 900;

    private static readonly Order Order = new() { Id = 10, CustomerId = 5 };

    private ReturnSubmissionLogic _logic = null!;
    private Mock<IExchangeService> _exchangeService = null!;
    private Dictionary<int, int> _returnable = null!;
    private Mock<IAclService> _aclService = null!;
    private Mock<IProductService> _productService = null!;
    private (PackName? packName, ExchangeType? replacement) _aclScope;

    [SetUp]
    public void SetUp()
    {
        _returnable = new Dictionary<int, int> { [OrderItemId] = 2 };

        _exchangeService = new Mock<IExchangeService>();
        _exchangeService.Setup(service => service.GetReturnableQuantitiesAsync(Order)).ReturnsAsync(() => _returnable);
        _exchangeService.Setup(service => service.GetActiveActionsByReasonIdAsync(ReasonId, 1))
            .ReturnsAsync(new List<ReturnRequestAction> { new() { Id = ActionId } });
        _exchangeService.Setup(service => service.DetermineEnabledReturnModesAsync(1, ReasonId, ActionId))
            .ReturnsAsync(new List<ExchangeReturnMode> { ExchangeReturnMode.Exchange });
        _exchangeService.Setup(service => service.GetActiveReasonIdsAsync(1)).ReturnsAsync(new List<int> { ReasonId });

        var returnRequestService = new Mock<IReturnRequestService>();
        returnRequestService.Setup(service => service.GetReturnRequestReasonByIdAsync(ReasonId)).ReturnsAsync(new ReturnRequestReason { Id = ReasonId });

        var orderService = new Mock<IOrderService>();
        orderService.Setup(service => service.GetOrderItemsAsync(Order.Id, null, null, 0))
            .ReturnsAsync(new List<OrderItem> { new() { Id = OrderItemId, OrderId = Order.Id, ProductId = ReturnedProductId, Quantity = 3 } });

        _productService = new Mock<IProductService>();
        _productService.Setup(service => service.GetProductByIdAsync(ReturnedProductId)).ReturnsAsync(new Product { Id = ReturnedProductId, Published = true, VisibleIndividually = true });
        _productService.Setup(service => service.GetProductByIdAsync(OtherProductId)).ReturnsAsync(new Product { Id = OtherProductId, Published = true, VisibleIndividually = true });
        _productService.Setup(service => service.GetProductByIdAsync(HiddenProductId)).ReturnsAsync(new Product { Id = HiddenProductId, Published = false, VisibleIndividually = true });
        _productService.Setup(service => service.GetProductByIdAsync(GiftCardProductId)).ReturnsAsync(new Product { Id = GiftCardProductId, Published = true, VisibleIndividually = true, IsGiftCard = true });
        _productService.Setup(service => service.GetProductByIdAsync(OptionsProductId)).ReturnsAsync(new Product { Id = OptionsProductId, Published = true, VisibleIndividually = true });

        //the product with options needs its size
        var attributeService = new Mock<IProductAttributeService>();
        attributeService.SetReturnsDefault<Task<IList<ProductAttributeMapping>>>(Task.FromResult<IList<ProductAttributeMapping>>(new List<ProductAttributeMapping>()));
        attributeService.Setup(service => service.GetProductAttributeMappingsByProductIdAsync(OptionsProductId))
            .ReturnsAsync(new List<ProductAttributeMapping> { new() { Id = SizeMappingId, ProductId = OptionsProductId, IsRequired = true } });
        attributeService.Setup(service => service.GetProductAttributeValuesAsync(SizeMappingId))
            .ReturnsAsync(new List<ProductAttributeValue> { new() { Id = 901 }, new() { Id = 902 } });

        //the item was ordered for packname 7; the ACL check records the catalogue it ran in
        var featureService = new Mock<ICWFeatureService>();
        featureService.Setup(service => service.IsEnabledAsync(CWFeature.PackName, It.IsAny<int?>())).ReturnsAsync(true);
        var cwOrderService = new Mock<ICWOrderService>();
        cwOrderService.Setup(service => service.GetPackNameOrderItemAsync(OrderItemId)).ReturnsAsync(new PackNameOrderItem { OrderItemId = OrderItemId, PackNameId = 7 });
        var packNameService = new Mock<IPackNameService>();
        packNameService.Setup(service => service.GetPackNameByIdAsync(7)).ReturnsAsync(new PackName { Id = 7 });

        _aclScope = (null, null);
        _aclService = new Mock<IAclService>();
        _aclService.Setup(service => service.AuthorizeAsync(It.IsAny<Product>()))
            .Callback(() => _aclScope = (PackNameCatalogScope.Current, PackNameCatalogScope.CurrentReplacement))
            .ReturnsAsync(true);
        var storeMappingService = new Mock<IStoreMappingService>();
        storeMappingService.Setup(service => service.AuthorizeAsync(It.IsAny<Product>())).ReturnsAsync(true);

        var storeContext = new Mock<IStoreContext>();
        storeContext.Setup(context => context.GetCurrentStoreAsync()).ReturnsAsync(new Store { Id = 1 });

        var localizationService = new Mock<ILocalizationService>();
        localizationService.Setup(service => service.GetResourceAsync(It.IsAny<string>())).ReturnsAsync((string key) => key[(key.LastIndexOf('.') + 1)..]);

        _logic = TestObject.Create<ReturnSubmissionLogic>();
        TestObject.Set(_logic, "_exchangeService", _exchangeService.Object);
        TestObject.Set(_logic, "_returnRequestService", returnRequestService.Object);
        TestObject.Set(_logic, "_orderService", orderService.Object);
        TestObject.Set(_logic, "_productService", _productService.Object);
        TestObject.Set(_logic, "_aclService", _aclService.Object);
        TestObject.Set(_logic, "_productAttributeService", attributeService.Object);
        TestObject.Set(_logic, "_cwFeatureService", featureService.Object);
        TestObject.Set(_logic, "_cwOrderService", cwOrderService.Object);
        TestObject.Set(_logic, "_packNameService", packNameService.Object);
        TestObject.Set(_logic, "_storeMappingService", storeMappingService.Object);
        TestObject.Set(_logic, "_storeContext", storeContext.Object);
        TestObject.Set(_logic, "_localizationService", localizationService.Object);
    }

    private static ReturnWizardSubmission Submission(ExchangeReturnMode? mode, int quantity = 1, ExchangeType? type = null, int? replacementId = null,
        Dictionary<int, int>? attributes = null) => new()
    {
        Mode = mode,
        Lines = [new ReturnWizardLine { OrderItemId = OrderItemId, Quantity = quantity, ExchangeType = type, ReplacementProductId = replacementId, ReplacementAttributes = attributes ?? new Dictionary<int, int>() }]
    };

    private Task<IList<string>> CheckAsync(ReturnWizardSubmission submission, int actionId = ActionId) =>
        _logic.CheckSubmissionAsync(Order, ReasonId, actionId, submission);

    [TestCase(ExchangeType.Exact, null)]
    [TestCase(ExchangeType.SameStyle, ReturnedProductId)]
    [TestCase(ExchangeType.DifferentStyle, OtherProductId)]
    public async Task An_exchange_that_fits_its_type_goes_on(ExchangeType type, int? replacementId)
    {
        (await CheckAsync(Submission(ExchangeReturnMode.Exchange, 2, type, replacementId))).Should().BeEmpty();
    }

    [Test]
    public async Task The_first_step_checks_the_items_reason_and_action_only()
    {
        (await CheckAsync(Submission(null, 2))).Should().BeEmpty();
    }

    [Test]
    public async Task More_units_than_can_be_returned_are_refused()
    {
        (await CheckAsync(Submission(null, 50))).Should().Equal("QuantityNotAvailable");
    }

    [Test]
    public async Task An_item_that_cannot_be_returned_is_refused()
    {
        _returnable.Clear();

        (await CheckAsync(Submission(null, 1))).Should().Equal("QuantityNotAvailable");
    }

    [Test]
    public async Task A_negative_quantity_is_refused()
    {
        (await CheckAsync(Submission(null, -1))).Should().Equal("QuantityNotAvailable");
    }

    [Test]
    public async Task An_action_not_offered_for_the_reason_is_refused()
    {
        (await CheckAsync(Submission(null), actionId: 99)).Should().Equal("ReasonActionNotAvailable");
    }

    [Test]
    public async Task A_reason_whose_rule_is_inactive_is_refused()
    {
        _exchangeService.Setup(service => service.GetActiveReasonIdsAsync(1)).ReturnsAsync(new List<int>());

        (await CheckAsync(Submission(null))).Should().Equal("ReasonActionNotAvailable");
    }

    [Test]
    public async Task A_mode_not_offered_for_the_reason_and_action_is_refused()
    {
        (await CheckAsync(Submission(ExchangeReturnMode.CreditNote))).Should().Equal("ModeNotAvailable");
    }

    [Test]
    public async Task A_same_style_exchange_for_another_product_is_refused()
    {
        //a same-style exchange is not checked against the packname's allocation, so it may only change the options
        (await CheckAsync(Submission(ExchangeReturnMode.Exchange, 1, ExchangeType.SameStyle, OtherProductId))).Should().Equal("ReplacementNotAvailable");
    }

    [Test]
    public async Task A_different_style_exchange_for_the_returned_product_is_refused()
    {
        (await CheckAsync(Submission(ExchangeReturnMode.Exchange, 1, ExchangeType.DifferentStyle, ReturnedProductId))).Should().Equal("ReplacementNotAvailable");
    }

    [Test]
    public async Task An_exact_exchange_with_a_replacement_is_refused()
    {
        (await CheckAsync(Submission(ExchangeReturnMode.Exchange, 1, ExchangeType.Exact, OtherProductId))).Should().Equal("ReplacementNotAvailable");
    }

    [Test]
    public async Task A_replacement_not_on_sale_is_refused()
    {
        (await CheckAsync(Submission(ExchangeReturnMode.Exchange, 1, ExchangeType.DifferentStyle, HiddenProductId))).Should().Equal("ReplacementNotAvailable");
    }

    [Test]
    public async Task A_replacement_posted_with_a_credit_note_is_not_used()
    {
        _exchangeService.Setup(service => service.DetermineEnabledReturnModesAsync(1, ReasonId, ActionId))
            .ReturnsAsync(new List<ExchangeReturnMode> { ExchangeReturnMode.CreditNote });

        (await CheckAsync(Submission(ExchangeReturnMode.CreditNote, 1, ExchangeType.SameStyle, HiddenProductId))).Should().BeEmpty();
    }

    [Test]
    public async Task A_different_style_replacement_is_checked_in_the_items_packname_catalogue()
    {
        await CheckAsync(Submission(ExchangeReturnMode.Exchange, 1, ExchangeType.DifferentStyle, OtherProductId));

        //as the picker offered it, not the catalogue of the packname selected now
        _aclScope.packName!.Id.Should().Be(7);
        _aclScope.replacement.Should().Be(ExchangeType.DifferentStyle);
    }

    [Test]
    public async Task A_same_style_exchange_of_a_product_no_longer_published_goes_on()
    {
        //the customer already has the product: changing its options needs no catalogue
        _productService.Setup(service => service.GetProductByIdAsync(ReturnedProductId)).ReturnsAsync(new Product { Id = ReturnedProductId, Published = false });

        (await CheckAsync(Submission(ExchangeReturnMode.Exchange, 1, ExchangeType.SameStyle, ReturnedProductId))).Should().BeEmpty();
    }

    [Test]
    public async Task A_replacement_the_picker_cannot_take_is_refused()
    {
        //a gift card needs details the picker has no field for
        (await CheckAsync(Submission(ExchangeReturnMode.Exchange, 1, ExchangeType.DifferentStyle, GiftCardProductId))).Should().Equal("ReplacementNotAvailable");
    }

    [Test]
    public async Task A_replacement_without_a_required_option_is_refused()
    {
        (await CheckAsync(Submission(ExchangeReturnMode.Exchange, 1, ExchangeType.DifferentStyle, OptionsProductId))).Should().Equal("ReplacementNotAvailable");
        (await CheckAsync(Submission(ExchangeReturnMode.Exchange, 1, ExchangeType.DifferentStyle, OptionsProductId, new Dictionary<int, int> { [SizeMappingId] = 999 })))
            .Should().Equal(new[] { "ReplacementNotAvailable" }, "999 is not one of the product's sizes");
    }

    [Test]
    public async Task A_replacement_with_its_required_option_goes_on()
    {
        (await CheckAsync(Submission(ExchangeReturnMode.Exchange, 1, ExchangeType.DifferentStyle, OptionsProductId, new Dictionary<int, int> { [SizeMappingId] = 902 })))
            .Should().BeEmpty();
    }
}
