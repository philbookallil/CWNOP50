using Microsoft.AspNetCore.Mvc.Rendering;
using Nop.Web.Framework.Models;
using System.ComponentModel;

namespace Nop.Plugin.CW.Core.Areas.Admin.Models.Exchanges.FeeRule;

public partial record FeeRuleSearchModel : BaseSearchModel
{
    public FeeRuleSearchModel()
    {
        AvailableFeeTypes = new List<SelectListItem>();
        AvailableOperatorsTypes = new List<SelectListItem>();
    }

    public int StoreId { get; set; }

    [DisplayName("Fee Type")]
    public int FeeTypeId { get; set; }

    [DisplayName("Operator")]
    public int OperatorId { get; set; }

    public decimal Amount { get; set; }

    public IList<SelectListItem> AvailableFeeTypes { get; set; }

    public IList<SelectListItem> AvailableOperatorsTypes { get; set; }
}