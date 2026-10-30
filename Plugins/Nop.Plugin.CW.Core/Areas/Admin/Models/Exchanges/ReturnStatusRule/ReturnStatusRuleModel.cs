using Nop.Web.Framework.Models;
using Nop.Web.Framework.Mvc.ModelBinding;

namespace Nop.Plugin.CW.Core.Areas.Admin.Models.Exchanges.ReturnStatusRule;

public record ReturnStatusRuleModel : BaseNopEntityModel
{
    public int StoreId { get; set; }

    public int ReturnStatusId { get; set; }

    [NopResourceDisplayName("Plugins.CW.Core.Admin.Exchanges.Rules.Fields.StatusRules")]
    public string ReturnStatus { get; set; }

    [NopResourceDisplayName("Plugins.CW.Core.Admin.Exchanges.Rules.Fields.PerformStoreCredit")]
    public bool PerformStoreCredit { get; set; }

    [NopResourceDisplayName("Plugins.CW.Core.Admin.Exchanges.Rules.Fields.PerformExchange")]
    public bool PerformExchange { get; set; }

    [NopResourceDisplayName("Plugins.CW.Core.Admin.Exchanges.Rules.Fields.BackInStock")]
    public bool BackInStock { get; set; }

    [NopResourceDisplayName("Plugins.CW.Core.Admin.Exchanges.Rules.Fields.Authorised")]
    public bool Authorised { get; set; }
}