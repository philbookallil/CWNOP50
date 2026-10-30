using AwesomeAssertions;
using Moq;
using Nop.Core;
using Nop.Core.Domain.Customers;
using Nop.Core.Domain.Stores;
using Nop.Plugin.CW.Core;
using Nop.Plugin.CW.Core.Domain.PackNames;
using Nop.Plugin.CW.Core.Domain.Settings;
using Nop.Plugin.CW.Core.Services.BulkOrder;
using Nop.Plugin.CW.Core.Services.PackNames;
using Nop.Plugin.CW.Core.Services.Settings;
using Nop.Plugin.CW.Core.Services.Subsidies;
using Nop.Services.Configuration;
using NUnit.Framework;

namespace CW.Tests.Core.Services.PackNames;

/// <summary>
/// Where a customer lands: the subsidy catalogue first, when the store asks for it and the selected packname has
/// product groups; bulk ordering next; the homepage otherwise.
/// </summary>
[TestFixture]
public class PackNameLandingServiceTests
{
    private readonly Customer _customer = new() { Id = 4 };
    private readonly PackName _packName = new() { Id = 20 };

    private Mock<ISubsidyOrderingService> _ordering;
    private Mock<IBulkOrderEntryService> _bulkOrder;
    private Mock<ICWFeatureService> _features;
    private Mock<IPackNameContext> _packNameContext;
    private MultiStoreSettings _settings;

    private PackNameLandingService Build()
    {
        var storeContext = new Mock<IStoreContext>();
        storeContext.Setup(context => context.GetCurrentStoreAsync()).ReturnsAsync(new Store { Id = 1 });

        var settingService = new Mock<ISettingService>();
        settingService.Setup(service => service.LoadSettingAsync<MultiStoreSettings>(1)).ReturnsAsync(_settings);

        return new PackNameLandingService(storeContext.Object, settingService.Object, _features.Object,
            _packNameContext.Object, _ordering.Object, _bulkOrder.Object);
    }

    [SetUp]
    public void SetUp()
    {
        _settings = new MultiStoreSettings { SubsidySendUserAfterLogin = true };

        _features = new Mock<ICWFeatureService>();
        _features.Setup(service => service.IsEnabledAsync(CWFeature.Subsidy, 1)).ReturnsAsync(true);

        _packNameContext = new Mock<IPackNameContext>();
        _packNameContext.Setup(context => context.GetCurrentPackNameAsync(_customer)).ReturnsAsync(_packName);

        _ordering = new Mock<ISubsidyOrderingService>();
        _ordering.Setup(service => service.PackNameHasActiveProductGroupsAsync(_packName.Id)).ReturnsAsync(true);

        _bulkOrder = new Mock<IBulkOrderEntryService>();
        _bulkOrder.Setup(service => service.ResolveLandingRouteAsync(_customer)).ReturnsAsync((string)null);
    }

    [Test]
    public async Task A_packname_with_product_groups_lands_on_the_subsidy_catalogue()
    {
        (await Build().ResolveSignInLandingRouteAsync(_customer)).Should().Be(CWCoreDefaults.SubsidyCatalogRouteName);
        (await Build().ResolveSelectionLandingRouteAsync(_customer)).Should().Be(CWCoreDefaults.SubsidyCatalogRouteName);
    }

    [Test]
    public async Task The_subsidy_catalogue_comes_before_bulk_ordering()
    {
        _bulkOrder.Setup(service => service.ResolveLandingRouteAsync(_customer)).ReturnsAsync(CWCoreDefaults.BULKORDER_INDEX);

        (await Build().ResolveSignInLandingRouteAsync(_customer)).Should().Be(CWCoreDefaults.SubsidyCatalogRouteName);
    }

    [Test]
    public async Task With_the_setting_off_bulk_ordering_decides()
    {
        _settings.SubsidySendUserAfterLogin = false;
        _bulkOrder.Setup(service => service.ResolveLandingRouteAsync(_customer)).ReturnsAsync(CWCoreDefaults.BULKORDER_INDEX);

        (await Build().ResolveSignInLandingRouteAsync(_customer)).Should().Be(CWCoreDefaults.BULKORDER_INDEX);
    }

    [Test]
    public async Task Selecting_a_packname_never_lands_on_bulk_ordering()
    {
        _settings.SubsidySendUserAfterLogin = false;
        _bulkOrder.Setup(service => service.ResolveLandingRouteAsync(_customer)).ReturnsAsync(CWCoreDefaults.BULKORDER_INDEX);

        (await Build().ResolveSelectionLandingRouteAsync(_customer)).Should().BeNull();
    }

    [Test]
    public async Task With_the_subsidy_feature_off_the_setting_has_no_effect()
    {
        _features.Setup(service => service.IsEnabledAsync(CWFeature.Subsidy, 1)).ReturnsAsync(false);

        (await Build().ResolveSignInLandingRouteAsync(_customer)).Should().BeNull();
    }

    [Test]
    public async Task A_packname_without_product_groups_stays_on_the_homepage()
    {
        _ordering.Setup(service => service.PackNameHasActiveProductGroupsAsync(_packName.Id)).ReturnsAsync(false);

        (await Build().ResolveSignInLandingRouteAsync(_customer)).Should().BeNull();
    }

    [Test]
    public async Task A_customer_without_a_packname_stays_on_the_homepage()
    {
        _packNameContext.Setup(context => context.GetCurrentPackNameAsync(_customer)).ReturnsAsync((PackName)null);

        (await Build().ResolveSignInLandingRouteAsync(_customer)).Should().BeNull();
    }
}
