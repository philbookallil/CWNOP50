using Nop.Web.Framework.Models;

namespace Nop.Plugin.CW.Core.Areas.Admin.Models.CreditNotes;

/// <summary>
/// Represents a credit note search model
/// </summary>
public partial record CreditNoteSearchModel : BaseSearchModel
{
    public int CustomerId { get; set; }
}