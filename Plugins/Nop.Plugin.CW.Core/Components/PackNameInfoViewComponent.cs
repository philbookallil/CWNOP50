using Microsoft.AspNetCore.Mvc;
using Nop.Core;
using Nop.Plugin.CW.Core.Domain.Settings;
using Nop.Plugin.CW.Core.Models.PackNames;
using Nop.Plugin.CW.Core.Services.Branches;
using Nop.Plugin.CW.Core.Services.MultiStores;
using Nop.Plugin.CW.Core.Services.Orders;
using Nop.Plugin.CW.Core.Services.PackNames;
using Nop.Plugin.CW.Core.Services.Settings;
using Nop.Plugin.CW.Core.Services.Subsidies;
using Nop.Services.Configuration;
using Nop.Web.Framework.Components;

namespace Nop.Plugin.CW.Core.Components;

[ViewComponent(Name = "PackNameInfo")]
public class PackNameInfoViewComponent : NopViewComponent
{
    #region Fields

    protected readonly IPackNameContext _packNameContext;
    protected readonly IWorkContext _workContext;
    protected readonly IPackNameService _packNameService;
    protected readonly IBranchService _branchService;
    protected readonly IMultiStoreService _multiStoreService;
    protected readonly ISubsidyOrderingService _subsidyOrderingService;
    protected readonly IShoppingCartOverrideService _shoppingCartService;
    protected readonly ISettingService _settingService;
    protected readonly ICWFeatureService _cwFeatureService;

    #endregion

    #region Ctor

    public PackNameInfoViewComponent(
        IPackNameContext packNameContext,
        IWorkContext workContext,
        IPackNameService packNameService,
        IBranchService branchService,
        IMultiStoreService multiStoreService,
        ISubsidyOrderingService subsidyOrderingService,
        IShoppingCartOverrideService shoppingCartService,
        ISettingService settingService,
        ICWFeatureService cwFeatureService)
    {
        _packNameContext = packNameContext;
        _workContext = workContext;
        _packNameService = packNameService;
        _branchService = branchService;
        _multiStoreService = multiStoreService;
        _subsidyOrderingService = subsidyOrderingService;
        _shoppingCartService = shoppingCartService;
        _settingService = settingService;
        _cwFeatureService = cwFeatureService;
    }

    #endregion

    #region Methods

    public async Task<IViewComponentResult> InvokeAsync(string widgetZone, object additionalData)
    {
        //No feature check here by design: PackNameContext.SetCurrentPackNameAsync returns an empty
        //PackName when CWFeature.PackName is off, so this renders nothing.

        var customer = await _workContext.GetCurrentCustomerAsync();
        var packName = await _packNameContext.GetCurrentPackNameAsync(customer);

        if (packName == null || packName.Id == 0)
            return Content(string.Empty);

        var packNameList = await _packNameService.GetPackNameListByIdAsync(packName.PackNameListId);
        var branch = packNameList?.BranchId.HasValue == true
            ? await _branchService.GetBranchByIdAsync(packNameList.BranchId.Value)
            : null;
        var department = packName.DepartmentId.HasValue
            ? await _multiStoreService.GetDepartmentByIdAsync(packName.DepartmentId.Value)
            : null;
        var costCenter = packName.CostCenterId.HasValue
            ? await _multiStoreService.GetCostCenterByIdAsync(packName.CostCenterId.Value)
            : null;

        var subsidyPackNames = await _subsidyOrderingService.GetInOrderingSubsidyPackNamesByPackNameIdAsync(packName.Id);
        var canOrder = await _subsidyOrderingService.PackNameCanOrderAsync(packName.Id);

        //Asked only while ordering is open: a closed packname already shows why it cannot order.
        var hasShippingAddress = true;

        if (canOrder && packNameList != null)
        {
            var storeSettings = await _settingService.LoadSettingAsync<MultiStoreSettings>(packNameList.StoreId);

            hasShippingAddress = await _shoppingCartService.PackNameHasShippingAddressAsync(packNameList.BranchId ?? 0,
                packName.Id, packName.CustomerId ?? 0, storeSettings);
        }

        var model = new PackNameInfoModel
        {
            Name = packName.Name,
            Colour = packName.Colour,
            IsGroup = packName.Group,
            IsCustomer = packName.IsCustomer,
            IsManager = packName.Manager,
            Branch = branch?.BranchCode,
            Department = department?.Name,
            CostCenter = costCenter?.Name,
            POEntry = packName.POEntry,
            //the subsidy button opens the subsidy flyout, which is closed while Subsidy is off for the store
            HasSubsidies = subsidyPackNames.Any() && await _cwFeatureService.IsEnabledAsync(CWFeature.Subsidy),
            CanOrder = canOrder,
            HasShippingAddress = hasShippingAddress,
        };

        return View(model);
    }

    #endregion
}