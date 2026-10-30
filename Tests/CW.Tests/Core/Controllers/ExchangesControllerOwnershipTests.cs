using AwesomeAssertions;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Primitives;
using Moq;
using Nop.Core;
using Nop.Core.Domain.Catalog;
using Nop.Core.Domain.Customers;
using Nop.Core.Domain.Orders;
using Nop.Plugin.CW.Core.Controllers;
using Nop.Plugin.CW.Core.Domain.Settings;
using Nop.Plugin.CW.Core.Factories;
using Nop.Plugin.CW.Core.Models.Exchanges;
using Nop.Plugin.CW.Core.Services.Common;
using Nop.Plugin.CW.Core.Services.Settings;
using Nop.Services.Catalog;
using Nop.Services.Localization;
using Nop.Services.Messages;
using Nop.Services.Orders;
using Nop.Services.Security;
using Nop.Services.Stores;
using Nop.Web.Models.Catalog;
using NUnit.Framework;

namespace CW.Tests.Core.Controllers;

/// <summary>
/// The returns wizard's controller serves only the signed-in customer's own orders, items and return requests, and
/// checks the antiforgery token on every post
/// </summary>
[TestFixture]
public class ExchangesControllerOwnershipTests
{
    private const int CustomerId = 5;
    private const int OtherCustomerId = 9;

    private ExchangesController _controller = null!;
    private Mock<IOrderService> _orderService = null!;
    private Mock<IProductService> _productService = null!;
    private Mock<IReturnRequestService> _returnRequestService = null!;
    private Mock<IExchangesModelFactory> _exchangesModelFactory = null!;
    private Mock<ICWPdfService> _pdfService = null!;

    [SetUp]
    public void SetUp()
    {
        _orderService = new Mock<IOrderService>();
        _orderService.Setup(service => service.GetOrderByIdAsync(10)).ReturnsAsync(new Order { Id = 10, CustomerId = CustomerId });
        _orderService.Setup(service => service.GetOrderByIdAsync(20)).ReturnsAsync(new Order { Id = 20, CustomerId = OtherCustomerId });
        _orderService.Setup(service => service.GetOrderItemByIdAsync(100)).ReturnsAsync(new OrderItem { Id = 100, OrderId = 10, ProductId = 1 });
        _orderService.Setup(service => service.GetOrderItemByIdAsync(200)).ReturnsAsync(new OrderItem { Id = 200, OrderId = 20, ProductId = 1 });

        _productService = new Mock<IProductService>();
        _productService.Setup(service => service.GetProductByIdAsync(1)).ReturnsAsync(new Product { Id = 1, Published = true });
        _productService.Setup(service => service.GetProductByIdAsync(2)).ReturnsAsync(new Product { Id = 2, Published = false });

        _returnRequestService = new Mock<IReturnRequestService>();
        _returnRequestService.Setup(service => service.GetReturnRequestByIdAsync(300)).ReturnsAsync(new ReturnRequest { Id = 300, OrderItemId = 200, CustomerId = OtherCustomerId });

        var workContext = new Mock<IWorkContext>();
        workContext.Setup(context => context.GetCurrentCustomerAsync()).ReturnsAsync(new Customer { Id = CustomerId });

        var orderProcessingService = new Mock<IOrderProcessingService>();
        orderProcessingService.Setup(service => service.IsReturnRequestAllowedAsync(It.IsAny<Order>())).ReturnsAsync(true);

        var aclService = new Mock<IAclService>();
        aclService.Setup(service => service.AuthorizeAsync(It.IsAny<Product>())).ReturnsAsync(true);
        var storeMappingService = new Mock<IStoreMappingService>();
        storeMappingService.Setup(service => service.AuthorizeAsync(It.IsAny<Product>())).ReturnsAsync(true);

        var validator = new Mock<IValidator<ReturnWizardSubmission>>();
        validator.Setup(v => v.ValidateAsync(It.IsAny<ReturnWizardSubmission>(), It.IsAny<CancellationToken>())).ReturnsAsync(new ValidationResult());

        _exchangesModelFactory = new Mock<IExchangesModelFactory>();
        _exchangesModelFactory.Setup(factory => factory.PrepareReplacementProductOptionsModelAsync(It.IsAny<Product>(), It.IsAny<int>()))
            .ReturnsAsync(new ReplacementProductOptionsModel { Product = new ProductDetailsModel() });
        _exchangesModelFactory.Setup(factory => factory.PrepareReplacementProductListModelAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>()))
            .ReturnsAsync(new ReplacementProductListModel());

        _pdfService = new Mock<ICWPdfService>();

        var storeContext = new Mock<IStoreContext>();
        storeContext.Setup(context => context.GetCurrentStoreAsync()).ReturnsAsync(new Nop.Core.Domain.Stores.Store { Id = 1 });

        _controller = TestObject.Create<ExchangesController>();
        TestObject.Set(_controller, "_orderService", _orderService.Object);
        TestObject.Set(_controller, "_productService", _productService.Object);
        TestObject.Set(_controller, "_returnRequestService", _returnRequestService.Object);
        TestObject.Set(_controller, "_workContext", workContext.Object);
        TestObject.Set(_controller, "_orderProcessingService", orderProcessingService.Object);
        TestObject.Set(_controller, "_aclService", aclService.Object);
        TestObject.Set(_controller, "_storeMappingService", storeMappingService.Object, optional: true);
        TestObject.Set(_controller, "_wizardValidator", validator.Object);
        TestObject.Set(_controller, "_exchangesModelFactory", _exchangesModelFactory.Object);
        TestObject.Set(_controller, "_cwPdfService", _pdfService.Object);
        TestObject.Set(_controller, "_cwFeatureService", new Mock<ICWFeatureService>().Object);
        TestObject.Set(_controller, "_storeContext", storeContext.Object);
        //the customer has no return request among the ones asked for
        var exchangeService = new Mock<Nop.Plugin.CW.Core.Services.Exchanges.IExchangeService>();
        exchangeService.SetReturnsDefault<Task<IList<ReturnRequest>>>(Task.FromResult<IList<ReturnRequest>>(new List<ReturnRequest>()));
        TestObject.Set(_controller, "_exchangeService", exchangeService.Object);
        TestObject.Set(_controller, "_notificationService", new Mock<INotificationService>().Object);
        TestObject.Set(_controller, "_localizationService", new Mock<ILocalizationService>().Object);
    }

    private static IFormCollection Form(params (string key, string value)[] fields) =>
        new FormCollection(fields.ToDictionary(field => field.key, field => new StringValues(field.value)));

    [Test]
    public void Every_post_of_the_wizard_checks_the_antiforgery_token()
    {
        typeof(ExchangesController).GetCustomAttributes(typeof(AutoValidateAntiforgeryTokenAttribute), true)
            .Should().NotBeEmpty("a page on another site could otherwise post returns for a signed-in customer");
    }

    [Test]
    public async Task The_summary_of_another_customers_order_is_refused()
    {
        var result = await _controller.DisplayReturnRequestSummary(new ReturnRequestGatherInfoModel { OrderId = 20 },
            Form(("exchange_mode", "2"), ("returned_item_qty_200", "1")));

        result.Should().BeOfType<ChallengeResult>();
        _exchangesModelFactory.Verify(factory => factory.RebuildGatheredInfoAsync(It.IsAny<ReturnRequestGatherInfoModel>(), It.IsAny<ReturnWizardSubmission>()), Times.Never);
    }

    [Test]
    public async Task The_picker_refuses_an_item_of_another_customers_order()
    {
        var result = await _controller.ReplacementProductOptions(1, 200, null);

        result.Should().BeOfType<NotFoundResult>();
    }

    [Test]
    public async Task The_picker_grid_refuses_an_item_of_another_customers_order()
    {
        var result = await _controller.ReplacementProductList(200, 1, null!, 0);

        result.Should().BeOfType<NotFoundResult>();
    }

    [Test]
    public async Task The_picker_refuses_an_unpublished_product()
    {
        var result = await _controller.ReplacementProductOptions(2, 100, null);

        result.Should().BeOfType<NotFoundResult>();
    }

    [Test]
    public async Task The_picker_refuses_the_options_of_another_customers_exchange()
    {
        var result = await _controller.ReplacementProductOptions(1, 100, 300);

        result.Should().BeOfType<NotFoundResult>();
        _exchangesModelFactory.Verify(factory => factory.PreselectCurrentAttributesAsync(It.IsAny<ProductDetailsModel>(), It.IsAny<int?>(), It.IsAny<int>()), Times.Never);
    }

    [Test]
    public async Task The_picker_opens_for_an_item_of_the_customers_own_order()
    {
        var result = await _controller.ReplacementProductOptions(1, 100, null);

        result.Should().BeOfType<PartialViewResult>();
    }

    [Test]
    public async Task The_receipt_of_another_customers_return_request_is_not_printed()
    {
        await _controller.GenerateReturnRequestReceipt(300);

        _pdfService.Verify(service => service.PrintReturnExchangeRequestToPdfAsync(It.IsAny<Stream>(), It.IsAny<ReturnRequest>()), Times.Never);
    }

    [Test]
    public async Task The_receipts_of_another_customers_return_requests_are_not_printed()
    {
        await _controller.GenerateReturnRequestReceipts([300]);

        _pdfService.Verify(service => service.PrintReturnExchangeRequestsToPdfAsync(It.IsAny<Stream>(),
            It.Is<IList<ReturnRequest>>(requests => requests.Count > 0)), Times.Never);
    }
}
