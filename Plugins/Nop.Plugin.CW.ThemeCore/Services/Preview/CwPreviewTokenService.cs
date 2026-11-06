using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;

namespace Nop.Plugin.CW.ThemeCore.Services.Preview;

/// <summary>
/// Data-Protection-backed implementation of <see cref="ICwPreviewTokenService"/>. The protector's purpose
/// string isolates these tokens; the underlying key ring is shared app-wide, so a token minted on the admin
/// domain validates on any store domain (see the go-live note about sharing keys across app instances).
/// </summary>
public class CwPreviewTokenService : ICwPreviewTokenService
{
    #region Fields

    protected readonly IDataProtector _protector;

    private static readonly JsonSerializerOptions _json = new() { PropertyNameCaseInsensitive = true };

    #endregion

    #region Ctor

    public CwPreviewTokenService(IDataProtectionProvider dataProtectionProvider)
    {
        _protector = dataProtectionProvider.CreateProtector("CW.ThemeCore.LivePreview.v1");
    }

    #endregion

    #region Methods

    public string Mint(int storeId, string adminOrigin, TimeSpan ttl)
    {
        var payload = new PreviewTokenPayload
        {
            StoreId = storeId,
            AdminOrigin = adminOrigin,
            ExpiresUtc = DateTime.UtcNow.Add(ttl)
        };

        //Protect(string) returns a URL-safe (base64url) string — safe to drop straight into a query value
        return _protector.Protect(JsonSerializer.Serialize(payload, _json));
    }

    public bool TryValidate(string token, int currentStoreId, out PreviewTokenPayload payload)
    {
        payload = null;
        if (string.IsNullOrWhiteSpace(token))
            return false;

        try
        {
            var candidate = JsonSerializer.Deserialize<PreviewTokenPayload>(_protector.Unprotect(token), _json);
            if (candidate is null || candidate.ExpiresUtc < DateTime.UtcNow || candidate.StoreId != currentStoreId)
                return false;

            payload = candidate;
            return true;
        }
        catch
        {
            //tampered / wrong key / malformed → treat as "no preview", never throw into the request
            return false;
        }
    }

    #endregion
}
