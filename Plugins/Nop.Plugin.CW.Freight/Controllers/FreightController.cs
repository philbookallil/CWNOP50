using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Nop.Plugin.CW.Freight.Domain;
using Nop.Plugin.CW.Freight.Models;
using Nop.Plugin.CW.Freight.Services;
using Nop.Plugin.CW.Freight.Settings;
using Nop.Services.Configuration;
using Nop.Services.Customers;
using Nop.Services.Directory;
using Nop.Services.Localization;
using Nop.Services.Messages;
using Nop.Services.Security;
using Nop.Services.Stores;
using Nop.Web.Framework;
using Nop.Web.Framework.Controllers;
using Nop.Web.Framework.Models.Extensions;
using Nop.Web.Framework.Mvc.Filters;

namespace Nop.Plugin.CW.Freight.Controllers;

[AuthorizeAdmin]
[Area(AreaNames.ADMIN)]
[AutoValidateAntiforgeryToken]
public class CWFreightController : BasePluginController
{
    #region Fields

    protected readonly ICountryService _countryService;
    protected readonly ICustomerService _customerService;
    protected readonly IFreightService _freightService;
    protected readonly ILocalizationService _localizationService;
    protected readonly INotificationService _notificationService;
    protected readonly ISettingService _settingService;
    protected readonly IStoreService _storeService;
    protected readonly CWFreightSettings _cwFreightSettings;

    #endregion

    #region Ctor

    public CWFreightController(
        ICountryService countryService,
        ICustomerService customerService,
        IFreightService freightService,
        ILocalizationService localizationService,
        INotificationService notificationService,
        ISettingService settingService,
        IStoreService storeService,
        CWFreightSettings cwFreightSettings)
    {
        _countryService = countryService;
        _customerService = customerService;
        _freightService = freightService;
        _localizationService = localizationService;
        _notificationService = notificationService;
        _settingService = settingService;
        _storeService = storeService;
        _cwFreightSettings = cwFreightSettings;
    }

    #endregion

    #region Utilities

    /// <summary>
    /// Prepares the shipping method model: the select lists always, and the values of the method when there is no
    /// posted model to keep
    /// </summary>
    /// <param name="model">The model; the posted one when a save is refused</param>
    /// <param name="shippingMethod">The method being edited; null on create</param>
    /// <param name="excludeProperties">Whether to keep the model's values instead of the method's</param>
    protected virtual async Task<CWShippingMethodModel> PrepareShippingMethodModelAsync(CWShippingMethodModel model,
        CWShippingMethod shippingMethod, bool excludeProperties = false)
    {
        if (shippingMethod != null && !excludeProperties)
        {
            model.Id = shippingMethod.Id;
            model.Name = shippingMethod.Name;
            model.Description = shippingMethod.Description;
            model.DisplayOrder = shippingMethod.DisplayOrder;
            model.Rate = shippingMethod.Rate;
            model.ApplyPerBranch = shippingMethod.ApplyPerBranch;
            model.LowestOrderTotal = shippingMethod.LowestOrderTotal;
            model.HighestOrderTotal = shippingMethod.HighestOrderTotal;

            model.SelectedStoreIds = (await _freightService.GetStoreMappingsForMethodAsync(shippingMethod.Id))
                .Select(m => m.StoreId).Where(id => id != 0).ToList();
            model.SelectedCustomerRoleIds = (await _freightService.GetCustomerRoleMappingsForMethodAsync(shippingMethod.Id))
                .Select(m => m.CustomerRoleId).ToList();
            model.RestrictedCountryIds = (await _freightService.GetCountryMappingsForMethodAsync(shippingMethod.Id))
                .Select(m => m.CountryId).Where(id => id != 0).ToList();
        }

        //no "All" entry: an empty store list means every store, an empty country list restricts nothing
        model.AvailableStores = (await _storeService.GetAllStoresAsync())
            .Select(store => new SelectListItem { Text = store.Name, Value = store.Id.ToString() })
            .ToList();
        model.AvailableCustomerRoles = (await _customerService.GetAllCustomerRolesAsync(showHidden: true))
            .Select(role => new SelectListItem { Text = role.Name, Value = role.Id.ToString() })
            .ToList();
        model.AvailableCountries = (await _countryService.GetAllCountriesForShippingAsync())
            .Select(country => new SelectListItem { Text = country.Name, Value = country.Id.ToString() })
            .ToList();

        return model;
    }

    protected virtual async Task SaveMethodMappingsAsync(CWShippingMethod shippingMethod, CWShippingMethodModel model)
    {
        await _freightService.DeleteStoreMappingsForMethodAsync(shippingMethod.Id);
        foreach (var storeId in model.SelectedStoreIds.Where(id => id > 0).Distinct())
            await _freightService.InsertStoreMappingAsync(new CWShippingMethodStoreMapping
            {
                ShippingMethodId = shippingMethod.Id,
                StoreId = storeId
            });

        await _freightService.DeleteCustomerRoleMappingsForMethodAsync(shippingMethod.Id);
        foreach (var roleId in model.SelectedCustomerRoleIds.Where(id => id > 0).Distinct())
            await _freightService.InsertCustomerRoleMappingAsync(new CWShippingMethodCustomerRoleMapping
            {
                ShippingMethodId = shippingMethod.Id,
                CustomerRoleId = roleId
            });

        await _freightService.DeleteCountryMappingsForMethodAsync(shippingMethod.Id);
        foreach (var countryId in model.RestrictedCountryIds.Where(id => id > 0).Distinct())
            await _freightService.InsertCountryMappingAsync(new CWShippingMethodCountryMapping
            {
                ShippingMethodId = shippingMethod.Id,
                CountryId = countryId
            });
    }

    #endregion

    #region Configure

    [CheckPermission(StandardPermission.Configuration.MANAGE_SHIPPING_SETTINGS)]
    public async Task<IActionResult> Configure()
    {
        var model = new ConfigurationModel
        {
            LimitMethodsToCreated = _cwFreightSettings.LimitMethodsToCreated,
            SearchStoreId = 0
        };

        model.AvailableStores.Add(new SelectListItem
        {
            Text = await _localizationService.GetResourceAsync("Admin.Common.All"),
            Value = "0",
            Selected = true
        });
        foreach (var store in await _storeService.GetAllStoresAsync())
            model.AvailableStores.Add(new SelectListItem { Text = store.Name, Value = store.Id.ToString() });

        model.SetGridPageSize();

        return View("~/Plugins/CW.Freight/Views/Configure.cshtml", model);
    }

    [HttpPost]
    [CheckPermission(StandardPermission.Configuration.MANAGE_SHIPPING_SETTINGS)]
    public async Task<IActionResult> Configure(ConfigurationModel model)
    {
        _cwFreightSettings.LimitMethodsToCreated = model.LimitMethodsToCreated;
        await _settingService.SaveSettingAsync(_cwFreightSettings);

        _notificationService.SuccessNotification(await _localizationService.GetResourceAsync("Admin.Plugins.Saved"));

        return await Configure();
    }

    #endregion

    #region Methods list

    [HttpPost]
    [CheckPermission(StandardPermission.Configuration.MANAGE_SHIPPING_SETTINGS)]
    public async Task<IActionResult> MethodList(ConfigurationModel searchModel)
    {
        var methods = await _freightService.GetAllCwShippingMethodsAsync(storeId: searchModel.SearchStoreId);
        var pagedMethods = methods.ToPagedList(searchModel);

        var gridModel = await new CWShippingMethodListModel().PrepareToGridAsync(searchModel, pagedMethods, () =>
        {
            return pagedMethods.SelectAwait(async m => new CWShippingMethodModel
            {
                Id = m.Id,
                Name = m.Name,
                Description = m.Description,
                DisplayOrder = m.DisplayOrder,
                Rate = m.Rate,
                ApplyPerBranch = m.ApplyPerBranch,
                LowestOrderTotal = m.LowestOrderTotal,
                HighestOrderTotal = m.HighestOrderTotal
            });
        });

        return Json(gridModel);
    }

    #endregion

    #region Create

    [CheckPermission(StandardPermission.Configuration.MANAGE_SHIPPING_SETTINGS)]
    public async Task<IActionResult> CreateFreightMethod()
    {
        var model = await PrepareShippingMethodModelAsync(new CWShippingMethodModel(), null);
        return View("~/Plugins/CW.Freight/Views/CreateFreightMethod.cshtml", model);
    }

    [HttpPost]
    [CheckPermission(StandardPermission.Configuration.MANAGE_SHIPPING_SETTINGS)]
    public async Task<IActionResult> CreateFreightMethod(CWShippingMethodModel model)
    {
        if (!ModelState.IsValid)
            return View("~/Plugins/CW.Freight/Views/CreateFreightMethod.cshtml",
                await PrepareShippingMethodModelAsync(model, null, true));

        var shippingMethod = new CWShippingMethod
        {
            Name = model.Name,
            Description = model.Description,
            DisplayOrder = model.DisplayOrder,
            Rate = model.Rate,
            ApplyPerBranch = model.ApplyPerBranch,
            LowestOrderTotal = model.LowestOrderTotal,
            HighestOrderTotal = model.HighestOrderTotal
        };

        await _freightService.InsertCwShippingMethodAsync(shippingMethod);
        await SaveMethodMappingsAsync(shippingMethod, model);

        _notificationService.SuccessNotification(
            await _localizationService.GetResourceAsync("Admin.Configuration.Shipping.Methods.Added"));

        return RedirectToAction(nameof(Configure));
    }

    #endregion

    #region Edit

    [CheckPermission(StandardPermission.Configuration.MANAGE_SHIPPING_SETTINGS)]
    public async Task<IActionResult> EditFreightMethod(int id)
    {
        var shippingMethod = await _freightService.GetCwShippingMethodByIdAsync(id);
        if (shippingMethod == null)
            return RedirectToAction(nameof(Configure));

        var model = await PrepareShippingMethodModelAsync(new CWShippingMethodModel(), shippingMethod);
        return View("~/Plugins/CW.Freight/Views/EditFreightMethod.cshtml", model);
    }

    [HttpPost]
    [CheckPermission(StandardPermission.Configuration.MANAGE_SHIPPING_SETTINGS)]
    public async Task<IActionResult> EditFreightMethod(CWShippingMethodModel model)
    {
        var shippingMethod = await _freightService.GetCwShippingMethodByIdAsync(model.Id);
        if (shippingMethod == null)
            return RedirectToAction(nameof(Configure));

        if (!ModelState.IsValid)
            return View("~/Plugins/CW.Freight/Views/EditFreightMethod.cshtml",
                await PrepareShippingMethodModelAsync(model, shippingMethod, true));

        shippingMethod.Name = model.Name;
        shippingMethod.Description = model.Description;
        shippingMethod.DisplayOrder = model.DisplayOrder;
        shippingMethod.Rate = model.Rate;
        shippingMethod.ApplyPerBranch = model.ApplyPerBranch;
        shippingMethod.LowestOrderTotal = model.LowestOrderTotal;
        shippingMethod.HighestOrderTotal = model.HighestOrderTotal;

        await _freightService.UpdateCwShippingMethodAsync(shippingMethod);
        await SaveMethodMappingsAsync(shippingMethod, model);

        _notificationService.SuccessNotification(
            await _localizationService.GetResourceAsync("Admin.Configuration.Shipping.Methods.Updated"));

        return RedirectToAction(nameof(Configure));
    }

    #endregion

    #region Delete

    [HttpPost]
    [CheckPermission(StandardPermission.Configuration.MANAGE_SHIPPING_SETTINGS)]
    public async Task<IActionResult> DeleteMethod(int id)
    {
        var shippingMethod = await _freightService.GetCwShippingMethodByIdAsync(id);
        if (shippingMethod == null)
            return RedirectToAction(nameof(Configure));

        await _freightService.DeleteCwShippingMethodAsync(shippingMethod);

        _notificationService.SuccessNotification(
            await _localizationService.GetResourceAsync("Admin.Configuration.Shipping.Methods.Deleted"));

        return RedirectToAction(nameof(Configure));
    }

    #endregion
}
