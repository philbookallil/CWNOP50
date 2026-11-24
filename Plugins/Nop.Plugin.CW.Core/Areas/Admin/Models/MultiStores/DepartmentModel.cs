using Nop.Plugin.CW.Core.Areas.Admin.Models.Approvals;
using Nop.Web.Framework.Models;
using Nop.Web.Framework.Mvc.ModelBinding;

namespace Nop.Plugin.CW.Core.Areas.Admin.Models.MultiStores;

public record DepartmentModel : BaseNopEntityModel
{
    #region Ctor

    public DepartmentModel()
    {
        DepartmentApprovalRule = new ApprovalRuleModel();
        DepartmentManagerSearchModel = new DepartmentManagerSearchModel();
    }

    #endregion

    #region Properties

    [NopResourceDisplayName("Plugins.CW.Core.Admin.Departments.Department.Field.StoreId")]
    public int StoreId { get; set; }

    [NopResourceDisplayName("Plugins.CW.Core.Admin.Departments.Department.Field.Name")]
    public string Name { get; set; }

    [NopResourceDisplayName("Plugins.CW.Core.Admin.Departments.Department.Field.Active")]
    public bool Active { get; set; }

    public bool IsNew { get; set; }

    /// <summary>
    /// Gets or sets the approval panel; not named as the branch form's, since Manage Store keeps both forms on the page
    /// and the fields' ids come from the name
    /// </summary>
    public ApprovalRuleModel DepartmentApprovalRule { get; set; }

    public DepartmentManagerSearchModel DepartmentManagerSearchModel { get; set; }

    #endregion
}
