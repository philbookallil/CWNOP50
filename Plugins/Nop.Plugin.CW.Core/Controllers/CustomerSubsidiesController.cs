using Microsoft.AspNetCore.Mvc;
using Nop.Core;
using Nop.Plugin.CW.Core.Domain.Settings;
using Nop.Plugin.CW.Core.Factories;
using Nop.Plugin.CW.Core.Filters;
using Nop.Plugin.CW.Core.Services.PackNames;
using Nop.Services.Customers;
using Nop.Web.Controllers;

namespace Nop.Plugin.CW.Core.Controllers;

/// <summary>
/// The customer's subsidy page under My Account.
/// </summary>
/// <remarks>
/// The page follows the packname being ordered for, the same packname the subsidy bar and the catalogue
/// follow: an employee sees their own, a manager sees the employee they have selected. Without a packname
/// there is nothing to show and the menu item that leads here is not rendered either.
/// </remarks>
[CheckCWFeature(CWFeature.Subsidy, StoreSource = CWFeatureStoreSource.CurrentStore)]
public class CustomerSubsidiesController : BasePublicController
{
    #region Fields

    protected readonly IWorkContext _workContext;
    protected readonly ICustomerService _customerService;
    protected readonly IPackNameContext _packNameContext;
    protected readonly ICustomerSubsidiesModelFactory _customerSubsidiesModelFactory;

    #endregion

    #region Ctor

    public CustomerSubsidiesController(IWorkContext workContext,
        ICustomerService customerService,
        IPackNameContext packNameContext,
        ICustomerSubsidiesModelFactory customerSubsidiesModelFactory)
    {
        _workContext = workContext;
        _customerService = customerService;
        _packNameContext = packNameContext;
        _customerSubsidiesModelFactory = customerSubsidiesModelFactory;
    }

    #endregion

    #region Methods

    public virtual async Task<IActionResult> Index()
    {
        var customer = await _workContext.GetCurrentCustomerAsync();

        if (!await _customerService.IsRegisteredAsync(customer))
            return Challenge();

        var packName = await _packNameContext.GetCurrentPackNameAsync(customer);

        if (packName == null || packName.Id == 0)
            return AccessDeniedView();

        var model = await _customerSubsidiesModelFactory.PrepareCustomerSubsidiesModelAsync(packName, customer);

        return View("~/Plugins/CW.Core/Views/CustomerSubsidies/Index.cshtml", model);
    }

    #endregion
}