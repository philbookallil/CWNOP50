using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Nop.Web.Framework;
using Nop.Web.Framework.Mvc.Routing;

namespace Nop.Plugin.CW.Core;
/// <summary>
/// Represents plugin route provider
/// </summary>
public class RouteProvider : IRouteProvider
{
    /// <summary>
    /// Register routes
    /// </summary>
    /// <param name="endpointRouteBuilder">Route builder</param>
    public void RegisterRoutes(IEndpointRouteBuilder endpointRouteBuilder)
    {
        //Override default route for customers list page in admin area
        endpointRouteBuilder.MapControllerRoute(
            name: CWCoreDefaults.CUSTOMER_LIST,
            pattern: "admin/customer/list",
            defaults: new { controller = "ExtendedCustomer", action = "List", area = AreaNames.ADMIN }
        );

        //Override default route for customers create page in admin area
        endpointRouteBuilder.MapControllerRoute(
            name: CWCoreDefaults.CUSTOMER_CREATE,
            pattern: "admin/customer/create",
            defaults: new { controller = "ExtendedCustomer", action = "Create", area = AreaNames.ADMIN }
        );

        //Override default route for customers edit page in admin area
        endpointRouteBuilder.MapControllerRoute(
            name: CWCoreDefaults.CUSTOMER_EDIT,
            pattern: "admin/customer/edit/{id}",
            defaults: new { controller = "ExtendedCustomer", action = "Edit", area = AreaNames.ADMIN }
        );

        endpointRouteBuilder.MapControllerRoute(
            name: CWCoreDefaults.MULTISTORE_EDIT,
            pattern: "admin/multistore/edit/{id}",
            defaults: new { controller = "MultiStore", action = "Edit", area = AreaNames.ADMIN }
        );

        // Packname checkout page
        endpointRouteBuilder.MapControllerRoute(
            name: "PackNameCheckout",
            pattern: "packname/checkout",
            defaults: new { controller = "PackNameCheckout", action = "Checkout" }
        );

        endpointRouteBuilder.MapControllerRoute(
            name: CWCoreDefaults.CustomerCreditsForMeRouteName,
            pattern: "Credits/CreditsForMe",
            defaults: new { controller = "CreditNotes", action = "CreditsForMe" });


        //Return wizard — gather step (the entry form posts here)
        endpointRouteBuilder.MapControllerRoute(
            name: "CW.Exchanges.Gather",
            pattern: "exchangereturns/gather/{orderId:min(0)}",
            defaults: new { controller = "Exchanges", action = "Gather" }
        );

        //Return wizard — summary confirm step
        endpointRouteBuilder.MapControllerRoute(
            name: "CW.Exchanges.Summary",
            pattern: "exchangereturns/summary",
            defaults: new { controller = "Exchanges", action = "ReturnRequestSummary" }
        );

        endpointRouteBuilder.MapControllerRoute(
            name: CWCoreDefaults.SubsidyCatalogRouteName,
            pattern: "SubsidyCatalog",
            defaults: new { controller = "SubsidyCatalog", action = "Index" });

        endpointRouteBuilder.MapControllerRoute(
            name: "Plugin.CW.Core.SubsidyCatalog.Items",
            pattern: "SubsidyCatalog/Items",
            defaults: new { controller = "SubsidyCatalog", action = "Items" });

        endpointRouteBuilder.MapControllerRoute(
            name: "Plugin.CW.Core.SubsidyCatalog.Bar",
            pattern: "SubsidyCatalog/Bar",
            defaults: new { controller = "SubsidyCatalog", action = "Bar" });

        endpointRouteBuilder.MapControllerRoute(
            name: "Plugin.CW.Core.SubsidyCatalog.ProductGroupHeaders",
            pattern: "SubsidyCatalog/ProductGroupHeaders",
            defaults: new { controller = "SubsidyCatalog", action = "ProductGroupHeaders" });

        // My Account > Subsidies
        endpointRouteBuilder.MapControllerRoute(
            name: CWCoreDefaults.CustomerSubsidiesRouteName,
            pattern: "customer/subsidies",
            defaults: new { controller = "CustomerSubsidies", action = "Index" });

        // Packname order-completed page
        endpointRouteBuilder.MapControllerRoute(
            name: CWCoreDefaults.PackNameCheckoutCompletedRouteName,
            pattern: "packname/checkout/completed/{orderId:int?}",
            defaults: new { controller = "PackNameCheckout", action = "Completed" });

        endpointRouteBuilder.MapControllerRoute(
            name: CWCoreDefaults.BULKORDER_INDEX,
            pattern: "bulkordering",
            defaults: new { controller = "BulkOrder", action = "Index" });

        endpointRouteBuilder.MapControllerRoute(
            name: CWCoreDefaults.BULKORDER_ITEMS,
            pattern: "bulkordering/items",
            defaults: new { controller = "BulkOrder", action = "Items" });

        endpointRouteBuilder.MapControllerRoute(
            name: CWCoreDefaults.BULKORDER_GRID,
            pattern: "bulkordering/grid",
            defaults: new { controller = "BulkOrder", action = "Grid" });

        endpointRouteBuilder.MapControllerRoute(
            name: CWCoreDefaults.BULKORDER_ADDTOCART,
            pattern: "bulkordering/addtocart",
            defaults: new { controller = "BulkOrder", action = "AddToCart" });
    }

    /// <summary>
    /// Gets a priority of route provider
    /// </summary>
    public int Priority => 1;
}