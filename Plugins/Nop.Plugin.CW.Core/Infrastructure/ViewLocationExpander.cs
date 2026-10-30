using Microsoft.AspNetCore.Mvc.Razor;

namespace Nop.Plugin.CW.Core.Infrastructure;

/// <summary>
/// Puts CW.Core's view locations (<see cref="ViewsPathsRegistry"/>) ahead of every other location, the theme's and nop's
/// included, on the storefront and in the admin area alike
/// </summary>
/// <remarks>
/// nop asks for its views by name, so a CW view with the same name at one of these locations is the one that renders,
/// and a theme cannot replace it: the theme's locations come after CW's.
/// </remarks>
public class ViewLocationExpander : IViewLocationExpander
{
    /// <summary>
    /// The locations do not depend on the request, so nothing is added to the lookup's cache key
    /// </summary>
    /// <param name="context">View location expander context</param>
    public void PopulateValues(ViewLocationExpanderContext context)
    {
    }

    /// <summary>
    /// CW.Core's locations first, then every location already registered that is not one of them
    /// </summary>
    /// <param name="context">View location expander context</param>
    /// <param name="viewLocations">The locations registered before this expander runs</param>
    /// <returns>The locations in the order they are searched</returns>
    public IEnumerable<string> ExpandViewLocations(ViewLocationExpanderContext context, IEnumerable<string> viewLocations)
    {
        return ViewsPathsRegistry.ViewsPaths.Concat(viewLocations.Where(location => !ViewsPathsRegistry.ViewsPaths.Contains(location)));
    }
}