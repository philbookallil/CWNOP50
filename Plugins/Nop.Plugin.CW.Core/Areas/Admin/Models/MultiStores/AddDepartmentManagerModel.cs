using Nop.Web.Framework.Models;

namespace Nop.Plugin.CW.Core.Areas.Admin.Models.MultiStores;

public record AddDepartmentManagerModel : BaseNopModel
{
    #region Ctor

    public AddDepartmentManagerModel()
    {
        SelectedManagerIds = new List<int>();
    }
    #endregion

    #region Properties

    public int DepartmentId { get; set; }

    public IList<int> SelectedManagerIds { get; set; }

    #endregion
}
