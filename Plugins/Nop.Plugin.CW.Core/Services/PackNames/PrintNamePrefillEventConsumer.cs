using Nop.Core;
using Nop.Core.Domain.Catalog;
using Nop.Plugin.CW.Core.Domain.Settings;
using Nop.Plugin.CW.Core.Services.Settings;
using Nop.Services.Configuration;
using Nop.Services.Events;
using Nop.Web.Framework.Events;
using Nop.Web.Framework.Models;
using Nop.Web.Models.Catalog;

namespace Nop.Plugin.CW.Core.Services.PackNames;

/// <summary>
/// Pre-fills the "name to print" product attribute on the product page with the selected packname's print name.
/// </summary>
/// <remarks>
/// The attribute is the one the store picked in its settings (<c>PrintNameProductAttributeId</c>), and only a text
/// box takes a default. A product page opened to edit a cart line keeps the line's own value. The bulk-order grid
/// empties the attribute list on the pages it takes, so there is nothing to fill there. The customer can still
/// overtype the name.
/// </remarks>
public class PrintNamePrefillEventConsumer : IConsumer<ModelPreparedEvent<BaseNopModel>>
{
    #region Fields

    protected readonly IWorkContext _workContext;
    protected readonly IStoreContext _storeContext;
    protected readonly ISettingService _settingService;
    protected readonly IPackNameContext _packNameContext;
    protected readonly ICWFeatureService _cwFeatureService;

    #endregion

    #region Ctor

    public PrintNamePrefillEventConsumer(IWorkContext workContext,
        IStoreContext storeContext,
        ISettingService settingService,
        IPackNameContext packNameContext,
        ICWFeatureService cwFeatureService)
    {
        _workContext = workContext;
        _storeContext = storeContext;
        _settingService = settingService;
        _packNameContext = packNameContext;
        _cwFeatureService = cwFeatureService;
    }

    #endregion

    #region Methods

    public async Task HandleEventAsync(ModelPreparedEvent<BaseNopModel> eventMessage)
    {
        if (eventMessage.Model is not ProductDetailsModel model)
            return;

        if (model.AddToCart.UpdatedShoppingCartItemId > 0 || model.ProductAttributes.Count == 0)
            return;

        if (!await _cwFeatureService.IsEnabledAsync(CWFeature.PackName))
            return;

        var store = await _storeContext.GetCurrentStoreAsync();
        var settings = await _settingService.LoadSettingAsync<MultiStoreSettings>(store.Id);

        if (!settings.PrintNameEnabled || settings.PrintNameProductAttributeId <= 0)
            return;

        var attribute = model.ProductAttributes.FirstOrDefault(productAttribute =>
            productAttribute.ProductAttributeId == settings.PrintNameProductAttributeId
            && productAttribute.AttributeControlType == AttributeControlType.TextBox);

        if (attribute == null)
            return;

        var packName = await _packNameContext.GetCurrentPackNameAsync(await _workContext.GetCurrentCustomerAsync());

        if (string.IsNullOrWhiteSpace(packName?.PrintName))
            return;

        attribute.DefaultValue = packName.PrintName;
    }

    #endregion
}