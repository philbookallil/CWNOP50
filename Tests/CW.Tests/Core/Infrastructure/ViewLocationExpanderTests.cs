using AwesomeAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.AspNetCore.Routing;
using Nop.Plugin.CW.Core.Infrastructure;
using NUnit.Framework;

namespace CW.Tests.Core.Infrastructure;

/// <summary>
/// How CW's views replace nop's: the plugin's folders are searched before nop's own, in the admin area and on the
/// storefront, so a CW view at the same relative path as a nop view is the one that renders
/// </summary>
[TestFixture]
public class ViewLocationExpanderTests
{
    private static readonly string[] NopLocations =
    [
        "/Areas/{2}/Views/{1}/{0}.cshtml",
        "/Areas/{2}/Views/Shared/{0}.cshtml",
        "/Views/{1}/{0}.cshtml",
        "/Views/Shared/{0}.cshtml"
    ];

    private static List<string> Expand(string areaName)
    {
        var actionContext = new ActionContext(new DefaultHttpContext(), new RouteData(), new ActionDescriptor());
        var context = new ViewLocationExpanderContext(actionContext, "_CreateOrUpdate.Mappings", "Category", areaName, null, false);

        return new ViewLocationExpander().ExpandViewLocations(context, NopLocations).ToList();
    }

    [Test]
    public void In_the_admin_area_the_plugins_admin_folder_comes_before_nops()
    {
        var locations = Expand("Admin");

        locations.IndexOf("~/Plugins/CW.Core/Areas/Admin/Views/{1}/{0}.cshtml").Should().BeGreaterThanOrEqualTo(0)
            .And.BeLessThan(locations.IndexOf("/Areas/{2}/Views/{1}/{0}.cshtml"));
    }

    [Test]
    public void On_the_storefront_the_plugins_views_folder_comes_before_nops()
    {
        var locations = Expand(null);

        locations.IndexOf("~/Plugins/CW.Core/Views/{1}/{0}.cshtml").Should().BeGreaterThanOrEqualTo(0)
            .And.BeLessThan(locations.IndexOf("/Views/{1}/{0}.cshtml"));
    }

    [Test]
    public void Nops_own_locations_are_all_kept()
    {
        Expand("Admin").Should().Contain(NopLocations);
    }
}
