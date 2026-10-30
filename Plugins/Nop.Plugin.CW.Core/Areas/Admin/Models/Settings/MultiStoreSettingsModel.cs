using Microsoft.AspNetCore.Mvc.Rendering;
using Nop.Web.Framework.Models;
using Nop.Web.Framework.Mvc.ModelBinding;

namespace Nop.Plugin.CW.Core.Areas.Admin.Models.Settings;

public record MultiStoreSettingsModel : BaseNopModel, ISettingsModel
{
    #region Ctor

    public MultiStoreSettingsModel()
    {
        AvailableShippingMethods = new List<SelectListItem>();
        AvailableDefaultCustomerRoles = new List<SelectListItem>();
        AvailableProductAttributes = new List<SelectListItem>();
        AvailableBulkOrderViewModes = new List<SelectListItem>();
        BulkOrderAxisAttributeIds = new List<int>();
    }

    #endregion

    #region Properties

    public int ActiveStoreScopeConfiguration { get; set; }

    public int StoreId { get; set; }

    #region PackNames

    [NopResourceDisplayName("Plugins.CW.Core.Admin.MultiStores.Settings.PackNameEnable")]
    public bool PackNameEnable { get; set; }
    public bool PackNameEnable_OverrideForStore { get; set; }

    [NopResourceDisplayName("Plugins.CW.Core.Admin.MultiStores.Settings.UseStaffAsPackName")]
    public bool UseStaffAsPackName { get; set; }
    public bool UseStaffAsPackName_OverrideForStore { get; set; }

    [NopResourceDisplayName("Plugins.CW.Core.Admin.MultiStores.Settings.UseBranchesAsPackNameList")]
    public bool UseBranchesAsPackNameList { get; set; }
    public bool UseBranchesAsPackNameList_OverrideForStore { get; set; }

    [NopResourceDisplayName("Plugins.CW.Core.Admin.MultiStores.Settings.PackNameListNameFrom")]
    public int PackNameListNameFrom { get; set; }
    public bool PackNameListNameFrom_OverrideForStore { get; set; }

    [NopResourceDisplayName("Plugins.CW.Core.Admin.MultiStores.Settings.EmployeeNumberEnabled")]
    public bool EmployeeNumberEnabled { get; set; }
    public bool EmployeeNumberEnabled_OverrideForStore { get; set; }

    [NopResourceDisplayName("Plugins.CW.Core.Admin.MultiStores.Settings.PrintNameEnabled")]
    public bool PrintNameEnabled { get; set; }
    public bool PrintNameEnabled_OverrideForStore { get; set; }

    [NopResourceDisplayName("Plugins.CW.Core.Admin.MultiStores.Settings.PrintNameProductAttributeId")]
    public int PrintNameProductAttributeId { get; set; }
    public bool PrintNameProductAttributeId_OverrideForStore { get; set; }
    public IList<SelectListItem> AvailableProductAttributes { get; set; }

    [NopResourceDisplayName("Plugins.CW.Core.Admin.MultiStores.Settings.POEntryEnabled")]
    public bool POEntryEnabled { get; set; }
    public bool POEntryEnabled_OverrideForStore { get; set; }

    [NopResourceDisplayName("Plugins.CW.Core.Admin.MultiStores.Settings.CommentsEnabled")]
    public bool CommentsEnabled { get; set; }
    public bool CommentsEnabled_OverrideForStore { get; set; }

    [NopResourceDisplayName("Plugins.CW.Core.Admin.MultiStores.Settings.GenderEnabled")]
    public bool GenderEnabled { get; set; }
    public bool GenderEnabled_OverrideForStore { get; set; }

    [NopResourceDisplayName("Plugins.CW.Core.Admin.MultiStores.Settings.ShowDepartmentPackNamesByBranch")]
    public bool ShowDepartmentPackNamesByBranch { get; set; }
    public bool ShowDepartmentPackNamesByBranch_OverrideForStore { get; set; }

    [NopResourceDisplayName("Plugins.CW.Core.Admin.MultiStores.Settings.AllowManagersToAddPackNames")]
    public bool AllowManagersToAddPackNames { get; set; }
    public bool AllowManagersToAddPackNames_OverrideForStore { get; set; }

    [NopResourceDisplayName("Plugins.CW.Core.Admin.MultiStores.Settings.AllowToAddCustomerToPackName")]
    public bool AllowToAddCustomerToPackName { get; set; }
    public bool AllowToAddCustomerToPackName_OverrideForStore { get; set; }

    [NopResourceDisplayName("Plugins.CW.Core.Admin.MultiStores.Settings.AllowSubsidySelection")]
    public bool AllowSubsidySelection { get; set; }
    public bool AllowSubsidySelection_OverrideForStore { get; set; }

    [NopResourceDisplayName("Plugins.CW.Core.Admin.MultiStores.Settings.AllowEditSubsidyTopup")]
    public bool AllowEditSubsidyTopup { get; set; }
    public bool AllowEditSubsidyTopup_OverrideForStore { get; set; }

    [NopResourceDisplayName("Plugins.CW.Core.Admin.MultiStores.Settings.AllowToAddByExcel")]
    public bool AllowToAddByExcel { get; set; }
    public bool AllowToAddByExcel_OverrideForStore { get; set; }

    [NopResourceDisplayName("Plugins.CW.Core.Admin.MultiStores.Settings.AllowSubsidyCountByExcel")]
    public int AllowSubsidyCountByExcel { get; set; }
    public bool AllowSubsidyCountByExcel_OverrideForStore { get; set; }

    [NopResourceDisplayName("Plugins.CW.Core.Admin.MultiStores.Settings.LimitAssignedBranches")]
    public bool LimitAssignedBranches { get; set; }
    public bool LimitAssignedBranches_OverrideForStore { get; set; }

    [NopResourceDisplayName("Plugins.CW.Core.Admin.MultiStores.Settings.PackNamesOwnGlobalValue")]
    public bool PackNamesOwnGlobalValue { get; set; }
    public bool PackNamesOwnGlobalValue_OverrideForStore { get; set; }

    [NopResourceDisplayName("Plugins.CW.Core.Admin.MultiStores.Settings.PackNamesOwnStartAndFinishDates")]
    public bool PackNamesOwnStartAndFinishDates { get; set; }
    public bool PackNamesOwnStartAndFinishDates_OverrideForStore { get; set; }

    private IList<string> _selectedShippingMethods;

    [NopResourceDisplayName("Plugins.CW.Core.Admin.MultiStores.Settings.ShippingMethod")]
    public IList<string> SelectedShippingMethods
    {
        get
        {
            _selectedShippingMethods ??= string.IsNullOrWhiteSpace(ShippingMethod)
                    ? new List<string>()
                    : ShippingMethod.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
            return _selectedShippingMethods;
        }
        set
        {
            _selectedShippingMethods = value ?? new List<string>();
            ShippingMethod = _selectedShippingMethods.Count == 0
                ? string.Empty
                : string.Join(", ", _selectedShippingMethods);
        }
    }
    public string ShippingMethod { get; set; }
    public bool ShippingMethod_OverrideForStore { get; set; }
    public IList<SelectListItem> AvailableShippingMethods { get; set; }

    [NopResourceDisplayName("Plugins.CW.Core.Admin.MultiStores.Settings.AllowHomeDelivery")]
    public bool AllowHomeDelivery { get; set; }
    public bool AllowHomeDelivery_OverrideForStore { get; set; }

    [NopResourceDisplayName("Plugins.CW.Core.Admin.MultiStores.Settings.AllowCustomersToAddAddress")]
    public bool AllowCustomersToAddAddress { get; set; }
    public bool AllowCustomersToAddAddress_OverrideForStore { get; set; }

    [NopResourceDisplayName("Plugins.CW.Core.Admin.MultiStores.Settings.DoNotPreSelectShippingAddress")]
    public bool DoNotPreSelectShippingAddress { get; set; }
    public bool DoNotPreSelectShippingAddress_OverrideForStore { get; set; }

    [NopResourceDisplayName("Plugins.CW.Core.Admin.MultiStores.Settings.IncludeShippingOnSubsidy")]
    public bool IncludeShippingOnSubsidy { get; set; }
    public bool IncludeShippingOnSubsidy_OverrideForStore { get; set; }

    [NopResourceDisplayName("Plugins.CW.Core.Admin.MultiStores.Settings.SubsidySendUserAfterLogin")]
    public bool SubsidySendUserAfterLogin { get; set; }
    public bool SubsidySendUserAfterLogin_OverrideForStore { get; set; }

    #endregion

    #region CreditNotes && Exchanges

    [NopResourceDisplayName("Plugins.CW.Core.Admin.MultiStores.Settings.CreditNotesEnable")]
    public bool CreditNotesEnable { get; set; }
    public bool CreditNotesEnable_OverrideForStore { get; set; }

    [NopResourceDisplayName("Plugins.CW.Core.Admin.MultiStores.Settings.ExchangesEnable")]
    public bool ExchangesEnable { get; set; }
    public bool ExchangesEnable_OverrideForStore { get; set; }

    [NopResourceDisplayName("Plugins.CW.Core.Admin.MultiStores.Settings.AllowCloseWithoutPrintingReturnNote")]
    public bool AllowCloseWithoutPrintingReturnNote { get; set; }
    public bool AllowCloseWithoutPrintingReturnNote_OverrideForStore { get; set; }

    #endregion

    #region Subsidy

    [NopResourceDisplayName("Plugins.CW.Core.Admin.MultiStores.Settings.Subsidy")]
    public bool SubsidyEnable { get; set; }
    public bool SubsidyEnable_OverrideForStore { get; set; }

    #endregion

    #region Bulk Order

    [NopResourceDisplayName("Plugins.CW.Core.Admin.MultiStores.Settings.BulkOrderEnable")]
    public bool BulkOrderEnable { get; set; }
    public bool BulkOrderEnable_OverrideForStore { get; set; }

    [NopResourceDisplayName("Plugins.CW.Core.Admin.MultiStores.Settings.BulkOrderAxisAttributeIds")]
    public IList<int> BulkOrderAxisAttributeIds { get; set; }
    public bool BulkOrderAxisAttributeIds_OverrideForStore { get; set; }

    [NopResourceDisplayName("Plugins.CW.Core.Admin.MultiStores.Settings.BulkOrderMaxCellsPerProduct")]
    public int BulkOrderMaxCellsPerProduct { get; set; }
    public bool BulkOrderMaxCellsPerProduct_OverrideForStore { get; set; }

    [NopResourceDisplayName("Plugins.CW.Core.Admin.MultiStores.Settings.BulkOrderDefaultViewMode")]
    public int BulkOrderDefaultViewMode { get; set; }
    public bool BulkOrderDefaultViewMode_OverrideForStore { get; set; }
    public IList<SelectListItem> AvailableBulkOrderViewModes { get; set; }

    [NopResourceDisplayName("Plugins.CW.Core.Admin.MultiStores.Settings.BulkOrderShowProductImages")]
    public bool BulkOrderShowProductImages { get; set; }
    public bool BulkOrderShowProductImages_OverrideForStore { get; set; }

    [NopResourceDisplayName("Plugins.CW.Core.Admin.MultiStores.Settings.BulkOrderProductImageSize")]
    public int BulkOrderProductImageSize { get; set; }
    public bool BulkOrderProductImageSize_OverrideForStore { get; set; }

    [NopResourceDisplayName("Plugins.CW.Core.Admin.MultiStores.Settings.BulkOrderShowAttributeImages")]
    public bool BulkOrderShowAttributeImages { get; set; }
    public bool BulkOrderShowAttributeImages_OverrideForStore { get; set; }

    [NopResourceDisplayName("Plugins.CW.Core.Admin.MultiStores.Settings.BulkOrderAttributeImageSize")]
    public int BulkOrderAttributeImageSize { get; set; }
    public bool BulkOrderAttributeImageSize_OverrideForStore { get; set; }

    [NopResourceDisplayName("Plugins.CW.Core.Admin.MultiStores.Settings.BulkOrderAttributeImageSquaresSize")]
    public int BulkOrderAttributeImageSquaresSize { get; set; }
    public bool BulkOrderAttributeImageSquaresSize_OverrideForStore { get; set; }

    [NopResourceDisplayName("Plugins.CW.Core.Admin.MultiStores.Settings.BulkOrderHideQtyArrows")]
    public bool BulkOrderHideQtyArrows { get; set; }
    public bool BulkOrderHideQtyArrows_OverrideForStore { get; set; }

    [NopResourceDisplayName("Plugins.CW.Core.Admin.MultiStores.Settings.BulkOrderShowAttributePrices")]
    public bool BulkOrderShowAttributePrices { get; set; }
    public bool BulkOrderShowAttributePrices_OverrideForStore { get; set; }

    [NopResourceDisplayName("Plugins.CW.Core.Admin.MultiStores.Settings.BulkOrderShowProductStock")]
    public bool BulkOrderShowProductStock { get; set; }
    public bool BulkOrderShowProductStock_OverrideForStore { get; set; }

    [NopResourceDisplayName("Plugins.CW.Core.Admin.MultiStores.Settings.BulkOrderDefaultPageSize")]
    public int BulkOrderDefaultPageSize { get; set; }
    public bool BulkOrderDefaultPageSize_OverrideForStore { get; set; }

    [NopResourceDisplayName("Plugins.CW.Core.Admin.MultiStores.Settings.BulkOrderPageSizeOptions")]
    public string BulkOrderPageSizeOptions { get; set; }
    public bool BulkOrderPageSizeOptions_OverrideForStore { get; set; }

    [NopResourceDisplayName("Plugins.CW.Core.Admin.MultiStores.Settings.BulkOrderSendUserAfterLogin")]
    public bool BulkOrderSendUserAfterLogin { get; set; }
    public bool BulkOrderSendUserAfterLogin_OverrideForStore { get; set; }

    [NopResourceDisplayName("Plugins.CW.Core.Admin.MultiStores.Settings.BulkOrderEnableOnProductPage")]
    public bool BulkOrderEnableOnProductPage { get; set; }
    public bool BulkOrderEnableOnProductPage_OverrideForStore { get; set; }

    #endregion

    #region Store

    [NopResourceDisplayName("Plugins.CW.Core.Admin.MultiStores.Settings.DefaultPackNameCustomerRoleIds")]
    public IList<int> DefaultPackNameCustomerRoleIds { get; set; }
    public bool DefaultPackNameCustomerRoleIds_OverrideForStore { get; set; }
    public IList<SelectListItem> AvailableDefaultCustomerRoles { get; set; }

    #endregion

    #endregion
}