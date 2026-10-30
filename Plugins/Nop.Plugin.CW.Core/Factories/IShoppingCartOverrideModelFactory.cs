using Nop.Core.Domain.Orders;
using Nop.Plugin.CW.Core.Domain.Orders.Views;
using Nop.Plugin.CW.Core.Models.PackNameCheckout;
using Nop.Web.Factories;
using Nop.Web.Models.ShoppingCart;

namespace Nop.Plugin.CW.Core.Factories;

public interface IShoppingCartOverrideModelFactory : IShoppingCartModelFactory
{
    /// <summary>
    /// Prepare the shopping cart model
    /// </summary>
    /// <param name="model">Shopping cart model</param>
    /// <param name="cart">List of the shopping cart item</param>
    /// <param name="loadItems">Whether to load items</param>
    /// <param name="isEditable">Whether model is editable</param>
    /// <param name="validateCheckoutAttributes">Whether to validate checkout attributes</param>
    /// <param name="prepareAndDisplayOrderReviewData">Whether to prepare and display order review data</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the shopping cart model
    /// </returns>
    Task<PackNameShoppingCartModel> PreparePackNameShoppingCartModelAsync(PackNameShoppingCartModel model,
        IList<PackNameShoppingCartItem> packNameCart, bool loadItems = true, bool isEditable = true,
        bool validateCheckoutAttributes = false,
        bool prepareAndDisplayOrderReviewData = false);

    /// <summary>
    /// Prepare the checkout attribute models
    /// </summary>
    /// <param name="cart">List of the shopping cart item</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the list of the checkout attribute model
    /// </returns>
    Task<IList<ShoppingCartModel.CheckoutAttributeModel>> PreparePackNameCheckoutAttributeModelsAsync(
       IList<ShoppingCartItem> cart);

    /// <summary>
    /// Prepare the order totals model
    /// </summary>
    /// <param name="packNameCart">List of the shopping cart item</param>
    /// <param name="isEditable">Whether model is editable</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the order totals model
    /// </returns>
    Task<PackNameOrderTotalsModel> PreparePackNameOrderTotalsModelAsync(IList<PackNameShoppingCartItem> packNameCart, bool isEditable);
}
