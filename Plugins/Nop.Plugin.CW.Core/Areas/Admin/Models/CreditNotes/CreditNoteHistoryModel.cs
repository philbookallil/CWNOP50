using Nop.Web.Framework.Models;

namespace Nop.Plugin.CW.Core.Areas.Admin.Models.CreditNotes;

/// <summary>
/// Model class for Credit Note History record item
/// </summary>
public record CreditNoteHistoryModel : BaseNopEntityModel
{
    #region Properties

    public int CreditNoteId { get; set; }

    public int CustomerId { get; set; }

    public int OrderId { get; set; }

    public DateTime CreatedOnUtc { get; set; }

    public decimal Amount { get; set; }

    public decimal Remain { get; set; }

    public string AmountFormated { get; set; }

    public string RemainFormated { get; set; }

    public string CreatedOn { get; set; }

    #endregion 
}
