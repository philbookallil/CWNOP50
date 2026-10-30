using Nop.Web.Framework.Models;


namespace Nop.Plugin.CW.Core.Areas.Admin.Models.CreditNotes;

public record StoreCustomerCreditNotesModel : BaseNopEntityModel
{
    public string CustomerName { get; set; }

    public int NumberOfAvailableCreditNotes { get; set; }

    public decimal CreditNotesValue { get; set; }

    public string CreditNotesValueStr { get; set; }
}