using Nop.Plugin.Integration.Odoo.Domain;

namespace Nop.Plugin.Integration.Odoo.Services;

/// <summary>
/// Reads and writes the Odoo↔nopCommerce store id mapping (<see cref="OdooStoreMapping"/>).
/// </summary>
public interface IOdooStoreMapService
{
    /// <summary>Gets the mapping row for an Odoo store id, or null.</summary>
    Task<OdooStoreMapping> GetByOdooStoreIdAsync(int odooStoreId);

    /// <summary>Gets the nopCommerce store id mapped to an Odoo store id, or null.</summary>
    Task<int?> GetNopStoreIdAsync(int odooStoreId);

    /// <summary>
    /// Loads every mapping as an Odoo store id → nopCommerce store id lookup, for a run that resolves
    /// many store ids (one query instead of one per id). Both columns are unique (see
    /// <c>OdooStoreMappingUniqueIndexesMigration</c>), so the lookup is also invertible.
    /// </summary>
    Task<Dictionary<int, int>> GetNopStoreIdLookupAsync();

    /// <summary>Inserts or updates the mapping for an Odoo store id, recording the synced revision and write_date.</summary>
    Task UpsertAsync(int odooStoreId, int nopStoreId, int revision, string writeDate);
}
