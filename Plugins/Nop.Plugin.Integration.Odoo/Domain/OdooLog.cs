using Nop.Core;
using Nop.Core.Domain.Logging;

namespace Nop.Plugin.Integration.Odoo.Domain;

/// <summary>
/// Represents an Odoo synchronization log entry.
/// Modeled after Nop.Core.Domain.Logging.Log.
/// </summary>
public class OdooLog : BaseEntity
{
    /// <summary>
    /// Gets or sets the log level identifier
    /// </summary>
    public int LogLevelId { get; set; }

    /// <summary>
    /// Gets or sets the short message
    /// </summary>
    public string ShortMessage { get; set; }

    /// <summary>
    /// Gets or sets the full message (stack trace, details)
    /// </summary>
    public string FullMessage { get; set; }

    /// <summary>
    /// Gets or sets the entity type involved (e.g. "Product", "Variant", "Attribute")
    /// </summary>
    public string EntityType { get; set; }

    /// <summary>
    /// Gets or sets the Odoo entity ID
    /// </summary>
    public int? OdooEntityId { get; set; }

    /// <summary>
    /// Gets or sets the nopCommerce entity ID
    /// </summary>
    public int? NopEntityId { get; set; }

    /// <summary>
    /// Gets or sets the log event identifier that groups this trace.
    /// </summary>
    public int? LogEventId { get; set; }

    /// <summary>
    /// Gets or sets the date and time of creation (UTC)
    /// </summary>
    public DateTime CreatedOnUtc { get; set; }

    /// <summary>
    /// Gets or sets the log level
    /// </summary>
    public LogLevel LogLevel
    {
        get => (LogLevel)LogLevelId;
        set => LogLevelId = (int)value;
    }
}
