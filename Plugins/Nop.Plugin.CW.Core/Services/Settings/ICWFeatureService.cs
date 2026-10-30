using Nop.Core.Domain.Orders;
using Nop.Plugin.CW.Core.Domain.Settings;

namespace Nop.Plugin.CW.Core.Services.Settings;

/// <summary>
/// Resolves whether CW features are enabled, per store
/// </summary>
public interface ICWFeatureService
{
    /// <summary>Gets all feature flags for the specified store (null = current store)</summary>
    Task<CWFeatureSet> GetFeaturesAsync(int? storeId = null);

    /// <summary>Gets a value indicating whether the feature is enabled for the specified store (null = current store)</summary>
    Task<bool> IsEnabledAsync(CWFeature feature, int? storeId = null);

    /// <summary>Gets a value indicating whether the feature is enabled for the store the order belongs to</summary>
    Task<bool> IsEnabledForOrderAsync(CWFeature feature, Order order);

    /// <summary>
    /// Gets the features that are enabled for at least one store, as a single set.
    /// Prefer this over several IsEnabledForAnyStoreAsync calls: it is one cached pass over the stores.
    /// </summary>
    Task<CWFeatureSet> GetFeaturesForAnyStoreAsync();

    /// <summary>
    /// Gets a value indicating whether the feature is enabled for at least one store.
    /// For global (non store-scoped) UI such as the admin main menu.
    /// </summary>
    Task<bool> IsEnabledForAnyStoreAsync(CWFeature feature);

    /// <summary>
    /// Discards the per-request cache. Call after saving settings if the same request
    /// goes on to read feature flags.
    /// </summary>
    void ResetRequestCache();
}