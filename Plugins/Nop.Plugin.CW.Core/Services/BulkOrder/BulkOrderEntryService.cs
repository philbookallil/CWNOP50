using Nop.Core;
using Nop.Core.Domain.Customers;
using Nop.Plugin.CW.Core.Domain.Settings;
using Nop.Plugin.CW.Core.Services.Security;
using Nop.Plugin.CW.Core.Services.Settings;
using Nop.Services.Configuration;
using Nop.Services.Security;

namespace Nop.Plugin.CW.Core.Services.BulkOrder;

public class BulkOrderEntryService : IBulkOrderEntryService
{
    #region Fields

    protected readonly ICWFeatureService _cwFeatureService;
    protected readonly IPermissionService _permissionService;
    protected readonly ISettingService _settingService;
    protected readonly IStoreContext _storeContext;

    #endregion

    #region Ctor

    public BulkOrderEntryService(ICWFeatureService cwFeatureService,
        IPermissionService permissionService,
        ISettingService settingService,
        IStoreContext storeContext)
    {
        _cwFeatureService = cwFeatureService;
        _permissionService = permissionService;
        _settingService = settingService;
        _storeContext = storeContext;
    }

    #endregion

    #region Methods

    /// <remarks>
    /// The permission is checked against the customer PASSED IN, not the one on the work context, using
    /// nop's <c>AuthorizeAsync(string, Customer)</c> overload. Both callers need that: impersonation asks
    /// about the customer being impersonated rather than the admin doing it — the admin's own rights got
    /// them this far, and whose session it is now is a different question — and at sign-in the work context
    /// has only just been repointed.
    /// </remarks>
    public virtual async Task<bool> CanUseBulkOrderingAsync(Customer customer)
    {
        if (customer == null)
            return false;

        if (!await _cwFeatureService.IsEnabledAsync(CWFeature.BulkOrder))
            return false;

        //the same permission BulkOrderController carries as an attribute (:47). Sending somebody to a screen
        //that will refuse them is worse than not sending them, and a link to it is worse than no link.
        return await _permissionService.AuthorizeAsync(
            CWPermissionConfigManager.PUBLIC_ACCESS_BULKORDERING, customer);
    }

    /// <remarks>
    /// The setting is read first because it is this behaviour's master switch and the cheapest of the three
    /// reads; a store that has not opted in pays nothing for the feature and permission checks.
    /// Returns a ROUTE NAME rather than an IActionResult: the three call sites build different result types
    /// — a RedirectToRouteResult from a service, RedirectToRoute from two controllers — and the one thing
    /// they must share is which route, not how they get there.
    /// </remarks>
    public virtual async Task<string> ResolveLandingRouteAsync(Customer customer)
    {
        var store = await _storeContext.GetCurrentStoreAsync();
        var settings = await _settingService.LoadSettingAsync<MultiStoreSettings>(store.Id);

        if (!settings.BulkOrderSendUserAfterLogin)
            return null;

        if (!await CanUseBulkOrderingAsync(customer))
            return null;

        return CWCoreDefaults.BULKORDER_INDEX;
    }

    #endregion
}