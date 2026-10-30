using Nop.Web.Framework.Models;

namespace Nop.Plugin.CW.Core.Areas.Admin.Models.CreditNotes;

/// <summary>
/// Model class for a Credit Note record
/// </summary>
public record CreditNoteModel : BaseNopEntityModel
{
    #region Properties

    public decimal Amount { get; set; }

    public decimal Remain { get; set; }

    public string AmountFormated { get; set; }

    public string RemainFormated { get; set; }


    public string Name { get; set; }

    public string Reason { get; set; }

    public int OrderId { get; set; }

    public int CustomerId { get; set; }

    public int? ReturnRequestId { get; set; }

    public DateTime CreatedOnUtc { get; set; }
    public string CreatedOn { get; set; }
    public bool AllowDelete { get; set; }

    public bool IsReturnRequest
    {
        get { return (ReturnRequestId != null && ReturnRequestId > 0); }   // get method
    }

    #endregion
}
