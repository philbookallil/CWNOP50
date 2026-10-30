using Microsoft.AspNetCore.Mvc.Rendering;
using Nop.Web.Framework.Models;

namespace Nop.Plugin.CW.Core.Areas.Admin.Models.Exchanges;

public record ExchangeSettingsSearchModel : BaseSearchModel
{
    public ExchangeSettingsSearchModel()
    {
        AvailableLinkedActions = new List<SelectListItem>();
    }

    #region Properties

    public int StoreId { get; set; }

    public IList<SelectListItem> AvailableLinkedActions { get; set; }

    public IList<int> LinkedActionsIds { get; set; }

    public string DefaultTab { get; set; }

    public string ActiveTab { get; set; }

    #endregion
}
