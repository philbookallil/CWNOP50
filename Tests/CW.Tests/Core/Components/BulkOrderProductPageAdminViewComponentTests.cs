using AwesomeAssertions;
using Microsoft.AspNetCore.Mvc.ViewComponents;
using Moq;
using Nop.Core;
using Nop.Core.Domain.Catalog;
using Nop.Core.Domain.Common;
using Nop.Core.Domain.Stores;
using Nop.Plugin.CW.Core;
using Nop.Plugin.CW.Core.Areas.Admin.Components;
using Nop.Plugin.CW.Core.Areas.Admin.Models.Catalog;
using Nop.Plugin.CW.Core.Domain.Settings;
using Nop.Plugin.CW.Core.Services.Settings;
using Nop.Services.Common;
using Nop.Services.Configuration;
using Nop.Web.Areas.Admin.Models.Catalog;
using NUnit.Framework;

namespace CW.Tests.Core.Components;

/// <summary>
/// The product page's bulk-grid setting on nop's product edit form: what the three radios start from
/// </summary>
[TestFixture]
public class BulkOrderProductPageAdminViewComponentTests
{
    private Mock<ICWFeatureService> _featureService = null!;
    private Mock<IGenericAttributeService> _genericAttributeService = null!;
    private BulkOrderProductPageAdminViewComponent _component = null!;

    [SetUp]
    public void SetUp()
    {
        _featureService = new Mock<ICWFeatureService>();
        _featureService.Setup(service => service.IsEnabledForAnyStoreAsync(CWFeature.BulkOrder)).ReturnsAsync(true);

        _genericAttributeService = new Mock<IGenericAttributeService>();
        _genericAttributeService.Setup(service => service.GetAttributesForEntityAsync(It.IsAny<int>(), nameof(Product)))
            .ReturnsAsync(new List<GenericAttribute>());

        var settingService = new Mock<ISettingService>();
        settingService.Setup(service => service.LoadSettingAsync<MultiStoreSettings>(2))
            .ReturnsAsync(new MultiStoreSettings { BulkOrderEnableOnProductPage = true });

        var storeContext = new Mock<IStoreContext>();
        storeContext.Setup(context => context.GetCurrentStoreAsync()).ReturnsAsync(new Store { Id = 2 });

        _component = new BulkOrderProductPageAdminViewComponent(_featureService.Object, _genericAttributeService.Object,
            settingService.Object, storeContext.Object);
    }

    private void Stored(int productId, int storeId, string value) =>
        _genericAttributeService.Setup(service => service.GetAttributesForEntityAsync(productId, nameof(Product)))
            .ReturnsAsync(new List<GenericAttribute>
            {
                new() { EntityId = productId, KeyGroup = nameof(Product), Key = CWCoreDefaults.BULKORDER_SHOW_ON_PRODUCT_PAGE_PROPERTY, StoreId = storeId, Value = value }
            });

    private static BulkOrderProductPageModel Model(object result) =>
        (BulkOrderProductPageModel)result.Should().BeOfType<ViewViewComponentResult>().Subject.ViewData!.Model!;

    private static void ShouldBeEmpty(object result) =>
        result.Should().BeOfType<ContentViewComponentResult>().Which.Content.Should().BeEmpty();

    [Test]
    public async Task A_product_not_saved_yet_gets_no_setting()
    {
        ShouldBeEmpty(await _component.InvokeAsync("admin_product_details_block", new ProductModel { Id = 0 }));
        _genericAttributeService.VerifyNoOtherCalls();
    }

    [Test]
    public async Task Another_page_model_gets_no_setting()
    {
        ShouldBeEmpty(await _component.InvokeAsync("admin_product_details_block", new CategoryModel { Id = 5 }));
    }

    [Test]
    public async Task With_bulk_ordering_off_in_every_store_there_is_no_setting()
    {
        _featureService.Setup(service => service.IsEnabledForAnyStoreAsync(CWFeature.BulkOrder)).ReturnsAsync(false);

        ShouldBeEmpty(await _component.InvokeAsync("admin_product_details_block", new ProductModel { Id = 5 }));
    }

    [TestCase("1", "1")]
    [TestCase("0", "0")]
    [TestCase("", "")]
    [TestCase("yes", "")]
    public async Task The_saved_setting_is_selected(string saved, string selected)
    {
        Stored(5, 0, saved);

        var model = Model(await _component.InvokeAsync("admin_product_details_block", new ProductModel { Id = 5 }));

        model.ProductId.Should().Be(5);
        model.ShowOnProductPage.Should().Be(selected);
        model.StoreDefault.Should().BeTrue("the current store shows the grid on product pages");
    }

    [Test]
    public async Task A_store_specific_value_is_not_the_one_the_editor_shows()
    {
        //the editor writes and reads the store-agnostic row; a per-store row is data the runtime reads first
        Stored(5, 2, "0");

        Model(await _component.InvokeAsync("admin_product_details_block", new ProductModel { Id = 5 })).ShowOnProductPage.Should().BeEmpty();
    }
}
