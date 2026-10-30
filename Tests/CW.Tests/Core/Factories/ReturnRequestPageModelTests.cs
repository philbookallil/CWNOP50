using System.Linq.Expressions;
using AwesomeAssertions;
using Moq;
using Nop.Core;
using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Stores;
using Nop.Plugin.CW.Core.Domain.Settings;
using Nop.Plugin.CW.Core.Factories;
using Nop.Plugin.CW.Core.Models.ReturnRequests;
using Nop.Plugin.CW.Core.Services.Exchanges;
using Nop.Plugin.CW.Core.Services.Settings;
using Nop.Services.Localization;
using Nop.Services.Orders;
using Nop.Web.Framework.Mvc.Routing;
using Nop.Web.Models.Order;
using NUnit.Framework;

namespace CW.Tests.Core.Factories;

/// <summary>
/// CW's return page keeps what nop 5.00 puts on it (the page's titles, the submit text, the withdrawal token). With
/// Exchanges on, the store's rules decide its reasons and actions, and it opens with a reason chosen and only that
/// reason's actions; with Exchanges off it keeps nop's lists
/// </summary>
[TestFixture]
public class ReturnRequestPageModelTests
{
    private static readonly Order Order = new() { Id = 10, CustomOrderNumber = "R-10", CustomerId = 5 };

    private ReturnRequestOverrideModelFactory _factory = null!;
    private Mock<IExchangeService> _exchangeService = null!;
    private Mock<ICWFeatureService> _featureService = null!;

    [SetUp]
    public void SetUp()
    {
        var localizationService = new Mock<ILocalizationService>();
        localizationService.Setup(service => service.GetResourceAsync(It.IsAny<string>())).ReturnsAsync((string key) => key == "ReturnRequests.Title" ? "Return items of {1} ({0})" : key);
        localizationService.Setup(service => service.GetLocalizedAsync(It.IsAny<ReturnRequestReason>(), It.IsAny<Expression<Func<ReturnRequestReason, string>>>(), null, true, true))
            .ReturnsAsync((ReturnRequestReason reason, Expression<Func<ReturnRequestReason, string>> _, int? _, bool _, bool _) => reason.Name);
        localizationService.Setup(service => service.GetLocalizedAsync(It.IsAny<ReturnRequestAction>(), It.IsAny<Expression<Func<ReturnRequestAction, string>>>(), null, true, true))
            .ReturnsAsync((ReturnRequestAction action, Expression<Func<ReturnRequestAction, string>> _, int? _, bool _, bool _) => action.Name);

        var returnRequestService = new Mock<IReturnRequestService>();
        returnRequestService.Setup(service => service.GetAllReturnRequestReasonsAsync())
            .ReturnsAsync(new List<ReturnRequestReason> { new() { Id = 1, Name = "Wrong size" }, new() { Id = 2, Name = "Damaged" } });
        returnRequestService.Setup(service => service.GetAllReturnRequestActionsAsync())
            .ReturnsAsync(new List<ReturnRequestAction> { new() { Id = 7, Name = "Repair" }, new() { Id = 8, Name = "Replace" } });

        _exchangeService = new Mock<IExchangeService>();
        _exchangeService.Setup(service => service.GetActiveActionsByReasonIdAsync(It.IsAny<int>(), 1))
            .ReturnsAsync(new List<ReturnRequestAction> { new() { Id = 7, Name = "Repair" }, new() { Id = 8, Name = "Replace" } });
        _exchangeService.Setup(service => service.GetActiveActionsByReasonIdAsync(1, 1))
            .ReturnsAsync(new List<ReturnRequestAction> { new() { Id = 8, Name = "Replace" } });
        _exchangeService.SetReturnsDefault<Task<IDictionary<int, int>>>(Task.FromResult<IDictionary<int, int>>(new Dictionary<int, int>()));

        var orderService = new Mock<IOrderService>();
        orderService.SetReturnsDefault<Task<IList<OrderItem>>>(Task.FromResult<IList<OrderItem>>(new List<OrderItem>()));

        var nopUrlHelper = new Mock<INopUrlHelper>();
        nopUrlHelper.Setup(helper => helper.RouteUrl(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns("/orderdetails/10");

        var storeContext = new Mock<IStoreContext>();
        storeContext.Setup(context => context.GetCurrentStoreAsync()).ReturnsAsync(new Store { Id = 1 });

        _featureService = new Mock<ICWFeatureService>();
        _exchangeService.Setup(service => service.GetActiveReasonIdsAsync(1)).ReturnsAsync(new List<int> { 2 });

        _factory = TestObject.Create<ReturnRequestOverrideModelFactory>();
        TestObject.Set(_factory, "_cwFeatureService", _featureService.Object, optional: true);
        TestObject.Set(_factory, "_localizationService", localizationService.Object);
        TestObject.Set(_factory, "_returnRequestService", returnRequestService.Object);
        TestObject.Set(_factory, "_exchangeService", _exchangeService.Object);
        TestObject.Set(_factory, "_orderService", orderService.Object);
        TestObject.Set(_factory, "_nopUrlHelper", nopUrlHelper.Object);
        TestObject.Set(_factory, "_storeContext", storeContext.Object);
        TestObject.Set(_factory, "_returnRequestSettings", new ReturnRequestSettings { ReturnReasonsEnabled = true, ReturnActionsEnabled = true, ReturnRequestsAllowFiles = true });
    }

    private void ExchangesOn(List<int> activeReasonIds)
    {
        _featureService.Setup(service => service.IsEnabledAsync(CWFeature.Exchanges, 1)).ReturnsAsync(true);
        _exchangeService.Setup(service => service.GetActiveReasonIdsAsync(1)).ReturnsAsync(activeReasonIds);
    }

    private async Task<CWSubmitReturnRequestModel> PageAsync(SubmitReturnRequestModel? model = null) =>
        (CWSubmitReturnRequestModel)await _factory.PrepareSubmitReturnRequestModelAsync(model ?? new SubmitReturnRequestModel(), Order);

    [Test]
    public async Task The_page_keeps_nops_titles_settings_and_withdrawal_token()
    {
        var page = await PageAsync(new SubmitReturnRequestModel { WithdrawalToken = "token" });

        page.ReturnRequestTitle.Should().Be("Return items of R-10 (/orderdetails/10)");
        page.ReturnRequestPageTitle.Should().Be("PageTitle.ReturnItems");
        page.ReturnRequestSubmitText.Should().Be("ReturnRequests.Submit");
        page.ReturnReasonsEnabled.Should().BeTrue();
        page.ReturnActionsEnabled.Should().BeTrue();
        page.AllowFiles.Should().BeTrue();
        page.WithdrawalToken.Should().Be("token");
    }

    [Test]
    public async Task The_page_opens_with_the_first_reason_and_only_its_actions()
    {
        ExchangesOn(activeReasonIds: [1, 2]);

        var page = await PageAsync();

        page.ReturnRequestReasonId.Should().Be(1);
        page.ReturnActionItems.Select(item => item.Text).Should().Equal("Replace");
    }

    [Test]
    public async Task A_reason_already_chosen_keeps_its_actions()
    {
        ExchangesOn(activeReasonIds: [1, 2]);

        var page = await PageAsync(new SubmitReturnRequestModel { ReturnRequestReasonId = 2 });

        page.ReturnRequestReasonId.Should().Be(2);
        page.ReturnActionItems.Select(item => item.Text).Should().Equal("Repair", "Replace");
    }

    [Test]
    public async Task With_exchanges_on_only_the_reasons_the_stores_rules_offer_are_listed()
    {
        //reason 1's rule is inactive (or missing); reason 2's is active
        _featureService.Setup(service => service.IsEnabledAsync(CWFeature.Exchanges, 1)).ReturnsAsync(true);

        var page = await PageAsync();

        page.ReturnReasonItems.Select(item => item.Text).Should().Equal("Damaged");
        page.ReturnRequestReasonId.Should().Be(2);
    }

    [Test]
    public async Task With_exchanges_off_the_page_keeps_nops_reasons_and_actions()
    {
        var page = await PageAsync();

        page.ReturnReasonItems.Select(item => item.Text).Should().Equal("Wrong size", "Damaged");
        page.ReturnActionItems.Select(item => item.Text).Should().Equal("Repair", "Replace");
        page.ReturnRequestReasonId.Should().Be(0);
        page.NoReturnOptions.Should().BeFalse();
        _exchangeService.Verify(service => service.GetActiveActionsByReasonIdAsync(It.IsAny<int>(), It.IsAny<int>()), Times.Never);
    }

    [Test]
    public async Task With_exchanges_on_and_no_active_reason_no_return_can_be_requested()
    {
        ExchangesOn(activeReasonIds: []);

        var page = await PageAsync();

        page.ReturnReasonItems.Should().BeEmpty();
        page.NoReturnOptions.Should().BeTrue();
    }

    [Test]
    public async Task With_exchanges_on_and_no_active_action_no_return_can_be_requested()
    {
        ExchangesOn(activeReasonIds: [1, 2]);
        _exchangeService.Setup(service => service.GetActiveActionsByReasonIdAsync(It.IsAny<int>(), 1)).ReturnsAsync(new List<ReturnRequestAction>());

        var page = await PageAsync();

        page.ReturnReasonItems.Should().HaveCount(2);
        page.ReturnActionItems.Should().BeEmpty();
        page.NoReturnOptions.Should().BeTrue();
    }
}
