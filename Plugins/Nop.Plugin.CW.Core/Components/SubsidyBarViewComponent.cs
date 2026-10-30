using Microsoft.AspNetCore.Mvc;
using Nop.Core;
using Nop.Plugin.CW.Core.Domain.Settings;
using Nop.Plugin.CW.Core.Factories;
using Nop.Plugin.CW.Core.Services.PackNames;
using Nop.Plugin.CW.Core.Services.Settings;
using Nop.Plugin.CW.Core.Services.Subsidies;
using Nop.Web.Framework.Components;

namespace Nop.Plugin.CW.Core.Components;

[ViewComponent(Name = "SubsidyBar")]
public class SubsidyBarViewComponent : NopViewComponent
{
    #region Fields

    protected readonly ISubsidyCatalogModelFactory _catalogModelFactory;
    protected readonly IPackNameContext _packNameContext;
    protected readonly IWorkContext _workContext;
    protected readonly ISubsidyService _subsidyService;
    protected readonly ICWFeatureService _cwFeatureService;

    #endregion

    #region Ctor

    public SubsidyBarViewComponent(
        ISubsidyCatalogModelFactory catalogModelFactory,
        IPackNameContext packNameContext,
        IWorkContext workContext,
        ISubsidyService subsidyService,
        ICWFeatureService cwFeatureService)
    {
        _catalogModelFactory = catalogModelFactory;
        _packNameContext = packNameContext;
        _workContext = workContext;
        _subsidyService = subsidyService;
        _cwFeatureService = cwFeatureService;
    }

    #endregion

    #region Methods

    public async Task<IViewComponentResult> InvokeAsync(bool flyout = false)
    {
        if (!await _cwFeatureService.IsEnabledAsync(CWFeature.Subsidy))
            return Content(string.Empty);

        var customer = await _workContext.GetCurrentCustomerAsync();
        var packName = await _packNameContext.GetCurrentPackNameAsync(customer);

        if (packName == null || packName.Id == 0)
            return Content(string.Empty);

        //Validate whether the packname has at least one subsidy allocated
        var subsidies = await _subsidyService.GetAllSubsidyPackNamesByPackNameIdAsync(packName.Id);

        if (!subsidies.Any())
            return Content(string.Empty);

        var model = await _catalogModelFactory.PrepareSubsidyBarModelAsync(packName, flyout);

        return View(model);
    }

    #endregion
}
