using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Nop.Plugin.CW.Core.Filters;

/// <summary>
/// Builds the action result returned when a CW feature is disabled, so that the attribute and the
/// inline guard produce identical responses
/// </summary>
public interface ICWFeatureBlockResultBuilder
{
    /// <summary>
    /// Creates the result appropriate to the current request
    /// </summary>
    /// <param name="request">HTTP request</param>
    /// <param name="redirectRouteName">Public route to redirect to instead of the homepage</param>
    Task<IActionResult> CreateAsync(HttpRequest request, string redirectRouteName = null);
}
