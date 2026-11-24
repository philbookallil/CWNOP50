using System.Security.Cryptography;
using System.Text;
using Nop.Plugin.CW.Core.Services.Approvals;

namespace Nop.Plugin.CW.Core.Helpers;

/// <summary>
/// The tokens of the approval links: random, URL-safe, stored only as a hash
/// </summary>
public static class OrderApprovalTokenHelper
{
    /// <summary>
    /// Creates a token: <see cref="OrderApprovalDefaults.TokenBytes"/> random bytes, base64url without padding
    /// </summary>
    public static string CreateToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(OrderApprovalDefaults.TokenBytes);

        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    /// <summary>
    /// The hash a token is stored and looked up by: SHA-256 of its UTF-8 text, as 64 lowercase hex characters
    /// </summary>
    /// <param name="token">The token from the link</param>
    /// <returns>The hash; null for an empty token</returns>
    public static string HashToken(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return null;

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token.Trim()))).ToLowerInvariant();
    }
}
