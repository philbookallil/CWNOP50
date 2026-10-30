using Nop.Services.Plugins;

namespace Nop.Plugin.CW.Core.Infrastructure
{
    /// <summary>
    /// Utility class that serves as a "registry" for the Views Locations that are registered through the plugins
    /// </summary>
    public static class ViewsPathsRegistry
    {
        private static readonly IList<string> viewsPaths;
        private static readonly IList<string> partialViewsPaths;

        /// <summary>
        /// Constructor with default initialization of the views and prtial views Locations paths
        /// </summary>
        static ViewsPathsRegistry()
        {
            // Setup the default Views Paths (will mostly apply to NOP Core overrides)

            var plugin = "CW.Core";

            viewsPaths = new List<string>
            {
                $"~/Plugins/{plugin}/Views/{{0}}.cshtml",
                $"~/Plugins/{plugin}/Views/{{1}}/{{0}}.cshtml",
                $"~/Plugins/{plugin}/Views/Shared/{{0}}.cshtml",
                $"~/Plugins/{plugin}/Views/Shared/{{1}}/{{0}}.cshtml",
                $"~/Plugins/{plugin}/Views/Shared/Components/{{1}}/{{0}}.cshtml",
                $"~/Plugins/{plugin}/Areas/Admin/Views/{{1}}/{{0}}.cshtml",
                $"~/Plugins/{plugin}/Areas/Admin/Views/{{2}}/{{1}}/{{0}}.cshtml",
                $"~/Plugins/{plugin}/Areas/Admin/Views/Shared/{{0}}.cshtml",
                $"~/Plugins/{plugin}/Areas/Admin/Views/Shared/{{1}}/{{0}}.cshtml",
                $"~/Plugins/{plugin}/Areas/Admin/Views/Shared/Components/{{1}}/{{0}}.cshtml",
            };

            partialViewsPaths = new List<string>()
            {
                $"~/Plugins/{plugin}/Views/{{0}}.cshtml",
                $"~/Plugins/{plugin}/Views/{{1}}/{{0}}.cshtml",
                $"~/Plugins/{plugin}/Views/Shared/{{0}}.cshtml",
                $"~/Plugins/{plugin}/Views/Shared/{{1}}/{{0}}.cshtml",
                $"~/Plugins/{plugin}/Views/Shared/Components/{{1}}/{{0}}.cshtml",
                $"~/Plugins/{plugin}/Areas/Admin/Views/{{1}}/{{0}}.cshtml",
                $"~/Plugins/{plugin}/Areas/Admin/Views/{{2}}/{{1}}/{{0}}.cshtml",
                $"~/Plugins/{plugin}/Areas/Admin/Views/Shared/{{0}}.cshtml",
                $"~/Plugins/{plugin}/Areas/Admin/Views/Shared/{{1}}/{{0}}.cshtml",
                $"~/Plugins/{plugin}/Areas/Admin/Views/Shared/Components/{{1}}/{{0}}.cshtml",
            };
        }

        /// <summary>
        /// Adds the given Views Location path to the registry
        /// </summary>
        /// <param name="path"></param>
        public static void RegisterViewsPath(string path)
        {
            if (!viewsPaths.Contains(path))
            {
                viewsPaths.Add(path);
            }
        }

        /// <summary>
        /// Removes the given Views Location path from the registry
        /// </summary>
        /// <param name="path"></param>
        public static void UnregisterViewsPath(string path)
        {
            if (viewsPaths.Contains(path))
            {
                viewsPaths.Remove(path);
            }
        }

        /// <summary>
        /// Adds the given Partial Views Location path to the registry
        /// </summary>
        /// <param name="path"></param>
        public static void RegisterPartialViewsPath(string path)
        {
            if (!partialViewsPaths.Contains(path))
            {
                partialViewsPaths.Add(path);
            }
        }

        /// <summary>
        /// Removes the given Partial Views Location path from the registry
        /// </summary>
        /// <param name="path"></param>
        public static void UnregisterPartialViewsPath(string path)
        {
            if (partialViewsPaths.Contains(path))
            {
                partialViewsPaths.Remove(path);
            }
        }

        /// <summary>
        /// Gets the current list of registered Views Locations paths
        /// </summary>
        public static string[] ViewsPaths
        {
            get
            {
                return viewsPaths.ToArray();
            }
        }

        /// <summary>
        /// Gets the current list of registered Partial Views Locations paths
        /// </summary>
        public static string[] PartialViewsPaths
        {
            get
            {
                return partialViewsPaths.ToArray();
            }
        }
    }
}
