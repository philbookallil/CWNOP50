using Nop.Web.Framework.Models;

namespace Nop.Plugin.CW.Core.Models.CreditNotes;

public record CreditNotesOrderSummaryModel : BaseNopModel
{
    public bool NeedToShow { get; set; }

    public string Amount { get; set; }
}
