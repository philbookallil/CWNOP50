using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Caching.Memory;

namespace Nop.Plugin.Payments.NABGateway.Services;

/// <summary>
/// Holds the card a shopper entered on the payment-info step until the order is confirmed.
/// </summary>
/// <remarks>
/// nop 5.00 carries the payment request between checkout steps as JSON in the <c>GenericAttribute</c> table and no
/// longer has card fields on it, so the card cannot travel with the request. It is kept here instead: in process memory
/// only, encrypted with ASP.NET Data Protection, for <see cref="NABXMLPaymentDefaults.CardDataLifetimeMinutes"/>
/// minutes, and removed when the payment is sent. Nothing reaches the database. A second application instance does
/// not see it, so a web farm needs sticky sessions; when the card is gone the shopper is asked to enter it again.
/// </remarks>
public class NABXMLCardDataStore : INABXMLCardDataStore
{
    #region Fields

    protected readonly IMemoryCache _memoryCache;
    protected readonly IDataProtector _protector;

    #endregion

    #region Ctor

    public NABXMLCardDataStore(IMemoryCache memoryCache, IDataProtectionProvider dataProtectionProvider)
    {
        _memoryCache = memoryCache;
        _protector = dataProtectionProvider.CreateProtector(NABXMLPaymentDefaults.CardDataProtectionPurpose);
    }

    #endregion

    #region Utilities

    protected static string GetKey(int storeId, int customerId)
    {
        return $"{NABXMLPaymentDefaults.CardDataProtectionPurpose}.{storeId}.{customerId}";
    }

    #endregion

    #region Methods

    /// <summary>
    /// Keeps the card for the customer's checkout in a store, replacing any card kept before.
    /// </summary>
    /// <param name="storeId">Store identifier</param>
    /// <param name="customerId">Customer identifier</param>
    /// <param name="card">Card details</param>
    public virtual void Save(int storeId, int customerId, NABXMLCardData card)
    {
        ArgumentNullException.ThrowIfNull(card);

        var protectedCard = _protector.Protect(JsonSerializer.SerializeToUtf8Bytes(card));
        _memoryCache.Set(GetKey(storeId, customerId), protectedCard,
            TimeSpan.FromMinutes(NABXMLPaymentDefaults.CardDataLifetimeMinutes));
    }

    /// <summary>
    /// Returns the card kept for the customer's checkout in a store and removes it, so it is used once.
    /// </summary>
    /// <param name="storeId">Store identifier</param>
    /// <param name="customerId">Customer identifier</param>
    /// <returns>The card, or null when none is kept (never entered, expired, or kept by another instance)</returns>
    public virtual NABXMLCardData Take(int storeId, int customerId)
    {
        var key = GetKey(storeId, customerId);
        if (!_memoryCache.TryGetValue(key, out byte[] protectedCard))
            return null;

        _memoryCache.Remove(key);

        return JsonSerializer.Deserialize<NABXMLCardData>(_protector.Unprotect(protectedCard));
    }

    #endregion
}
