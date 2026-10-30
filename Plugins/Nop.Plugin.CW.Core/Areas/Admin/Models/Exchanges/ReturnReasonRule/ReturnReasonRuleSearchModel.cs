using Nop.Web.Framework.Models;

namespace Nop.Plugin.CW.Core.Areas.Admin.Models.Exchanges.ReturnReasonRule;

public partial record ReturnReasonRuleSearchModel : BaseSearchModel
{
    public int StoreId { get; set; }

}