using Nop.Web.Framework.Models;

namespace Nop.Plugin.CW.Core.Areas.Admin.Models.Exchanges.ReturnStatusRule;

public partial record ReturnStatusRuleSearchModel : BaseSearchModel
{
    public int StoreId { get; set; }
}