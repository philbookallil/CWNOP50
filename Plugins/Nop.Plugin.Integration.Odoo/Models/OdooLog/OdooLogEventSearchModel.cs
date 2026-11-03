using Microsoft.AspNetCore.Mvc.Rendering;
using Nop.Web.Framework.Models;
using Nop.Web.Framework.Mvc.ModelBinding;

namespace Nop.Plugin.Integration.Odoo.Models;

public partial record OdooLogEventSearchModel : BaseSearchModel
{
    public OdooLogEventSearchModel()
    {
        AvailableEventTypes = new List<SelectListItem>();
    }

    [NopResourceDisplayName("Plugins.Integration.Odoo.LogEvent.List.EventType")]
    public int EventTypeId { get; set; } = -1;

    public IList<SelectListItem> AvailableEventTypes { get; set; }
}