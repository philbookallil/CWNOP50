using Nop.Web.Framework.Models;
using Nop.Web.Framework.Mvc.ModelBinding;

namespace Nop.Plugin.CW.Core.Areas.Admin.Models.MultiStores;

//THE BRANCH COLLECTION'S ROW, not the editor's model. CostCenterModel carries the store, the branch
//and an address search model because the detail form needs them; a row needs a name and whether it
//is active. Kept separate so the collection cannot start depending on editor-only state.
public record CostCenterRowModel : BaseNopEntityModel
{
    [NopResourceDisplayName("Plugins.CW.Core.Admin.CostCenters.CostCenter.Field.Name")]
    public string Name { get; set; }

    [NopResourceDisplayName("Plugins.CW.Core.Admin.CostCenters.CostCenter.Field.Active")]
    public bool Active { get; set; }
}