namespace Nop.Plugin.CW.ThemeCore.Services;

/// <summary>
/// Reference to a store's generated Custom CSS file for rendering.
/// </summary>
public class CwGeneratedCssRef
{
    /// <summary>App-relative (~/) virtual path, for <c>Url.Content</c>.</summary>
    public string VirtualPath { get; set; }

    /// <summary>Cache-busting version (file last-write ticks).</summary>
    public long Version { get; set; }
}

/// <summary>
/// Generates, serves, and cleans up the per-store Custom CSS files (Capability 2, file half of the hybrid).
/// Sanitizes on write via <see cref="ICwCssSanitizer"/>.
/// </summary>
public interface ICwThemeCssFileService
{
    /// <summary>(Re)generate the sanitized file for one store, or delete it if the store has no Custom CSS.</summary>
    Task GenerateAsync(int storeId);

    /// <summary>(Re)generate for every store (call after a settings save, since a default change affects all).</summary>
    Task GenerateForAllStoresAsync();

    /// <summary>
    /// Get the render reference for a store; lazily (re)generates if the store has Custom CSS but the
    /// file is missing (e.g. after a redeploy wiped the plugin folder). Returns null when there is nothing to serve.
    /// </summary>
    Task<CwGeneratedCssRef> GetRenderRefAsync(int storeId);

    /// <summary>Delete all generated files (used on uninstall).</summary>
    Task DeleteAllAsync();
}
