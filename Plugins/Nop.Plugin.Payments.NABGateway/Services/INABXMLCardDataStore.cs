namespace Nop.Plugin.Payments.NABGateway.Services;

/// <summary>
/// Holds the card a shopper entered on the payment-info step until the order is confirmed (see <see cref="NABXMLCardDataStore"/>).
/// </summary>
public interface INABXMLCardDataStore
{
    /// <summary>
    /// Keeps the card for the customer's checkout in a store, replacing any card kept before.
    /// </summary>
    /// <param name="storeId">Store identifier</param>
    /// <param name="customerId">Customer identifier</param>
    /// <param name="card">Card details</param>
    void Save(int storeId, int customerId, NABXMLCardData card);

    /// <summary>
    /// Returns the card kept for the customer's checkout in a store and removes it, so it is used once.
    /// </summary>
    /// <param name="storeId">Store identifier</param>
    /// <param name="customerId">Customer identifier</param>
    /// <returns>The card, or null when none is kept (never entered, expired, or kept by another instance)</returns>
    NABXMLCardData Take(int storeId, int customerId);
}
