using Nop.Web.Framework.Models;

namespace Nop.Plugin.CW.Core.Areas.Admin.Models.CreditNotes;

public record StoreLiabilityModel : BaseNopEntityModel
{
    public int StoreId { get; set; }

    public string StoreName { get; set; }

    public int CreditNotesIssued { get; set; }

    public int CreditNotesConsumed { get; set; }

    public decimal LiabilityValue { get; set; }

    public string LiabilityValueStr { get; set; }

    public decimal PercentageUsed { get; set; }

    public string PercentageUsedStr { get; set; }

    public decimal IssuedCreditNotesValue { get; set; }

    public string IssuedCreditNotesValueStr { get; set; }
}