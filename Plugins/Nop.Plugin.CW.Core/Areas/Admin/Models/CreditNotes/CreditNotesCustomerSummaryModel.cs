using Nop.Web.Framework.Models;

namespace Nop.Plugin.CW.Core.Areas.Admin.Models.CreditNotes;

public record CreditNotesCustomerSummaryModel : BaseNopModel
{
    public int CustomerId { get; set; }

    public decimal CreditNotesAvailableAmount { get; set; }

    public string CreditNotesAvailableAmountFormated { get; set; }
}
