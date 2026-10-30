using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;
using Nop.Plugin.CW.Core.Domain.Settings;

namespace Nop.Plugin.CW.Core.Filters;

/// <summary>
/// Blocks the action when none of the specified CW features are enabled for the resolved store.
/// </summary>
/// <remarks>
/// Features listed in a single attribute are OR-ed: the action runs when at least one is enabled.
/// Stacking several attributes on one action AND-s them, because each filter blocks independently.
/// </remarks>
/// <example>
/// [CheckCWFeature(CWFeature.Subsidy, StoreSource = CWFeatureStoreSource.Parameter)]
/// public async Task&lt;IActionResult&gt; Subsidies(int storeId) { … }
///
/// [CheckCWFeature(CWFeature.Exchanges, CWFeature.CreditNotes,
///                  StoreSource = CWFeatureStoreSource.Order, ParameterName = "orderId")]
/// public async Task&lt;IActionResult&gt; OrderReturns(int orderId) { … }
/// </example>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public sealed class CheckCWFeatureAttribute : Attribute, IFilterFactory
{
    #region Fields

    private readonly CWFeature[] _features;

    #endregion

    #region Ctor

    public CheckCWFeatureAttribute(params CWFeature[] features)
    {
        if (features is null || features.Length == 0)
            throw new ArgumentException("At least one feature must be specified", nameof(features));

        _features = features;
    }

    #endregion

    #region Properties

    /// <summary>
    /// Gets or sets where the store identifier is resolved from. Required — leaving it unset blocks the action/>.
    /// </summary>
    public CWFeatureStoreSource StoreSource { get; set; } = CWFeatureStoreSource.NotSet;

    /// <summary>
    /// Gets or sets the parameter, model property, route or query-string key holding the identifier.
    /// Defaults to "storeId" for <see cref="CWFeatureStoreSource.Parameter"/>
    /// and "orderId" for <see cref="CWFeatureStoreSource.Order"/>.
    /// </summary>
    public string ParameterName { get; set; }

    /// <summary>
    /// Gets or sets a public store route name to redirect to instead of the homepage.
    /// </summary>
    public string RedirectRouteName { get; set; }

    /// <summary>
    /// Gets a value indicating whether the filter instance can be reused across requests
    /// </summary>
    public bool IsReusable => false;

    /// <summary>
    /// Gets or sets a value indicating whether a resolved store identifier of 0 is accepted.
    /// Store id 0 is the all-stores default scope in nop settings, which is meaningful for
    /// pages that edit the defaults (e.g. global fee settings). Only affects
    /// <see cref="CWFeatureStoreSource.Parameter"/>.
    /// Default false: 0 is treated as "not supplied" and the request is blocked.
    /// </summary>
    public bool AllowDefaultStore { get; set; }
    
    #endregion

    #region Methods

    public IFilterMetadata CreateInstance(IServiceProvider serviceProvider)
    {
        return ActivatorUtilities.CreateInstance<CheckCWFeatureFilter>(
            serviceProvider, _features, StoreSource, ParameterName ?? string.Empty, RedirectRouteName ?? string.Empty, AllowDefaultStore);
    }

    #endregion
}