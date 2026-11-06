namespace Nop.Plugin.CW.ThemeCore.Services;

/// <summary>
/// Builds the dynamic theme CSS (accent-colour custom properties) for a store.
/// This is the generation seam: Phase 1 inlines the output; Phase 2 can add a
/// file-writer strategy for bulk Custom CSS while reusing this generator.
/// </summary>
public interface ICwThemeCssService
{
    /// <summary>
    /// Generate the <c>:root { --cw-*: … }</c> custom-property block for the given store.
    /// Returns an empty string when no valid colours are configured.
    /// </summary>
    /// <param name="storeId">Store identifier (0 = default/all-stores settings)</param>
    Task<string> GenerateRootCssAsync(int storeId);
}
