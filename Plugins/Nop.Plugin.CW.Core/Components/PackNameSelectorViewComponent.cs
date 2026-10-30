using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Nop.Core;
using Nop.Plugin.CW.Core.Domain.Settings;
using Nop.Plugin.CW.Core.Models.PackNames;
using Nop.Plugin.CW.Core.Services.MultiStores;
using Nop.Plugin.CW.Core.Services.PackNames;
using Nop.Services.Configuration;
using Nop.Services.Helpers;
using Nop.Web.Framework.Components;

namespace Nop.Plugin.CW.Core.Components;

[ViewComponent(Name = "PackNameSelector")]
public partial class PackNameSelectorViewComponent : NopViewComponent
{
    #region Fields

    private readonly IWorkContext _workContext;
    private readonly IPackNameContext _packNameContext;
    private readonly IMultiStoreService _multiStoreService;
    private readonly IPackNameService _packNameService;
    private readonly ISettingService _settingService;
    private readonly IWebHelper _webHelper;
    private readonly IStoreContext _storeContext;

    #endregion

    #region Ctor

    public PackNameSelectorViewComponent(
        IWorkContext workContext,
        IPackNameContext packNameContext,
        IMultiStoreService multiStoreService,
        IPackNameService packNameService,
        ISettingService settingService,
        IWebHelper webHelper,
        IStoreContext storeContext)
    {
        _workContext = workContext;
        _packNameContext = packNameContext;
        _multiStoreService = multiStoreService;
        _packNameService = packNameService;
        _settingService = settingService;
        _webHelper = webHelper;
        _storeContext = storeContext;
    }

    #endregion

    #region Utilities

    public async Task PrepareAvailablePackNameLists(IList<SelectListItem> listItems, int packNameListId)
    {
        var customer = await _workContext.GetCurrentCustomerAsync();

        //Lets try to find if the customer has a packname and is a manager
        var packNameLists = await _multiStoreService.GetAvailablePackNameListsAsync(customer.Id);

        foreach (var packNameList in packNameLists)
        {
            listItems.Add(new SelectListItem
            {
                Text = packNameList.Name,
                Value = $"{packNameList.Id}",
                Selected = packNameList.Id == packNameListId
            });
        }

    }

    #endregion

    #region Methods

    public async Task<IViewComponentResult> InvokeAsync(string widgetZone, object additionalData)
    {
        //No feature check here by design: PackNameContext.SetCurrentPackNameAsync returns an empty
        //PackName when CWFeature.PackName is off, so this renders nothing.

        var customer = await _workContext.GetCurrentCustomerAsync();
        var store = await _storeContext.GetCurrentStoreAsync();

        var currentPackName = await _packNameContext.GetCurrentPackNameAsync(customer);

        if (currentPackName == null)
            return Content(string.Empty); // render nothing if no packName for the customer

        var selectedPackNameListId = await _packNameContext.GetSelectedPackNameListIdAsync(customer); //Could be a department which is a virtual list with negative value
        selectedPackNameListId = selectedPackNameListId != 0 ? selectedPackNameListId : currentPackName.PackNameListId;

        var model = new PackNameSelectorModel
        {
            ReturnUrl = _webHelper.GetRawUrl(HttpContext.Request),
            SelectedPackNameListId = selectedPackNameListId,
            SelectedPackNameId = currentPackName.Id
        };

        await PrepareAvailablePackNameLists(model.AvailablePackNameLists, selectedPackNameListId);

        if (model.AvailablePackNameLists.Count == 0)
            return Content(string.Empty); // render nothing if not a manager or no list available for the packName

        //the add and Excel buttons follow the settings of the manager's own store, as the actions behind them do
        var manager = await _packNameService.GetPackNameByCustomerIdAsync(customer.Id);
        var managerBranch = manager == null ? null : await _packNameService.GetBranchByPackNameIdAsync(manager.Id);
        var storeSettings = await _settingService.LoadSettingAsync<MultiStoreSettings>(managerBranch?.StoreId ?? store.Id);

        model.CanAddPackNames = storeSettings.AllowManagersToAddPackNames;
        model.CanAddByExcel = storeSettings.AllowToAddByExcel;

        //THE STORED LIST MAY NO LONGER BE ONE OF THE OPTIONS - a department's virtual list after the
        //mapping was revoked, or a list whose ownership was removed. The view marks Selected on the
        //matching option and then loads packnames for SelectedPackNameListId, so a stored id that is not
        //offered left the first list showing and the packname dropdown empty and unusable. Fall back to
        //the first list we are actually offering, server-side, so the view loads what it displays.
        if (!model.AvailablePackNameLists.Any(item => item.Selected))
        {
            var firstList = model.AvailablePackNameLists[0];

            firstList.Selected = true;
            model.SelectedPackNameListId = int.TryParse(firstList.Value, out var firstListId) ? firstListId : 0;
        }

        return View(model);
    }

    #endregion
}
