using Nop.Web.Framework.Models;

namespace Nop.Plugin.CW.Core.Areas.Admin.Models.Exchanges.ReturnReasonRule;

public partial record ReturnReasonsRuleSearchModel : BaseSearchModel
{
    public int Id { get; set; }
}