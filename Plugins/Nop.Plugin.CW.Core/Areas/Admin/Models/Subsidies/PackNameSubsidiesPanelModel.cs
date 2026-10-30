using Nop.Web.Framework.Models;

namespace Nop.Plugin.CW.Core.Areas.Admin.Models.Subsidies;

public record PackNameSubsidiesPanelModel : BaseNopModel
{
    public int PackNameId { get; set; }

    public string Prefix { get; set; }
}
