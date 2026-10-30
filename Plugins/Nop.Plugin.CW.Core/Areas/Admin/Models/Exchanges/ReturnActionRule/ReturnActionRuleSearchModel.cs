using Nop.Web.Framework.Models;

namespace Nop.Plugin.CW.Core.Areas.Admin.Models.Exchanges.ReturnActionRule;

public partial record ReturnActionRuleSearchModel : BaseSearchModel
{
    public int StoreId { get; set; }
}