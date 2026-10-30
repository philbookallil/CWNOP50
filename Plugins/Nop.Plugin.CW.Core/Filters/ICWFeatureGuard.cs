using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Nop.Plugin.CW.Core.Domain.Settings;

namespace Nop.Plugin.CW.Core.Filters;

/// <summary>
/// Checks a CW feature inline, for controller actions whose store cannot be resolved by
/// <see cref="CheckCWFeatureAttribute"/> because it needs a database lookup first
/// </summary>
public interface ICWFeatureGuard
{
    /// <summary>Returns a block result when the feature is disabled for the store, otherwise null</summary>
    Task<IActionResult> BlockIfDisabledAsync(CWFeature feature, int storeId, HttpRequest request);
}
