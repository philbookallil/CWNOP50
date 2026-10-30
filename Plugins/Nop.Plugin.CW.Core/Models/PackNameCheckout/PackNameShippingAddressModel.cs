using Nop.Web.Framework.Models;
using Nop.Web.Models.Common;

namespace Nop.Plugin.CW.Core.Models.PackNameCheckout;

public partial record PackNameShippingAddressModel : BaseNopModel
{
    #region Properties

    public int PackNameId { get; set; }

    public bool Group { get; set; }

    public string Colour { get; set; }

    public bool IsCustomer { get; set; }

    public AddressModel Address { get; set; } = new();

    #endregion   
}
