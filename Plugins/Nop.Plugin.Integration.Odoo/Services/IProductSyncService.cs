namespace Nop.Plugin.Integration.Odoo.Services;

public interface IProductSyncService
{
    /// <summary>
    /// Synchronizes products from Odoo: creates/updates nopCommerce products for the active, sellable
    /// templates and unpublishes the ones archived in Odoo.
    /// </summary>
    Task<ProductSyncResult> SyncOdooProductsAsync(int logEventId);
}

/// <summary>
/// Aggregated result of a product sync run. Every Odoo template the run looked at is counted exactly
/// once (archived templates only if they exist in NOP), so Processed is their sum by construction.
/// </summary>
public record ProductSyncResult(int Created, int Updated, int Skipped)
{
    public int Processed => Created + Updated + Skipped;
}
