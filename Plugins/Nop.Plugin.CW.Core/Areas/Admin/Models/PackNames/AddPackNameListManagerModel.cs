using Nop.Web.Framework.Models;

namespace Nop.Plugin.CW.Core.Areas.Admin.Models.PackNames;

public partial record AddPackNameListManagerModel : BaseNopModel
{
    #region Ctor

    public AddPackNameListManagerModel()
    {
        SelectedManagerIds = new List<int>();
    }

    #endregion

    #region Properties

    public int PackNameListId { get; set; }

    public IList<int> SelectedManagerIds { get; set; }

    #endregion
}
