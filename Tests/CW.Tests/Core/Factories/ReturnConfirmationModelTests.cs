using AwesomeAssertions;
using Moq;
using Nop.Core;
using Nop.Core.Domain.Catalog;
using Nop.Core.Domain.Customers;
using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Stores;
using Nop.Plugin.CW.Core.Domain.Settings;
using Nop.Plugin.CW.Core.Factories;
using Nop.Plugin.CW.Core.Services.Exchanges;
using Nop.Services.Catalog;
using Nop.Services.Configuration;
using Nop.Services.Helpers;
using Nop.Services.Localization;
using Nop.Services.Media;
using Nop.Services.Orders;
using Nop.Services.Seo;
using NUnit.Framework;

namespace CW.Tests.Core.Factories;

/// <summary>
/// The panel shown after the wizard (My Returns with ?returnRequests=...) lists only the signed-in customer's own return
/// requests, whatever identifiers the address carries
/// </summary>
[TestFixture]
public class ReturnConfirmationModelTests
{
    private ReturnRequestItemModelFactory _factory = null!;

    [SetUp]
    public void SetUp()
    {
        var othersRequest = new ReturnRequest { Id = 300, CustomerId = 9, OrderItemId = 200, Quantity = 1 };

        //the lookup by identifier finds another customer's request; the customer's own lookup finds none
        var exchangeService = new Mock<IExchangeService>();
        exchangeService.Setup(service => service.GetReturnRequestsById(It.IsAny<int[]>())).Returns(new List<ReturnRequest> { othersRequest });
        exchangeService.SetReturnsDefault<Task<IList<ReturnRequest>>>(Task.FromResult<IList<ReturnRequest>>(new List<ReturnRequest>()));

        var orderService = new Mock<IOrderService>();
        orderService.Setup(service => service.GetOrderItemByIdAsync(200)).ReturnsAsync(new OrderItem { Id = 200, OrderId = 20, ProductId = 50 });
        var productService = new Mock<IProductService>();
        productService.Setup(service => service.GetProductByIdAsync(50)).ReturnsAsync(new Product { Id = 50, Name = "Shirt" });

        var workContext = new Mock<IWorkContext>();
        workContext.Setup(context => context.GetCurrentCustomerAsync()).ReturnsAsync(new Customer { Id = 5 });
        var storeContext = new Mock<IStoreContext>();
        storeContext.Setup(context => context.GetCurrentStoreAsync()).ReturnsAsync(new Store { Id = 1 });
        var settingService = new Mock<ISettingService>();
        settingService.Setup(service => service.LoadSettingAsync<MultiStoreSettings>(It.IsAny<int>())).ReturnsAsync(new MultiStoreSettings());

        _factory = TestObject.Create<ReturnRequestItemModelFactory>();
        TestObject.Set(_factory, "_exchangeService", exchangeService.Object);
        TestObject.Set(_factory, "_orderService", orderService.Object);
        TestObject.Set(_factory, "_productService", productService.Object);
        TestObject.Set(_factory, "_workContext", workContext.Object, optional: true);
        TestObject.Set(_factory, "_storeContext", storeContext.Object);
        TestObject.Set(_factory, "_settingService", settingService.Object);
        TestObject.Set(_factory, "_returnRequestSettings", new ReturnRequestSettings(), optional: true);
        TestObject.Set(_factory, "_localizationService", new Mock<ILocalizationService>().Object);
        TestObject.Set(_factory, "_downloadService", new Mock<IDownloadService>().Object);
        TestObject.Set(_factory, "_urlRecordService", new Mock<IUrlRecordService>().Object);
        TestObject.Set(_factory, "_dateTimeHelper", new Mock<IDateTimeHelper>().Object);
        TestObject.Set(_factory, "_returnRequestService", new Mock<IReturnRequestService>().Object);
        TestObject.Set(_factory, "_exchangesDisplayService", new Mock<IExchangesDisplayService>().Object);
        TestObject.Set(_factory, "_exchangeMessageService", new Mock<Nop.Plugin.CW.Core.Services.Messages.IExchangeMessageService>().Object);
        TestObject.Set(_factory, "_logger", new Mock<Nop.Services.Logging.ILogger>().Object);
    }

    [Test]
    public async Task Another_customers_return_request_is_not_shown()
    {
        var model = await _factory.PrepareReturnExchangesConfirmationModelAsync([300]);

        model.Items.Should().BeEmpty("its product, packname and receipt text are another customer's");
    }
}
