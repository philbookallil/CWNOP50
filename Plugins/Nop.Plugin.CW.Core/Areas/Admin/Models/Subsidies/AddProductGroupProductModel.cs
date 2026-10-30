using Nop.Web.Framework.Models;

namespace Nop.Plugin.CW.Core.Areas.Admin.Models.Subsidies;

public partial record AddProductGroupProductModel : BaseNopModel
{
    #region Ctor

    public AddProductGroupProductModel()
    {
        SelectedProductIds = new List<int>();
    }

    #endregion

    #region Properties

    public int ProductGroupId { get; set; }

    public IList<int> SelectedProductIds { get; set; }

    #endregion
}
