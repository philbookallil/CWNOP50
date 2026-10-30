using System.Reflection;
using System.Runtime.CompilerServices;
using AwesomeAssertions;
using Moq;
using Nop.Core;
using Nop.Core.Domain.Customers;
using Nop.Plugin.CW.Core;
using Nop.Plugin.CW.Core.Domain.PackNames;
using Nop.Plugin.CW.Core.Domain.Settings;
using Nop.Plugin.CW.Core.Domain.Subsidies.DTOs;
using Nop.Plugin.CW.Core.Services;
using Nop.Plugin.CW.Core.Services.CreditNotes;
using Nop.Plugin.CW.Core.Services.PackNames;
using Nop.Plugin.CW.Core.Services.Settings;
using Nop.Plugin.CW.Core.Services.Subsidies;
using Nop.Services.Localization;
using Nop.Web.Models.Customer;
using NUnit.Framework;

namespace CW.Tests.Core.Services.Events;

/// <summary>
/// My Account's *My Subsidies* item shows when the page has a subsidy to list: an active subsidy, actively assigned to the
/// selected packname
/// </summary>
[TestFixture]
public class SubsidiesMenuItemTests
{
    private Mock<ISubsidyReportingService> _reportingService = null!;
    private EventConsumer _consumer = null!;

    [SetUp]
    public void SetUp()
    {
        var workContext = new Mock<IWorkContext>();
        workContext.Setup(context => context.GetCurrentCustomerAsync()).ReturnsAsync(new Customer { Id = 5 });
        var featureService = new Mock<ICWFeatureService>();
        featureService.Setup(service => service.GetFeaturesAsync(null)).ReturnsAsync(new CWFeatureSet(true, true, false, false, false));
        var packNameContext = new Mock<IPackNameContext>();
        packNameContext.Setup(context => context.GetCurrentPackNameAsync(It.IsAny<Customer>())).ReturnsAsync(new PackName { Id = 9 });
        _reportingService = new Mock<ISubsidyReportingService>();
        var localizationService = new Mock<ILocalizationService>();
        localizationService.Setup(service => service.GetResourceAsync(It.IsAny<string>())).ReturnsAsync((string key) => key);

        _consumer = (EventConsumer)RuntimeHelpers.GetUninitializedObject(typeof(EventConsumer));
        SetField("_workContext", workContext.Object);
        SetField("_cwFeatureService", featureService.Object);
        SetField("_creditNotesService", new Mock<ICreditNotesService>().Object);
        SetField("_packNameContext", packNameContext.Object);
        SetField("_subsidyReportingService", _reportingService.Object);
        SetField("_localizationService", localizationService.Object);
    }

    private void SetField(string name, object value) =>
        typeof(EventConsumer).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(_consumer, value);

    private async Task<IList<int>> MenuTabsAsync(params SubsidyPackNameDTO[] assignments)
    {
        _reportingService.Setup(service => service.GetSubsidyDetailsByPackNameIdAsync(9, false)).ReturnsAsync(assignments.ToList());
        var navigation = new CustomerNavigationModel();
        var method = typeof(EventConsumer).GetMethod("AddCustomerNavigationItemsAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;
        await (Task)method.Invoke(_consumer, [navigation])!;

        return navigation.CustomerNavigationItems.Select(item => item.Tab).ToList();
    }

    [Test]
    public async Task An_active_subsidy_actively_assigned_shows_the_item()
    {
        (await MenuTabsAsync(new SubsidyPackNameDTO { Active = true, AssignmentActive = true }))
            .Should().Contain(CWCoreDefaults.CustomerSubsidiesMenuTab);
    }

    [Test]
    public async Task Only_inactive_subsidies_or_assignments_show_no_item()
    {
        (await MenuTabsAsync(new SubsidyPackNameDTO { Active = false, AssignmentActive = true }, new SubsidyPackNameDTO { Active = true, AssignmentActive = false }))
            .Should().NotContain(CWCoreDefaults.CustomerSubsidiesMenuTab);
    }
}
