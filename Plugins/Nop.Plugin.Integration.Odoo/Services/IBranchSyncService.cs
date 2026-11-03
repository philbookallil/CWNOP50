namespace Nop.Plugin.Integration.Odoo.Services;

/// <summary>
/// Synchronises Odoo companies and their delivery addresses (res.partner) into a two-level CW.Core
/// Branch hierarchy, driven by the new <c>x_store</c> model.
///
/// Sync strategy:
///   1. Fetch all Odoo stores (x_store), each carrying its linked companies (x_studio_companies).
///   2. For each active store, resolve the mapped nopCommerce store.
///   3. Upsert each company (type company) as a Head Office — a top-level Branch.
///   4. Upsert each of its delivery addresses (type delivery) as a Branch under that Head Office.
///   All branches are keyed on CardCode = Odoo partner id and carry their Address, via
///   <c>IBranchService</c> / <c>IAddressService</c>.
/// </summary>
public interface IBranchSyncService
{
    /// <summary>
    /// Runs a full branch sync across all Odoo stores.
    /// Stores with no matching nopCommerce store are skipped.
    /// </summary>
    /// <returns>Aggregated result across all stores.</returns>
    Task<BranchSyncResult> SyncAllBranchesAsync(int logEventId);
}

/// <summary>
/// Aggregated result of a full branch sync run. Every Odoo partner the run handled — head-office
/// companies and delivery addresses — is counted exactly once (Skipped = archived and never synced, or
/// failed), so Processed is their sum by construction. Stores are the containers, not what is counted.
/// </summary>
public record BranchSyncResult(
    bool Success,
    int Created,
    int Updated,
    int Skipped,
    string ErrorMessage
)
{
    public int Processed => Created + Updated + Skipped;
}
