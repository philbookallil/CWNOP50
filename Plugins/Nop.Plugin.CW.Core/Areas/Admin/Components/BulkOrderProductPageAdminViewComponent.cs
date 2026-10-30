using Microsoft.AspNetCore.Mvc;
using Nop.Core;
using Nop.Core.Domain.Catalog;
using Nop.Plugin.CW.Core.Areas.Admin.Models.Catalog;
using Nop.Plugin.CW.Core.Domain.Settings;
using Nop.Plugin.CW.Core.Services.Settings;
using Nop.Services.Common;
using Nop.Services.Configuration;
using Nop.Web.Areas.Admin.Models.Catalog;
using Nop.Web.Framework.Components;

namespace Nop.Plugin.CW.Core.Areas.Admin.Components;

/// <summary>
/// The per-product override for the bulk grid on the product details page, on nop's product edit form
/// (AdminWidgetZones.ProductDetailsBlock, rendered inside its nop-cards in _CreateOrUpdate.cshtml).
/// </summary>
/// <remarks>
/// Reads and renders only. The save rides on the form's own POST and lands in EventConsumer's
/// EntityUpdatedEvent&lt;Product&gt; handler, which is the pattern this plugin already uses for the ACL
/// department field — a widget's inputs are not model-bound by nop's controller, so the value is read back
/// off the raw form.
/// </remarks>
[ViewComponent(Name = "BulkOrderProductPageAdmin")]
public class BulkOrderProductPageAdminViewComponent : NopViewComponent
{
    #region Fields

    protected readonly ICWFeatureService _cwFeatureService;
    protected readonly IGenericAttributeService _genericAttributeService;
    protected readonly ISettingService _settingService;
    protected readonly IStoreContext _storeContext;

    #endregion

    #region Ctor

    public BulkOrderProductPageAdminViewComponent(ICWFeatureService cwFeatureService,
        IGenericAttributeService genericAttributeService,
        ISettingService settingService,
        IStoreContext storeContext)
    {
        _cwFeatureService = cwFeatureService;
        _genericAttributeService = genericAttributeService;
        _settingService = settingService;
        _storeContext = storeContext;
    }

    #endregion

    #region Methods

    public async Task<IViewComponentResult> InvokeAsync(string widgetZone, object additionalData)
    {
        if (additionalData is not ProductModel productModel)
            return Content(string.Empty);

        //a product that has not been saved has no id to hang a GenericAttribute on, and nop's own blocks
        //above this zone hide themselves the same way (_CreateOrUpdate.cshtml, Model.Id > 0)
        if (productModel.Id == 0)
            return Content(string.Empty);

        //nothing to override while bulk ordering is off for every store
        if (!await _cwFeatureService.IsEnabledForAnyStoreAsync(CWFeature.BulkOrder))
            return Content(string.Empty);

        var store = await _storeContext.GetCurrentStoreAsync();
        var settings = await _settingService.LoadSettingAsync<MultiStoreSettings>(store.Id);

        //the store-agnostic row: what this editor writes, and the second step of the runtime read's fallback
        var attributes = await _genericAttributeService.GetAttributesForEntityAsync(productModel.Id, nameof(Product));

        var value = attributes.FirstOrDefault(a =>
                a.Key == CWCoreDefaults.BULKORDER_SHOW_ON_PRODUCT_PAGE_PROPERTY && a.StoreId == 0)?.Value;

        return View(new BulkOrderProductPageModel
        {
            ProductId = productModel.Id,
            ShowOnProductPage = value == "1" || value == "0" ? value : string.Empty,
            StoreDefault = settings.BulkOrderEnableOnProductPage
        });
    }

    #endregion
}