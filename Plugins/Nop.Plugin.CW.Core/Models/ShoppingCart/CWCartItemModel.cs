using Nop.Web.Models.ShoppingCart;

namespace Nop.Plugin.CW.Core.Models.ShoppingCart;

public record CWCartItemModel : ShoppingCartModel.ShoppingCartItemModel
{
    public static readonly string DEFAULT_COLOUR = "#DEDEDE";

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

    public CWCartItemModel() : base()
    {
    }

    /// <summary>
    /// Copy every value of nop's cart line, so none is lost as nop's model gains properties
    /// </summary>
    public CWCartItemModel(ShoppingCartModel.ShoppingCartItemModel shoppingCartItem) : base(shoppingCartItem)
    {
    }
}