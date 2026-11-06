using Nop.Core.Domain.Customers;

namespace Nop.Plugin.CW.ThemeCore.Services.Preview;

/// <summary>
/// Resolves (creating on first use) the per-store "preview customer" used to render live Cart/Checkout in the
/// Brand-Designer preview (Phase 3). It is a hidden <see cref="Customer.IsSystemAccount"/> account with a dummy
/// address and an auto-seeded dummy cart, so nop renders those pages through its normal pipeline without a real
/// session. The preview request impersonates this customer (token-gated) and is GET-only, so nothing here is ever
/// mutated by a viewer.
/// </summary>
public interface ICwPreviewCustomerService
{
    /// <summary>
    /// Get (or create) the store's preview customer and ensure it has a dummy address and a non-empty dummy cart.
    /// </summary>
    /// <param name="storeId">The store the preview is for.</param>
    /// <returns>The preview customer, ready to render Cart/Checkout.</returns>
    Task<Customer> GetOrCreatePreviewCustomerAsync(int storeId);
}
