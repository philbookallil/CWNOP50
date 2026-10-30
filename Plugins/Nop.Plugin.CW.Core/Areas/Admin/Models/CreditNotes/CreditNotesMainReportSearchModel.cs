using Nop.Web.Framework.Models;
using Nop.Web.Framework.Mvc.ModelBinding;

namespace Nop.Plugin.CW.Core.Areas.Admin.Models.CreditNotes;

public record CreditNotesMainReportSearchModel : BaseSearchModel
{
    [NopResourceDisplayName("Plugins.CW.Core.Admin.CreditNotes.Report.CreditNotesWithBalance")]
    public int CreditNotesWithBalance { get; set; }


    [NopResourceDisplayName("Plugins.CW.Core.Admin.CreditNotes.Report.Total")]
    public string TotalCreditNotesValue { get; set; }
}