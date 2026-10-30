using Microsoft.AspNetCore.Mvc.Rendering;
using Nop.Web.Framework.Models;

namespace Nop.Plugin.CW.Core.Models.Subsidies;

public record SubsidyCatalogModel : BaseNopModel
{
    #region Ctor

    public SubsidyCatalogModel()
    {
        AvailableViewModes = new List<SelectListItem>();
        ProductGroups = new List<SubsidyCatalogProductGroupModel>();
    }

    #endregion

    #region Properties

    public string ViewMode { get; set; }

    public bool AllowProductViewModeChanging { get; set; }

    public IList<SelectListItem> AvailableViewModes { get; set; }

    public IList<SubsidyCatalogProductGroupModel> ProductGroups { get; set; }

    #endregion
}
