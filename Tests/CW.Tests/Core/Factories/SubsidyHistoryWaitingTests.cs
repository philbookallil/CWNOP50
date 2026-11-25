using AwesomeAssertions;
using Moq;
using Nop.Core;
using Nop.Core.Domain.Directory;
using Nop.Core.Domain.Localization;
using Nop.Plugin.CW.Core.Areas.Admin.Factories;
using Nop.Plugin.CW.Core.Areas.Admin.Models.Subsidies;
using Nop.Plugin.CW.Core.Domain.Subsidies;
using Nop.Plugin.CW.Core.Domain.Subsidies.Views;
using Nop.Plugin.CW.Core.Services.Approvals;
using Nop.Plugin.CW.Core.Services.Subsidies;
using Nop.Services.Catalog;
using Nop.Services.Helpers;
using Nop.Services.Localization;
using NUnit.Framework;

namespace CW.Tests.Core.Factories;

/// <summary>
/// The admin history of a packname's subsidy says which uses wait for approval
/// </summary>
[TestFixture]
public class SubsidyHistoryWaitingTests
{
    [Test]
    public async Task A_use_by_an_order_waiting_for_approval_says_so_in_its_reason()
    {
        var history = new List<SubsidyHistory>
        {
            new() { HistoryType = HistoryType.SubsidyUsed, OrderId = 5034 },
            new() { HistoryType = HistoryType.SubsidyUsed, OrderId = 5031 },
            new() { HistoryType = HistoryType.SubsidyTopUp, Reason = "Birthday" }
        };

        var subsidyService = new Mock<ISubsidyService>();
        subsidyService.Setup(mock => mock.GetSubsidyPackNameByIdAsync(9)).ReturnsAsync(new SubsidyPackName { Id = 9, PackNameId = 1034, SubsidyId = 1 });
        var reportingService = new Mock<ISubsidyReportingService>();
        reportingService.Setup(mock => mock.GetSubsidyPackNameHistoryAsync(1034, 1, 0, 10)).ReturnsAsync(new PagedList<SubsidyHistory>(history, 0, 10, 3));
        var approvalService = new Mock<IOrderApprovalService>();
        approvalService.Setup(mock => mock.GetHeldOrderIdsAsync(It.Is<int[]>(ids => ids.SequenceEqual(new[] { 5034, 5031 })))).ReturnsAsync(new List<int> { 5034 });
        var localizationService = new Mock<ILocalizationService>();
        localizationService.Setup(mock => mock.GetResourceAsync(It.IsAny<string>())).ReturnsAsync((string key) => key.EndsWith("Waiting") ? "{0} (waiting)" : key.Split('.')[^1]);
        var workContext = new Mock<IWorkContext>();
        workContext.Setup(mock => mock.GetWorkingCurrencyAsync()).ReturnsAsync(new Currency());
        workContext.Setup(mock => mock.GetWorkingLanguageAsync()).ReturnsAsync(new Language { Id = 1 });

        var factory = TestObject.Create<SubsidyModelFactory>();
        TestObject.Set(factory, "_subsidyService", subsidyService.Object);
        TestObject.Set(factory, "_subsidyReportingService", reportingService.Object);
        TestObject.Set(factory, "_orderApprovalService", approvalService.Object);
        TestObject.Set(factory, "_localizationService", localizationService.Object);
        TestObject.Set(factory, "_workContext", workContext.Object);
        TestObject.Set(factory, "_dateTimeHelper", Mock.Of<IDateTimeHelper>());
        TestObject.Set(factory, "_priceFormatter", Mock.Of<IPriceFormatter>());

        var model = await factory.PreparePackNameSubsidyHistoryListModelAsync(new PackNameSubsidyProductGroupSearchModel { SubsidyPackNameId = 9, Start = 0, Length = 10 });

        model.Data.Select(row => row.Reason).Should().Equal("SubsidyUsed (waiting)", "SubsidyUsed", "Birthday");
    }
}
