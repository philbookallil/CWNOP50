using Nop.Web.Framework.Models;
using Nop.Web.Framework.Mvc.ModelBinding;

namespace Nop.Plugin.Integration.Odoo.Models;

public partial record OdooLogModel : BaseNopEntityModel
{
    [NopResourceDisplayName("Plugins.Integration.Odoo.Log.Fields.LogLevel")]
    public string LogLevel { get; set; }

    [NopResourceDisplayName("Plugins.Integration.Odoo.Log.Fields.ShortMessage")]
    public string ShortMessage { get; set; }

    [NopResourceDisplayName("Plugins.Integration.Odoo.Log.Fields.FullMessage")]
    public string FullMessage { get; set; }

    [NopResourceDisplayName("Plugins.Integration.Odoo.Log.Fields.EntityType")]
    public string EntityType { get; set; }

    [NopResourceDisplayName("Plugins.Integration.Odoo.Log.Fields.OdooEntityId")]
    public int? OdooEntityId { get; set; }

    [NopResourceDisplayName("Plugins.Integration.Odoo.Log.Fields.NopEntityId")]
    public int? NopEntityId { get; set; }

    [NopResourceDisplayName("Plugins.Integration.Odoo.Log.Fields.CreatedOn")]
    public DateTime CreatedOn { get; set; }

    public int LogEventId { get; set; }
}
