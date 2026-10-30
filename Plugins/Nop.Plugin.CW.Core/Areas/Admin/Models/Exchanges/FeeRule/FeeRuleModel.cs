using Nop.Web.Framework.Models;

namespace Nop.Plugin.CW.Core.Areas.Admin.Models.Exchanges.FeeRule;

public record FeeRuleModel : BaseNopEntityModel
{
    public int StoreId { get; set; }

    public int FeeTypeId { get; set; }

    public string FeeTypeName { get; set; }

    public decimal Amount { get; set; }

    public int OperatorId { get; set; }

    public string OperatorName { get; set; }

    public int UnitsQuantity { get; set; }

    public bool IsInherited { get; set; }
}