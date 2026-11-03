using Nop.Core;

namespace Nop.Plugin.Integration.Odoo.Domain;

/// <summary>
/// Represents a synchronization event that groups related OdooLog traces.
/// </summary>
public class OdooLogEvent : BaseEntity
{
    /// <summary>
    /// Gets or sets the display name (e.g. "Product Sync", "Branch Sync").
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the event type identifier.
    /// </summary>
    public int EventTypeId { get; set; }

    /// <summary>
    /// Gets or sets the UTC date/time when the sync event started.
    /// </summary>
    public DateTime StartedOnUtc { get; set; }

    /// <summary>
    /// Gets or sets the UTC date/time when the sync event finished (null if still running).
    /// </summary>
    public DateTime? FinishedOnUtc { get; set; }

    /// <summary>
    /// Gets or sets the number of entities created during the sync.
    /// </summary>
    public int TotalCreated { get; set; }

    /// <summary>
    /// Gets or sets the number of entities updated during the sync.
    /// </summary>
    public int TotalUpdated { get; set; }

    /// <summary>
    /// Gets or sets the number of entities skipped during the sync.
    /// </summary>
    public int TotalSkipped { get; set; }

    /// <summary>
    /// Gets or sets the number of stores/items processed during the sync.
    /// </summary>
    public int TotalProcessed { get; set; }

    /// <summary>
    /// Gets or sets the sync event type.
    /// </summary>
    public SyncEventType EventType
    {
        get => (SyncEventType)EventTypeId;
        set => EventTypeId = (int)value;
    }
}