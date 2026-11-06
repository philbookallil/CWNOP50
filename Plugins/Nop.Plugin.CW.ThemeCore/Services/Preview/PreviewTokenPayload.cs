namespace Nop.Plugin.CW.ThemeCore.Services.Preview;

/// <summary>
/// The signed payload carried by a live-preview URL token. Authorises "preview mode" for one store on its
/// own domain: the app serves every store domain and shares one Data Protection key ring, so a token minted
/// on the admin host validates cross-origin on the store host. Not a persisted entity — it exists only
/// inside the protected token string.
/// </summary>
public class PreviewTokenPayload
{
    /// <summary>The store the token authorises a preview for (must match the domain's resolved store).</summary>
    public int StoreId { get; set; }

    /// <summary>The admin origin (scheme://host[:port]) allowed to frame the store and exchange messages.</summary>
    public string AdminOrigin { get; set; }

    /// <summary>UTC expiry; the token is rejected after this.</summary>
    public DateTime ExpiresUtc { get; set; }
}
