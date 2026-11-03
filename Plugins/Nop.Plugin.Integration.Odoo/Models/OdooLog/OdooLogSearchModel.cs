using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;
using Nop.Web.Framework.Models;
using Nop.Web.Framework.Mvc.ModelBinding;

namespace Nop.Plugin.Integration.Odoo.Models;

public partial record OdooLogSearchModel : BaseSearchModel
{
    public OdooLogSearchModel()
    {
        AvailableLogLevels = new List<SelectListItem>();
    }

    [NopResourceDisplayName("Plugins.Integration.Odoo.Log.List.CreatedOnFrom")]
    [UIHint("DateNullable")]
    public DateTime? CreatedOnFrom { get; set; }

    [NopResourceDisplayName("Plugins.Integration.Odoo.Log.List.CreatedOnTo")]
    [UIHint("DateNullable")]
    public DateTime? CreatedOnTo { get; set; }

    [NopResourceDisplayName("Plugins.Integration.Odoo.Log.List.Message")]
    public string Message { get; set; }

    [NopResourceDisplayName("Plugins.Integration.Odoo.Log.List.LogLevel")]
    public int LogLevelId { get; set; }

    public IList<SelectListItem> AvailableLogLevels { get; set; }

    /// <summary>
    /// Used by the child table to filter traces by parent event.
    /// </summary>
    public int LogEventId { get; set; }
}
