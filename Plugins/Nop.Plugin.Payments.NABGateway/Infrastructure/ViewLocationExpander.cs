using Microsoft.AspNetCore.Mvc.Razor;

namespace Nop.Plugin.Payments.NABGateway.Infrastructure;

/// <summary>
/// Adds the plugin's view folders to the MVC view discovery pipeline so Razor can resolve
/// plugin views without callers having to spell out the full <c>~/Plugins/...</c> path.
/// </summary>
public class ViewLocationExpander : IViewLocationExpander
{
    public void PopulateValues(ViewLocationExpanderContext context)
    {
    }

    public IEnumerable<string> ExpandViewLocations(
        ViewLocationExpanderContext context, IEnumerable<string> viewLocations)
    {
        var pluginLocations = new[]
        {
            $"~/Plugins/{NABXMLPaymentDefaults.PluginFolder}/Views/{{0}}.cshtml",
            $"~/Plugins/{NABXMLPaymentDefaults.PluginFolder}/Views/{{1}}/{{0}}.cshtml"
        };

        return pluginLocations.Concat(viewLocations);
    }
}
