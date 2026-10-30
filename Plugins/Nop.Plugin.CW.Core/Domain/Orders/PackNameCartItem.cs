using Nop.Core;

namespace Nop.Plugin.CW.Core.Domain.Orders;

public partial class PackNameCartItem : BaseEntity
{
    public int ShoppingCartItemId { get; set; }

    public int PackNameId { get; set; }
}
