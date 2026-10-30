using Microsoft.AspNetCore.Mvc;
using Nop.Core;
using Nop.Plugin.Payments.NABGateway.Models;
using Nop.Services;
using Nop.Services.Configuration;
using Nop.Services.Localization;
using Nop.Services.Messages;
using Nop.Services.Security;
using Nop.Web.Framework;
using Nop.Web.Framework.Controllers;
using Nop.Web.Framework.Mvc.Filters;

namespace Nop.Plugin.Payments.NABGateway.Controllers;

/// <summary>
/// Admin configuration controller for the NAB Transact XML payment plugin.
/// </summary>
[AuthorizeAdmin]
[Area(AreaNames.ADMIN)]
[AutoValidateAntiforgeryToken]
public class NABXMLPaymentController : BasePaymentController
{
    #region Fields

    protected readonly ILocalizationService _localizationService;
    protected readonly INotificationService _notificationService;
    protected readonly IPermissionService _permissionService;
    protected readonly ISettingService _settingService;
    protected readonly IStoreContext _storeContext;

    #endregion

    #region Ctor

    public NABXMLPaymentController(ILocalizationService localizationService,
        INotificationService notificationService,
        IPermissionService permissionService,
        ISettingService settingService,
        IStoreContext storeContext)
    {
        _localizationService = localizationService;
        _notificationService = notificationService;
        _permissionService = permissionService;
        _settingService = settingService;
        _storeContext = storeContext;
    }

    #endregion

    #region Methods

    [CheckPermission(StandardPermission.Configuration.MANAGE_PAYMENT_METHODS)]
    public async Task<IActionResult> Configure()
    {
        var storeScope = await _storeContext.GetActiveStoreScopeConfigurationAsync();
        var settings = await _settingService.LoadSettingAsync<NABXMLPaymentSettings>(storeScope);

        var model = new ConfigurationModel
        {
            ActiveStoreScopeConfiguration = storeScope,
            GatewayURL = settings.GatewayURL,
            MerchantId = settings.MerchantId,
            Password = settings.Password,
            DebugMode = settings.DebugMode,
            IgnoreSslErrors = settings.IgnoreSslErrors
        };

        if (storeScope > 0)
        {
            model.GatewayURL_OverrideForStore = await _settingService.SettingExistsAsync(settings, x => x.GatewayURL, storeScope);
            model.MerchantId_OverrideForStore = await _settingService.SettingExistsAsync(settings, x => x.MerchantId, storeScope);
            model.Password_OverrideForStore = await _settingService.SettingExistsAsync(settings, x => x.Password, storeScope);
            model.DebugMode_OverrideForStore = await _settingService.SettingExistsAsync(settings, x => x.DebugMode, storeScope);
            model.IgnoreSslErrors_OverrideForStore = await _settingService.SettingExistsAsync(settings, x => x.IgnoreSslErrors, storeScope);
        }

        return View("~/Plugins/Payments.NABGateway/Views/Configure.cshtml", model);
    }

    [HttpPost]
    [CheckPermission(StandardPermission.Configuration.MANAGE_PAYMENT_METHODS)]
    public async Task<IActionResult> Configure(ConfigurationModel model)
    {
        if (!ModelState.IsValid)
            return await Configure();

        var storeScope = await _storeContext.GetActiveStoreScopeConfigurationAsync();
        var settings = await _settingService.LoadSettingAsync<NABXMLPaymentSettings>(storeScope);

        settings.GatewayURL = model.GatewayURL;
        settings.MerchantId = model.MerchantId;
        settings.Password = model.Password;
        settings.DebugMode = model.DebugMode;
        settings.IgnoreSslErrors = model.IgnoreSslErrors;

        await _settingService.SaveSettingOverridablePerStoreAsync(settings, x => x.GatewayURL, model.GatewayURL_OverrideForStore, storeScope, false);
        await _settingService.SaveSettingOverridablePerStoreAsync(settings, x => x.MerchantId, model.MerchantId_OverrideForStore, storeScope, false);
        await _settingService.SaveSettingOverridablePerStoreAsync(settings, x => x.Password, model.Password_OverrideForStore, storeScope, false);
        await _settingService.SaveSettingOverridablePerStoreAsync(settings, x => x.DebugMode, model.DebugMode_OverrideForStore, storeScope, false);
        await _settingService.SaveSettingOverridablePerStoreAsync(settings, x => x.IgnoreSslErrors, model.IgnoreSslErrors_OverrideForStore, storeScope, false);

        await _settingService.ClearCacheAsync();

        _notificationService.SuccessNotification(await _localizationService.GetResourceAsync("Admin.Plugins.Saved"));

        return await Configure();
    }

    #endregion
}
