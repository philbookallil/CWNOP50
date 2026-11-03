using Nop.Core;

namespace Nop.Plugin.Integration.Odoo.Domain;

/// <summary>
/// Maps an Odoo <c>x_store</c> record to its nopCommerce <see cref="Nop.Core.Domain.Stores.Store"/>,
/// and tracks the last synced revision for incremental sync.
/// </summary>
public class OdooStoreMapping : BaseEntity
{
    /// <summary>
    /// Gets or sets the Odoo x_store id.
    /// </summary>
    public int OdooStoreId { get; set; }

    /// <summary>
    /// Gets or sets the nopCommerce Store id.
    /// </summary>
    public int NopStoreId { get; set; }

    /// <summary>
    /// Gets or sets the last processed Odoo <c>x_studio_sync_revision</c>.
    /// </summary>
    public int LastSyncedRevision { get; set; }

    /// <summary>
    /// Gets or sets the last processed Odoo <c>write_date</c>, stored as returned by Odoo and compared
    /// as an opaque token. Null for rows written before this column existed (never skipped).
    /// </summary>
    public string LastSyncedWriteDate { get; set; }

    /// <summary>
    /// Gets or sets the date and time of creation (UTC).
    /// </summary>
    public DateTime CreatedOnUtc { get; set; }

    /// <summary>
    /// Gets or sets the date and time of the last update (UTC).
    /// </summary>
    public DateTime UpdatedOnUtc { get; set; }
}
