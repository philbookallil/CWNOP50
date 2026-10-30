using Microsoft.AspNetCore.Mvc.Rendering;
using Nop.Web.Framework.Models;
using Nop.Web.Framework.Mvc.ModelBinding;

namespace Nop.Plugin.CW.Core.Areas.Admin.Models.Catalog;

public partial record AclDepartmentModel : BaseNopEntityModel
{
    #region Ctor

    public AclDepartmentModel()
    {
        SelectedDepartmentIds = new List<int>();
        AvailableDepartments = new List<SelectListItem>();
    }

    #endregion

    #region Properties

    public int EntityId { get; set; }

    /// <summary>
    /// Gets or sets identifiers of the selected customer roles
    /// </summary>
    [NopResourceDisplayName("Plugins.CW.Core.Admin.AclDepartment.Field.SelectedDepartmentIds")]
    public IList<int> SelectedDepartmentIds { get; set; }

    /// <summary>
    /// Gets or sets items for the all available customer roles
    /// </summary>
    public IList<SelectListItem> AvailableDepartments { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the field shows only in the advanced mode, as nop's customer role field
    /// beside it does
    /// </summary>
    public bool IsAdvancedSetting { get; set; }

    #endregion
}