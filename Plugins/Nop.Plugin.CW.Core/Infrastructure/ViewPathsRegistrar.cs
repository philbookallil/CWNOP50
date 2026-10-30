namespace Nop.Plugin.CW.Core.Infrastructure
{
    /// <summary>
    /// Allows Plugins to register their Views paths
    /// </summary>
    public class ViewPathsRegistrar
    {
        /// <summary>
        /// Defines the View Path Type
        /// </summary>
        protected enum ViewPathType
        {
            VIEW = 1,
            PARTIAL_VIEW = 2
        }

        #region Properties

        protected string[] _viewsLocations = { };
        protected string[] _areaViewsLocations = { };
        protected string[] _partialViewsLocations = { };
        protected string[] _areaPartialViewsLocations = { };

        #endregion

        #region Methods

        /// <summary>
        /// Refreshes the list given type of paths locations by reading from the paths registry
        /// </summary>
        /// <param name="viewPathType"></param>
        protected void ReloadRegisteredPaths(ViewPathType viewPathType)
        {
            var viewsPaths = ViewsPathsRegistry.ViewsPaths;
            var partialViewsPaths = ViewsPathsRegistry.PartialViewsPaths;

            if (viewPathType == ViewPathType.VIEW && viewsPaths.Length > 0)
            {
                _viewsLocations = viewsPaths;
                _areaViewsLocations = viewsPaths;
            }

            if (viewPathType == ViewPathType.PARTIAL_VIEW && partialViewsPaths.Length > 0)
            {
                _partialViewsLocations = partialViewsPaths;
                _areaPartialViewsLocations = partialViewsPaths;
            }
        }

        /// <summary>
        /// Registers the given views location path
        /// </summary>
        /// <param name="path"></param>
        /// <param name="reloadRegistry">If true refreshes the local copy of registered views locations</param>
        public void RegisterViewsPath(string path, bool reloadRegistry = true)
        {
            ViewsPathsRegistry.RegisterViewsPath(path);
            if (reloadRegistry)
            {
                ReloadRegisteredPaths(ViewPathType.VIEW);
            }
        }

        /// <summary>
        /// Unregisters the given views location path
        /// </summary>
        /// <param name="path"></param>
        /// <param name="reloadRegistry">If true refreshes the local copy of registered views locations</param>
        public void UnregisterViewsPath(string path, bool reloadRegistry = true)
        {
            ViewsPathsRegistry.UnregisterViewsPath(path);
            if (reloadRegistry)
            {
                ReloadRegisteredPaths(ViewPathType.VIEW);
            }
        }

        /// <summary>
        /// Registers the given partial views location path
        /// </summary>
        /// <param name="path"></param>
        /// <param name="reloadRegistry">If true refreshes the local copy of registered views locations</param>
        public void RegisterPartialViewsPath(string path, bool reloadRegistry = true)
        {
            ViewsPathsRegistry.RegisterPartialViewsPath(path);
            if (reloadRegistry)
            {
                ReloadRegisteredPaths(ViewPathType.PARTIAL_VIEW);
            }
        }

        /// <summary>
        /// Unregisters the given partial views location path
        /// </summary>
        /// <param name="path"></param>
        /// <param name="reloadRegistry">If true refreshes the local copy of registered views locations</param>
        public void UnregisterPartialViewsPath(string path, bool reloadRegistry = true)
        {
            ViewsPathsRegistry.UnregisterPartialViewsPath(path);
            if (reloadRegistry)
            {
                ReloadRegisteredPaths(ViewPathType.PARTIAL_VIEW);
            }
        }

        /// <summary>
        /// Registers the given list of view location paths using the given type
        /// </summary>
        /// <param name="paths"></param>
        /// <param name="viewPathType"></param>
        protected void RegisterViewTypePaths(IList<string> paths, ViewPathType viewPathType)
        {
            foreach (var path in paths)
            {
                switch (viewPathType)
                {
                    case ViewPathType.VIEW:
                        RegisterViewsPath(path, false);
                        break;

                    case ViewPathType.PARTIAL_VIEW:
                        RegisterPartialViewsPath(path, false);
                        break;
                }
            }

            ReloadRegisteredPaths(viewPathType);
        }

        /// <summary>
        /// Registers the given list of views and partial views locations
        /// </summary>
        /// <param name="viewsPaths"></param>
        /// <param name="partialViewsPaths"></param>
        public void RegisterViewsPaths(IList<string> viewsPaths = null, IList<string> partialViewsPaths = null)
        {
            if (viewsPaths != null && viewsPaths.Count > 0)
            {
                RegisterViewTypePaths(viewsPaths, ViewPathType.VIEW);
            }

            if (partialViewsPaths != null && partialViewsPaths.Count > 0)
            {
                RegisterViewTypePaths(partialViewsPaths, ViewPathType.PARTIAL_VIEW);
            }
        }

        #endregion
    }
}
