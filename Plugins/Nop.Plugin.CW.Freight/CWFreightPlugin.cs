using Nop.Core;
using Nop.Core.Domain.Shipping;
using Nop.Plugin.CW.Freight.Infrastructure;
using Nop.Plugin.CW.Freight.Services;
using Nop.Plugin.CW.Freight.Settings;
using Nop.Services.Configuration;
using Nop.Services.Helpers;
using Nop.Services.Localization;
using Nop.Services.Plugins;
using Nop.Services.Shipping;
using Nop.Services.Shipping.Tracking;

namespace Nop.Plugin.CW.Freight;

public class CWFreightPlugin : BasePlugin, IShippingRateComputationMethod
{
    #region Fields

    protected readonly IFreightService _freightService;
    protected readonly ILocalizationService _localizationService;
    protected readonly ISettingService _settingService;
    protected readonly IStoreContext _storeContext;
    protected readonly IWebHelper _webHelper;

    #endregion

    #region Ctor

    public CWFreightPlugin(
        IFreightService freightService,
        ILocalizationService localizationService,
        ISettingService settingService,
        IStoreContext storeContext,
        IWebHelper webHelper)
    {
        _freightService = freightService;
        _localizationService = localizationService;
        _settingService = settingService;
        _storeContext = storeContext;
        _webHelper = webHelper;
    }

    #endregion

    #region Methods

    /// <summary>
    /// Gets available shipping options
    /// </summary>
    /// <remarks>
    /// CW.Core's IPackNameShippingMethodService sends one empty item, no address and no customer to list the names of
    /// every method of a store; that request must keep answering every method the store has.
    /// </remarks>
    public async Task<GetShippingOptionResponse> GetShippingOptionsAsync(GetShippingOptionRequest getShippingOptionRequest)
    {
        ArgumentNullException.ThrowIfNull(getShippingOptionRequest);

        var response = new GetShippingOptionResponse();

        if (getShippingOptionRequest.Items == null || !getShippingOptionRequest.Items.Any())
        {
            response.AddError("No shipment items");
            return response;
        }

        var store = await _storeContext.GetCurrentStoreAsync();
        var storeId = getShippingOptionRequest.StoreId != 0 ? getShippingOptionRequest.StoreId : store.Id;
        var countryId = getShippingOptionRequest.ShippingAddress?.CountryId ?? 0;

        var shippingMethods = await _freightService.GetAllCwShippingMethodsAsync(countryId, storeId);
        foreach (var shippingMethod in shippingMethods)
        {
            var rate = await _freightService.DetermineShippingMethodRateAsync(shippingMethod, getShippingOptionRequest);
            response.ShippingOptions.Add(new ShippingOption
            {
                Name = shippingMethod.Name,
                Description = shippingMethod.Description,
                Rate = rate,
                DisplayOrder = shippingMethod.DisplayOrder,
                ShippingRateComputationMethodSystemName = PluginDescriptor.SystemName
            });
        }

        return response;
    }

    /// <summary>
    /// Gets fixed shipping rate (returns null — rates are calculated per request)
    /// </summary>
    public Task<decimal?> GetFixedRateAsync(GetShippingOptionRequest getShippingOptionRequest)
    {
        return Task.FromResult<decimal?>(null);
    }

    /// <summary>
    /// Gets associated shipment tracker
    /// </summary>
    public Task<IShipmentTracker> GetShipmentTrackerAsync()
    {
        return Task.FromResult<IShipmentTracker>(null);
    }

    /// <summary>
    /// Gets a configuration page URL
    /// </summary>
    public override string GetConfigurationPageUrl()
    {
        return $"{_webHelper.GetStoreLocation()}Admin/CWFreight/Configure";
    }

    /// <summary>
    /// Install plugin
    /// </summary>
    public override async Task InstallAsync()
    {
        await _settingService.SaveSettingAsync(new CWFreightSettings
        {
            LimitMethodsToCreated = false
        });

        await _localizationService.AddOrUpdateLocaleResourceAsync(FreightLocaleResources.All);

        await base.InstallAsync();
    }

    /// <summary>
    /// Update plugin; installs the locale resources when the target version contains DATA
    /// </summary>
    /// <param name="currentVersion">Current version of plugin</param>
    /// <param name="targetVersion">New version of plugin</param>
    public override async Task UpdateAsync(string currentVersion, string targetVersion)
    {
        if (targetVersion.Contains("DATA"))
            await _localizationService.AddOrUpdateLocaleResourceAsync(FreightLocaleResources.All);

        await base.UpdateAsync(currentVersion, targetVersion);
    }

    /// <summary>
    /// Uninstall plugin
    /// </summary>
    public override async Task UninstallAsync()
    {
        await _settingService.DeleteSettingAsync<CWFreightSettings>();
        await _localizationService.DeleteLocaleResourcesAsync(FreightLocaleResources.Prefix);

        await base.UninstallAsync();
    }

    #endregion
}
