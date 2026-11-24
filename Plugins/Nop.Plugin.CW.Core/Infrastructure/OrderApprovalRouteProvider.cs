using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Nop.Plugin.CW.Core.Services.Approvals;
using Nop.Web.Framework.Mvc.Routing;

namespace Nop.Plugin.CW.Core.Infrastructure;

/// <summary>
/// The routes of the approver's review page
/// </summary>
/// <remarks>
/// The token is part of the path, so the link in the e-mail carries nothing a mail client might strip from a query.
/// </remarks>
public class OrderApprovalRouteProvider : IRouteProvider
{
    /// <summary>
    /// Register routes
    /// </summary>
    /// <param name="endpointRouteBuilder">Route builder</param>
    public void RegisterRoutes(IEndpointRouteBuilder endpointRouteBuilder)
    {
        endpointRouteBuilder.MapControllerRoute(
            name: OrderApprovalDefaults.AcceptRouteName,
            pattern: $"{OrderApprovalDefaults.ReviewPath}/{{token}}/accept",
            defaults: new { controller = "OrderApproval", action = "Accept" });

        endpointRouteBuilder.MapControllerRoute(
            name: OrderApprovalDefaults.DeclineRouteName,
            pattern: $"{OrderApprovalDefaults.ReviewPath}/{{token}}/decline",
            defaults: new { controller = "OrderApproval", action = "Decline" });

        endpointRouteBuilder.MapControllerRoute(
            name: OrderApprovalDefaults.ReviewRouteName,
            pattern: $"{OrderApprovalDefaults.ReviewPath}/{{token}}",
            defaults: new { controller = "OrderApproval", action = "Review" });
    }

    /// <summary>
    /// Gets a priority of route provider
    /// </summary>
    public int Priority => 0;
}
