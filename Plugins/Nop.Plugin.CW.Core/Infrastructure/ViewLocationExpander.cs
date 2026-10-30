using Microsoft.AspNetCore.Mvc.Razor;

namespace Nop.Plugin.CW.Core.Infrastructure
{
    /// <summary>
    /// Allows the system to provide a mechanism to register its view paths on the .Net Views Location mechanism 
    /// </summary>
    public class ViewLocationExpander : IViewLocationExpander
    {

        public void PopulateValues(ViewLocationExpanderContext context)
        {
        }

        public IEnumerable<string> ExpandViewLocations(ViewLocationExpanderContext context, IEnumerable<string> viewLocations)
        {
            var viewsLocations = ViewsPathsRegistry.ViewsPaths;
            var partialViewsLocations = ViewsPathsRegistry.PartialViewsPaths;

            // Join all of our registered paths into a single list
            IEnumerable<string> locations = new string[] { };
            if (viewsLocations != null && viewsLocations.Length > 0)
                locations = locations.Concat(viewsLocations);
            if (partialViewsLocations != null && partialViewsLocations.Length > 0)
                locations = locations.Concat(partialViewsLocations);

            if (context.AreaName != null && context.AreaName.Equals("Admin"))
            {
                var plugin = "Nop.Plugin.CW.Core";
                IEnumerable<string> headerLocations = new string[] {
                    $"~/Plugins/{plugin}/Areas/Admin/Views/Home/{{0}}.cshtml"
                };

                locations = locations.Concat(headerLocations);

            }

            // Filter out repeated elements
            locations = locations.Distinct();

            // Finally join our paths with the ones already registered
            return locations.Concat(ExtractMissingElements(locations, viewLocations));
        }

        /// <summary>
        /// Returns the elements from <paramref name="elements"/> that AREN'T contained on <paramref name="source"/>
        /// </summary>
        /// <param name="source"></param>
        /// <param name="elements"></param>
        /// <returns></returns>
        private IEnumerable<string> ExtractMissingElements(IEnumerable<string> source, IEnumerable<string> elements)
        {
            return elements.Where(x => !source.Contains(x));
        }
    }
}
