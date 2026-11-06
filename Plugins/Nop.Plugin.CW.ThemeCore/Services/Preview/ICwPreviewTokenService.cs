namespace Nop.Plugin.CW.ThemeCore.Services.Preview;

/// <summary>
/// Mints and validates the signed live-preview URL token. The token lets the Brand-Designer live preview
/// (an iframe of the real storefront) run in "preview mode" on the store's own domain without relying on the
/// admin's auth cookie — which does not travel cross-domain. Backed by ASP.NET Core Data Protection:
/// unforgeable, expiring, stateless, and valid across every store domain via the shared key ring.
/// </summary>
public interface ICwPreviewTokenService
{
    /// <summary>Mint a URL-safe token authorising a preview of <paramref name="storeId"/> framed by <paramref name="adminOrigin"/>.</summary>
    string Mint(int storeId, string adminOrigin, TimeSpan ttl);

    /// <summary>
    /// Validate a token against the current store. Returns false (and null payload) when the token is
    /// missing, tampered, expired, or minted for a different store.
    /// </summary>
    bool TryValidate(string token, int currentStoreId, out PreviewTokenPayload payload);
}
