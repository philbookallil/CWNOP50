using Nop.Web.Models.ShoppingCart;

namespace Nop.Plugin.CW.Core.Models.ShoppingCart;

public record CWMiniCartItemModel : MiniShoppingCartModel.ShoppingCartItemModel
{
    #region Related to Return Requests

    public int? ReturnRequestItemId { get; set; }

    /// <summary>
    /// Indicates if this Cart Item belongs to a Return Request
    /// </summary>
    public bool IsReturnedItem
    {
        get
        {
            return ReturnRequestItemId.HasValue && ReturnRequestItemId.Value > 0;
        }
    }

    #endregion

    /// <summary>
    /// Copy every value of nop's mini cart line, so none is lost as nop's model gains properties
    /// </summary>
    public CWMiniCartItemModel(MiniShoppingCartModel.ShoppingCartItemModel shoppingCartItem) : base(shoppingCartItem)
    {
    }
}
