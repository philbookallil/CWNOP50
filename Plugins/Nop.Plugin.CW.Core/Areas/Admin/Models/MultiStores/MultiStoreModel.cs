using Nop.Web.Framework.Models;
using Nop.Web.Framework.Mvc.ModelBinding;

namespace Nop.Plugin.CW.Core.Areas.Admin.Models.MultiStores;

public record MultiStoreModel : BaseNopEntityModel
{
    #region Properties

    [NopResourceDisplayName("Admin.Configuration.Stores.Fields.Name")]
    public string Name { get; set; }

    [NopResourceDisplayName("Admin.Configuration.Stores.Fields.DisplayOrder")]
    public int DisplayOrder { get; set; }

    [NopResourceDisplayName("Plugins.CW.Core.Admin.MultiStores.Store.Fields.BranchesCount")]
    public int BranchesCount { get; set; }

    [NopResourceDisplayName("Admin.Configuration.Stores.Fields.Url")]
    public string Url { get; set; }

    [NopResourceDisplayName("Admin.Configuration.Stores.Fields.CompanyName")]
    public string CompanyName { get; set; }

    #endregion
}
