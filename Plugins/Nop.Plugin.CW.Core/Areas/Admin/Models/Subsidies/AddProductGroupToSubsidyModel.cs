using Nop.Web.Framework.Models;

namespace Nop.Plugin.CW.Core.Areas.Admin.Models.Subsidies;

public partial record AddProductGroupToSubsidyModel : BaseNopModel
{
    #region Ctor

    public AddProductGroupToSubsidyModel()
    {
        SelectedProductGroupIds = new List<int>();
    }
    #endregion

    #region Properties

    public int SubsidyId { get; set; }

    public IList<int> SelectedProductGroupIds { get; set; }

    #endregion
}
