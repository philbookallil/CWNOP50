using Nop.Web.Framework.Models;
using Nop.Web.Framework.Mvc.ModelBinding;

namespace Nop.Plugin.CW.Core.Areas.Admin.Models.PackNames;

public record PackNameListRowModel : BaseNopEntityModel
{
    [NopResourceDisplayName("Plugins.CW.Core.Admin.PackNameLists.PackNameList.Field.Name")]
    public string Name { get; set; }

    [NopResourceDisplayName("Plugins.CW.Core.Admin.PackNameLists.PackNameList.Field.IsDefault")]
    public bool IsDefault { get; set; }

    [NopResourceDisplayName("Plugins.CW.Core.Admin.PackNameLists.PackNameList.Field.AllManagers")]
    public bool AllManagers { get; set; }

    [NopResourceDisplayName("Plugins.CW.Core.Admin.PackNameLists.PackNameList.Field.ParentManagers")]
    public bool ParentManagers { get; set; }

    public int PackNameCount { get; set; }

    public int ManagerCount { get; set; }
}
