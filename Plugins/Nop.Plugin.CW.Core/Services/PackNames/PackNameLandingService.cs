using Nop.Core;
using Nop.Core.Domain.Customers;
using Nop.Plugin.CW.Core.Domain.Settings;
using Nop.Plugin.CW.Core.Services.BulkOrder;
using Nop.Plugin.CW.Core.Services.Settings;
using Nop.Plugin.CW.Core.Services.Subsidies;
using Nop.Services.Configuration;

namespace Nop.Plugin.CW.Core.Services.PackNames;

/// <summary>
/// Where a customer lands when the store has somewhere better than the homepage to put them.
/// </summary>
/// <remarks>
/// Two store settings are read in one order: the subsidy catalogue first, for a customer whose selected packname
/// has product groups to spend from (<c>SubsidySendUserAfterLogin</c>), then bulk ordering
/// (<c>BulkOrderSendUserAfterLogin</c>). Selecting a packname consults the subsidy setting only: the catalogue
/// is about the packname just selected, bulk ordering is about the session. One place answers the question so
/// sign-in, packname selection and admin impersonation cannot drift apart.
/// </remarks>
public class PackNameLandingService : IPackNameLandingService
{
    #region Fields

    protected readonly IStoreContext _storeContext;
    protected readonly ISettingService _settingService;
    protected readonly ICWFeatureService _cwFeatureService;
    protected readonly IPackNameContext _packNameContext;
    protected readonly ISubsidyOrderingService _subsidyOrderingService;
    protected readonly IBulkOrderEntryService _bulkOrderEntryService;

    #endregion

    #region Ctor

    public PackNameLandingService(IStoreContext storeContext,
        ISettingService settingService,
        ICWFeatureService cwFeatureService,
        IPackNameContext packNameContext,
        ISubsidyOrderingService subsidyOrderingService,
        IBulkOrderEntryService bulkOrderEntryService)
    {
        _storeContext = storeContext;
        _settingService = settingService;
        _cwFeatureService = cwFeatureService;
        _packNameContext = packNameContext;
        _subsidyOrderingService = subsidyOrderingService;
        _bulkOrderEntryService = bulkOrderEntryService;
    }

    #endregion

    #region Utilities

    /// <summary>
    /// The subsidy catalogue, when the store sends customers there and the selected packname has product groups
    /// to spend from; null otherwise.
    /// </summary>
    protected virtual async Task<string> ResolveSubsidyLandingRouteAsync(Customer customer)
    {
        var store = await _storeContext.GetCurrentStoreAsync();
        var settings = await _settingService.LoadSettingAsync<MultiStoreSettings>(store.Id);

        if (!settings.SubsidySendUserAfterLogin)
            return null;

        if (!await _cwFeatureService.IsEnabledAsync(CWFeature.Subsidy, store.Id))
            return null;

        var packName = await _packNameContext.GetCurrentPackNameAsync(customer);

        if (packName == null || packName.Id == 0)
            return null;

        if (!await _subsidyOrderingService.PackNameHasActiveProductGroupsAsync(packName.Id))
            return null;

        return CWCoreDefaults.SubsidyCatalogRouteName;
    }

    #endregion

    #region Methods

    public virtual async Task<string> ResolveSignInLandingRouteAsync(Customer customer)
    {
        ArgumentNullException.ThrowIfNull(customer);

        return await ResolveSubsidyLandingRouteAsync(customer)
            ?? await _bulkOrderEntryService.ResolveLandingRouteAsync(customer);
    }

    public virtual async Task<string> ResolveSelectionLandingRouteAsync(Customer customer)
    {
        ArgumentNullException.ThrowIfNull(customer);

        return await ResolveSubsidyLandingRouteAsync(customer);
    }

    #endregion
}