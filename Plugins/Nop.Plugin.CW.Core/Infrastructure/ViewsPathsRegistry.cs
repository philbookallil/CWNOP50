namespace Nop.Plugin.CW.Core.Infrastructure;

/// <summary>
/// The view locations CW.Core searches before the theme's and nop's
/// </summary>
/// <remarks>
/// nop asks for its views by name, so a CW view at one of these locations with the same name as a nop view replaces it.
/// {0} is the view name and {1} the controller name.
/// </remarks>
public static class ViewsPathsRegistry
{
    private const string PLUGIN_FOLDER = "CW.Core";

    /// <summary>
    /// Gets the view locations, in the order they are searched
    /// </summary>
    public static IReadOnlyList<string> ViewsPaths { get; } =
    [
        $"~/Plugins/{PLUGIN_FOLDER}/Views/{{0}}.cshtml",
        $"~/Plugins/{PLUGIN_FOLDER}/Views/{{1}}/{{0}}.cshtml",
        $"~/Plugins/{PLUGIN_FOLDER}/Views/Shared/{{0}}.cshtml",
        $"~/Plugins/{PLUGIN_FOLDER}/Views/Shared/{{1}}/{{0}}.cshtml",
        $"~/Plugins/{PLUGIN_FOLDER}/Views/Shared/Components/{{1}}/{{0}}.cshtml",
        $"~/Plugins/{PLUGIN_FOLDER}/Areas/Admin/Views/{{1}}/{{0}}.cshtml",
        $"~/Plugins/{PLUGIN_FOLDER}/Areas/Admin/Views/Shared/{{0}}.cshtml",
        $"~/Plugins/{PLUGIN_FOLDER}/Areas/Admin/Views/Shared/{{1}}/{{0}}.cshtml",
        $"~/Plugins/{PLUGIN_FOLDER}/Areas/Admin/Views/Shared/Components/{{1}}/{{0}}.cshtml"
    ];
}