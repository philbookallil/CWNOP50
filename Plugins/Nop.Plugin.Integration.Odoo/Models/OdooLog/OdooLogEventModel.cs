using Nop.Web.Framework.Models;
using Nop.Web.Framework.Mvc.ModelBinding;

namespace Nop.Plugin.Integration.Odoo.Models;

public partial record OdooLogEventModel : BaseNopEntityModel
{
    [NopResourceDisplayName("Plugins.Integration.Odoo.LogEvent.Fields.Name")]
    public string Name { get; set; }

    [NopResourceDisplayName("Plugins.Integration.Odoo.LogEvent.Fields.EventType")]
    public string EventType { get; set; }

    [NopResourceDisplayName("Plugins.Integration.Odoo.LogEvent.Fields.StartedOn")]
    public DateTime StartedOn { get; set; }

    [NopResourceDisplayName("Plugins.Integration.Odoo.LogEvent.Fields.FinishedOn")]
    public DateTime? FinishedOn { get; set; }

    [NopResourceDisplayName("Plugins.Integration.Odoo.LogEvent.Fields.TraceCount")]
    public int TraceCount { get; set; }

    [NopResourceDisplayName("Plugins.Integration.Odoo.LogEvent.Fields.HasErrors")]
    public bool HasErrors { get; set; }

    /// <summary>
    /// Final status: "Done", "Running", or "Failed".
    /// </summary>
    public string Status { get; set; }

    /// <summary>
    /// Human-readable summary of sync statistics (e.g. "Created: 5 | Updated: 3 | Skipped: 0 | Processed: 2").
    /// </summary>
    public string Summary { get; set; }
}