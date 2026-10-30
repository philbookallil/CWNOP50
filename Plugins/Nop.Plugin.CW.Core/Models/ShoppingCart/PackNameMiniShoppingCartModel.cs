using Nop.Web.Models.ShoppingCart;

namespace Nop.Plugin.CW.Core.Models.ShoppingCart;

public record PackNameMiniShoppingCartModel : MiniShoppingCartModel
{
    public int PackNameTotalProducts { get; set; }

    public int ManagerTotalProducts { get; set; }

    public string CurrentPackNameName { get; set; }

    public bool HasOtherPackNameItems { get; set; }
}