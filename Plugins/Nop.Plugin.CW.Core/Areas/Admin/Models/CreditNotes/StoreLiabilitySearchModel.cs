using Nop.Web.Framework.Models;

namespace Nop.Plugin.CW.Core.Areas.Admin.Models.CreditNotes;

public partial record StoreLiabilitySearchModel : BaseSearchModel
{
    public int StoreId { get; set; }
}