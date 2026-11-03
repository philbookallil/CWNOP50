namespace Nop.Plugin.Integration.Odoo.Services;

/// <summary>
/// Synchronises Odoo stores (<c>x_store</c>) into nopCommerce core Stores, tracked in
/// <see cref="Nop.Plugin.Integration.Odoo.Domain.OdooStoreMapping"/>.
/// </summary>
public interface IStoreSyncService
{
    /// <summary>
    /// Creates/updates a nopCommerce Store for each active Odoo store and records the mapping.
    /// </summary>
    Task<StoreSyncResult> SyncStoresAsync(int logEventId);
}

/// <summary>
/// Aggregated result of a store sync run. Every x_store the run retrieved is counted exactly once
/// (Skipped = archived, unchanged, duplicate-in-Odoo or failed), so Processed is their sum by construction.
/// </summary>
public record StoreSyncResult(int Created, int Updated, int Skipped)
{
    public int Processed => Created + Updated + Skipped;
}
