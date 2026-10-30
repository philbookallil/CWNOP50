using Nop.Web.Framework.Models;

namespace Nop.Plugin.CW.Core.Models.PackNameCheckout;

public record PackNameShippingTotalsModel : BaseNopModel
{
    public string Shipping { get; set; }

    public string SelectedShippingMethod { get; set; }

    public string EntityName { get; set; }
}
