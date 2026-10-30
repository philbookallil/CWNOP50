using Nop.Web.Framework.Models;
using Nop.Web.Framework.Mvc.ModelBinding;

namespace Nop.Plugin.CW.Core.Areas.Admin.Models.Exchanges.ReturnReasonRule;

public record ReturnReasonRuleModel : BaseNopEntityModel
{
    public int StoreId { get; set; }

    public int ReturnReasonId { get; set; }

    [NopResourceDisplayName("Plugins.CW.Core.Admin.Exchanges.Rules.Fields.ReasonRules")]
    public string ReturnReason { get; set; }

    [NopResourceDisplayName("Plugins.CW.Core.Admin.Exchanges.Rules.Fields.AllowStoreCredit")]
    public bool AllowStoreCredit { get; set; }

    [NopResourceDisplayName("Plugins.CW.Core.Admin.Exchanges.Rules.Fields.AllowExchange")]
    public bool AllowExchange { get; set; }

    [NopResourceDisplayName("Plugins.CW.Core.Admin.Exchanges.Rules.Fields.BackInStock")]
    public bool BackInStock { get; set; }

    [NopResourceDisplayName("Plugins.CW.Core.Admin.Exchanges.Rules.Fields.AllowRepackagingFee")]
    public bool AllowRepackagingFee { get; set; }

    [NopResourceDisplayName("Plugins.CW.Core.Admin.Exchanges.Rules.Fields.AllowShippingFee")]
    public bool AllowShippingFee { get; set; }

    [NopResourceDisplayName("Plugins.CW.Core.Admin.Exchanges.Rules.Fields.IsActive")]
    public bool IsActive { get; set; }

    [NopResourceDisplayName("Plugins.CW.Core.Admin.Exchanges.Rules.Fields.DisplayOrder")]
    public int DisplayOrder { get; set; }

    public int LinkedActions { get; set; }

    public string LinkedActionsNames { get; set; }
}