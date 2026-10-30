using Nop.Web.Framework.Models;

namespace Nop.Plugin.CW.Core.Areas.Admin.Models.CreditNotes;

/// <summary>
/// Represents a credit note search model
/// </summary>
public partial record CreditNoteHistorySearchModel : BaseSearchModel
{
    public int CreditNoteId { get; set; }
}