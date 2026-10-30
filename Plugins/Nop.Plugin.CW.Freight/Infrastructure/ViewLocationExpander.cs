using Microsoft.AspNetCore.Mvc.Razor;

namespace Nop.Plugin.CW.Freight.Infrastructure;

public class ViewLocationExpander : IViewLocationExpander
{
    private const string PLUGIN_FOLDER = "CW.Freight";

    public void PopulateValues(ViewLocationExpanderContext context)
    {
    }

    public IEnumerable<string> ExpandViewLocations(ViewLocationExpanderContext context, IEnumerable<string> viewLocations)
    {
        var pluginLocations = new[]
        {
            $"~/Plugins/{PLUGIN_FOLDER}/Views/{{0}}.cshtml",
            $"~/Plugins/{PLUGIN_FOLDER}/Views/{{1}}/{{0}}.cshtml",
        };

        return pluginLocations.Concat(viewLocations);
    }
}
