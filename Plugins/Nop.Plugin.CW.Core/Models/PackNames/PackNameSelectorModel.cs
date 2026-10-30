using Microsoft.AspNetCore.Mvc.Rendering;
using Nop.Web.Framework.Models;

namespace Nop.Plugin.CW.Core.Models.PackNames;

public record PackNameSelectorModel : BaseNopModel
{
    #region Ctor

    public PackNameSelectorModel()
    {
        AvailablePackNameLists = new List<SelectListItem>();
    }

    #endregion

    #region Properties

    public int SelectedPackNameListId { get; set; }

    public int SelectedPackNameId { get; set; }

    public bool CanAddPackNames { get; set; }

    public bool CanAddByExcel { get; set; }

    public string ReturnUrl { get; set; }

    public IList<SelectListItem> AvailablePackNameLists { get; set; }

    #endregion
}
