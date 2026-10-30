using Microsoft.AspNetCore.Mvc.Rendering;
using Nop.Core.Domain.Customers;
using Nop.Plugin.CW.Core.Areas.Admin.Models.Settings;
using Nop.Plugin.CW.Core.Domain.Settings;
using Nop.Plugin.CW.Core.Services.Shipping;
using Nop.Services.Catalog;
using Nop.Services.Configuration;
using Nop.Services.Customers;
using Nop.Services.Localization;
using Nop.Web.Areas.Admin.Infrastructure.Mapper.Extensions;

namespace Nop.Plugin.CW.Core.Areas.Admin.Factories;

public class CWSettingModelFactory : ICWSettingModelFactory
{
    #region Fields

    protected readonly ISettingService _settingService;
    protected readonly IProductAttributeService _productAttributeService;
    protected readonly ICustomerService _customerService;
    protected readonly ILocalizationService _localizationService;
    protected readonly IPackNameShippingMethodService _packNameShippingMethodService;

    #endregion

    #region Ctor

    public CWSettingModelFactory(ISettingService settingService,
        IProductAttributeService productAttributeService,
        ICustomerService customerService,
        ILocalizationService localizationService,
        IPackNameShippingMethodService packNameShippingMethodService)
    {
        _settingService = settingService;
        _productAttributeService = productAttributeService;
        _customerService = customerService;
        _localizationService = localizationService;
        _packNameShippingMethodService = packNameShippingMethodService;
    }

    #endregion

    #region Utilities

    protected async Task PrepareAvailableProductAttributesAsync(IList<SelectListItem> items, bool addEmpty = false)
    {
        ArgumentNullException.ThrowIfNull(items);

        var productAttributes = await _productAttributeService.GetAllProductAttributesAsync();
        if (productAttributes != null)
            foreach (var productAttribute in productAttributes)
            {
                items.Add(new SelectListItem { Value = productAttribute.Id.ToString(), Text = productAttribute.Name });
            }

        if (addEmpty)
            items.Insert(0, new SelectListItem { Text = "", Value = "0" });
    }

    protected async Task PrepareAvailableCustomerRolesAsync(IList<SelectListItem> items)
    {
        //prepare available customer roles
        var availableRoles = await _customerService.GetAllCustomerRolesAsync(showHidden: true);

        foreach (var role in availableRoles.Where(cr => cr.SystemName != NopCustomerDefaults.RegisteredRoleName && cr.SystemName != NopCustomerDefaults.GuestsRoleName))
        {
            items.Add(new SelectListItem
            {
                Text = role.Name,
                Value = role.Id.ToString(),
            });
        }
    }

    #endregion

    #region Methods

    /// <summary>
    /// Prepare multiStore basic settings model
    /// </summary>
    /// <param name="model">MultiStore model</param>
    /// <param name="storeId">Store identifier</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the multiStore model
    /// </returns>
    public async Task PrepareMultiStoreBasicSettingsModelAsync(MultiStoreSettingsModel model, int storeId)
    {
        var storeSettings = await _settingService.LoadSettingAsync<MultiStoreSettings>(storeId);

        model.EmployeeNumberEnabled = storeSettings.EmployeeNumberEnabled;
        model.POEntryEnabled = storeSettings.POEntryEnabled;
        model.GenderEnabled = storeSettings.GenderEnabled;
        model.PrintNameEnabled = storeSettings.PrintNameEnabled;
        model.CommentsEnabled = storeSettings.CommentsEnabled;
        model.PackNamesOwnGlobalValue = storeSettings.PackNamesOwnGlobalValue;
        model.PackNamesOwnStartAndFinishDates = storeSettings.PackNamesOwnStartAndFinishDates;
    }

    /// <summary>
    /// Prepare store settings model
    /// </summary>
    /// <param name="model">Store settings model</param>
    /// <param name="storeId">Store identifier</param>
    /// <returns>Store settings model</returns>
    public async Task<MultiStoreSettingsModel> PrepareMultiStoreSettingsModelAsync(MultiStoreSettingsModel model, int storeId)
    {
        //fill in model values from the entity
        if (model == null)
        {
            //Check default store setting values
            var allStoreSettings = await _settingService.LoadSettingAsync<MultiStoreSettings>(0);

            if (!await _settingService.SettingExistsAsync(allStoreSettings, settings => settings.AllowSubsidyCountByExcel))
            {
                allStoreSettings.AllowSubsidyCountByExcel = 3;
                await _settingService.SaveSettingAsync(allStoreSettings, x => x.AllowSubsidyCountByExcel, 0);
            }

            //load settings for the store
            var storeSettings = await _settingService.LoadSettingAsync<MultiStoreSettings>(storeId);

            //fill in model values from the entity
            model = storeSettings.ToSettingsModel<MultiStoreSettingsModel>();

            //prepare available product attributes

            await PrepareAvailableProductAttributesAsync(model.AvailableProductAttributes, true);

            model.AvailableBulkOrderViewModes.Add(new SelectListItem { Value = "0", Text = await _localizationService.GetResourceAsync("Catalog.ViewMode.Grid") });
            model.AvailableBulkOrderViewModes.Add(new SelectListItem { Value = "1", Text = await _localizationService.GetResourceAsync("Catalog.ViewMode.List") });

            model.UseStaffAsPackName_OverrideForStore = await _settingService.SettingExistsAsync(storeSettings, x => x.UseStaffAsPackName, storeId);
            model.UseBranchesAsPackNameList_OverrideForStore = await _settingService.SettingExistsAsync(storeSettings, x => x.UseBranchesAsPackNameList, storeId);
            model.PackNameListNameFrom_OverrideForStore = await _settingService.SettingExistsAsync(storeSettings, x => x.PackNameListNameFrom, storeId);
            model.EmployeeNumberEnabled_OverrideForStore = await _settingService.SettingExistsAsync(storeSettings, x => x.EmployeeNumberEnabled, storeId);
            model.PrintNameEnabled_OverrideForStore = await _settingService.SettingExistsAsync(storeSettings, x => x.PrintNameEnabled, storeId);
            model.PrintNameProductAttributeId_OverrideForStore = await _settingService.SettingExistsAsync(storeSettings, x => x.PrintNameProductAttributeId, storeId);
            model.POEntryEnabled_OverrideForStore = await _settingService.SettingExistsAsync(storeSettings, x => x.POEntryEnabled, storeId);
            model.CommentsEnabled_OverrideForStore = await _settingService.SettingExistsAsync(storeSettings, x => x.CommentsEnabled, storeId);
            model.GenderEnabled_OverrideForStore = await _settingService.SettingExistsAsync(storeSettings, x => x.GenderEnabled, storeId);
            model.ShowDepartmentPackNamesByBranch_OverrideForStore = await _settingService.SettingExistsAsync(storeSettings, x => x.ShowDepartmentPackNamesByBranch, storeId);
            model.AllowManagersToAddPackNames_OverrideForStore = await _settingService.SettingExistsAsync(storeSettings, x => x.AllowManagersToAddPackNames, storeId);
            model.AllowToAddCustomerToPackName_OverrideForStore = await _settingService.SettingExistsAsync(storeSettings, x => x.AllowToAddCustomerToPackName, storeId);
            model.AllowSubsidySelection_OverrideForStore = await _settingService.SettingExistsAsync(storeSettings, x => x.AllowSubsidySelection, storeId);
            model.AllowEditSubsidyTopup_OverrideForStore = await _settingService.SettingExistsAsync(storeSettings, x => x.AllowEditSubsidyTopup, storeId);
            model.AllowToAddByExcel_OverrideForStore = await _settingService.SettingExistsAsync(storeSettings, x => x.AllowToAddByExcel, storeId);
            model.AllowSubsidyCountByExcel_OverrideForStore = await _settingService.SettingExistsAsync(storeSettings, x => x.AllowSubsidyCountByExcel, storeId);
            model.LimitAssignedBranches_OverrideForStore = await _settingService.SettingExistsAsync(storeSettings, x => x.LimitAssignedBranches, storeId);
            model.PackNamesOwnGlobalValue_OverrideForStore = await _settingService.SettingExistsAsync(storeSettings, x => x.PackNamesOwnGlobalValue, storeId);
            model.PackNamesOwnStartAndFinishDates_OverrideForStore = await _settingService.SettingExistsAsync(storeSettings, x => x.PackNamesOwnStartAndFinishDates, storeId);
            model.ShippingMethod_OverrideForStore = await _settingService.SettingExistsAsync(storeSettings, x => x.ShippingMethod, storeId);
            model.AllowHomeDelivery_OverrideForStore = await _settingService.SettingExistsAsync(storeSettings, x => x.AllowHomeDelivery, storeId);
            model.AllowCustomersToAddAddress_OverrideForStore = await _settingService.SettingExistsAsync(storeSettings, x => x.AllowCustomersToAddAddress, storeId);
            model.DoNotPreSelectShippingAddress_OverrideForStore = await _settingService.SettingExistsAsync(storeSettings, x => x.DoNotPreSelectShippingAddress, storeId);
            model.IncludeShippingOnSubsidy_OverrideForStore = await _settingService.SettingExistsAsync(storeSettings, x => x.IncludeShippingOnSubsidy, storeId);
            model.SubsidySendUserAfterLogin_OverrideForStore = await _settingService.SettingExistsAsync(storeSettings, x => x.SubsidySendUserAfterLogin, storeId);

            model.PackNameEnable_OverrideForStore = await _settingService.SettingExistsAsync(storeSettings, x => x.PackNameEnable, storeId);
            model.CreditNotesEnable_OverrideForStore = await _settingService.SettingExistsAsync(storeSettings, x => x.CreditNotesEnable, storeId);
            model.ExchangesEnable_OverrideForStore = await _settingService.SettingExistsAsync(storeSettings, x => x.ExchangesEnable, storeId);
            model.AllowCloseWithoutPrintingReturnNote_OverrideForStore = await _settingService.SettingExistsAsync(storeSettings, x => x.AllowCloseWithoutPrintingReturnNote, storeId);
            model.SubsidyEnable_OverrideForStore = await _settingService.SettingExistsAsync(storeSettings, x => x.SubsidyEnable, storeId);

            model.BulkOrderEnable_OverrideForStore = await _settingService.SettingExistsAsync(storeSettings, x => x.BulkOrderEnable, storeId);
            model.BulkOrderAxisAttributeIds_OverrideForStore = await _settingService.SettingExistsAsync(storeSettings, x => x.BulkOrderAxisAttributeIds, storeId);
            model.BulkOrderMaxCellsPerProduct_OverrideForStore = await _settingService.SettingExistsAsync(storeSettings, x => x.BulkOrderMaxCellsPerProduct, storeId);
            model.BulkOrderDefaultViewMode_OverrideForStore = await _settingService.SettingExistsAsync(storeSettings, x => x.BulkOrderDefaultViewMode, storeId);
            model.BulkOrderShowProductImages_OverrideForStore = await _settingService.SettingExistsAsync(storeSettings, x => x.BulkOrderShowProductImages, storeId);
            model.BulkOrderProductImageSize_OverrideForStore = await _settingService.SettingExistsAsync(storeSettings, x => x.BulkOrderProductImageSize, storeId);
            model.BulkOrderShowAttributeImages_OverrideForStore = await _settingService.SettingExistsAsync(storeSettings, x => x.BulkOrderShowAttributeImages, storeId);
            model.BulkOrderAttributeImageSize_OverrideForStore = await _settingService.SettingExistsAsync(storeSettings, x => x.BulkOrderAttributeImageSize, storeId);
            model.BulkOrderAttributeImageSquaresSize_OverrideForStore = await _settingService.SettingExistsAsync(storeSettings, x => x.BulkOrderAttributeImageSquaresSize, storeId);
            model.BulkOrderHideQtyArrows_OverrideForStore = await _settingService.SettingExistsAsync(storeSettings, x => x.BulkOrderHideQtyArrows, storeId);
            model.BulkOrderShowAttributePrices_OverrideForStore = await _settingService.SettingExistsAsync(storeSettings, x => x.BulkOrderShowAttributePrices, storeId);
            model.BulkOrderShowProductStock_OverrideForStore = await _settingService.SettingExistsAsync(storeSettings, x => x.BulkOrderShowProductStock, storeId);
            model.BulkOrderDefaultPageSize_OverrideForStore = await _settingService.SettingExistsAsync(storeSettings, x => x.BulkOrderDefaultPageSize, storeId);
            model.BulkOrderPageSizeOptions_OverrideForStore = await _settingService.SettingExistsAsync(storeSettings, x => x.BulkOrderPageSizeOptions, storeId);
            model.BulkOrderSendUserAfterLogin_OverrideForStore = await _settingService.SettingExistsAsync(storeSettings, x => x.BulkOrderSendUserAfterLogin, storeId);
            model.BulkOrderEnableOnProductPage_OverrideForStore = await _settingService.SettingExistsAsync(storeSettings, x => x.BulkOrderEnableOnProductPage, storeId);

            model.BulkOrderAxisAttributeIds = storeSettings.BulkOrderAxisAttributeIds;

            await PrepareAvailableCustomerRolesAsync(model.AvailableDefaultCustomerRoles);

            model.DefaultPackNameCustomerRoleIds = storeSettings.DefaultPackNameCustomerRoleIds;
            model.DefaultPackNameCustomerRoleIds_OverrideForStore = await _settingService.SettingExistsAsync(storeSettings, x => x.DefaultPackNameCustomerRoleIds, storeId);
        }

        //fill in additional values (not existing in the entity)
        model.ActiveStoreScopeConfiguration = storeId;
        model.StoreId = storeId;

        //the methods the freight plugin offers in the store; the picker is empty without them
        foreach (var shippingMethodName in await _packNameShippingMethodService.GetShippingMethodNamesAsync(storeId))
            model.AvailableShippingMethods.Add(new SelectListItem { Value = shippingMethodName, Text = shippingMethodName });

        return model;
    }

    #endregion
}
