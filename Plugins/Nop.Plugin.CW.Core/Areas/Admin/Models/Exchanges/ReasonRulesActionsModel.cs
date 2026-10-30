
using Microsoft.AspNetCore.Mvc.Rendering;
using Nop.Web.Framework.Models;
using Nop.Web.Framework.Mvc.ModelBinding;

namespace Nop.Plugin.CW.Core.Areas.Admin.Models.Exchanges;

public record ReasonRulesActionsModel : BaseSearchModel
{
    public ReasonRulesActionsModel()
    {
        AvailableLinkedActions = new List<SelectListItem>();
    }

    public int ReturnReasonId { get; set; }

    public int StoreId { get; set; }

    public IList<SelectListItem> AvailableLinkedActions { get; set; }

    [NopResourceDisplayName("Plugins.CW.Core.Admin.Exchanges.Rules.Fields.LinkedActions")]
    public IList<int> LinkedActionsIds { get; set; }
}