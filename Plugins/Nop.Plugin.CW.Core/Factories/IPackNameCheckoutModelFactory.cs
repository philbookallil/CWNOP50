using Nop.Core.Domain.Customers;
using Nop.Plugin.CW.Core.Domain.Orders.Views;
using Nop.Plugin.CW.Core.Domain.PackNames;
using Nop.Plugin.CW.Core.Domain.Settings;
using Nop.Plugin.CW.Core.Models.PackNameCheckout;
using Nop.Web.Models.Common;

namespace Nop.Plugin.CW.Core.Factories;

public interface IPackNameCheckoutModelFactory
{
    /// <summary>
    /// Prepare packaging name checkout model
    /// </summary>
    /// <param name="cart">Cart</param>
    /// <param name="customer">Customer</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the packaging name checkout model
    /// </returns>
    Task<PackNameCheckoutModel> PreparePackNameCheckoutModelAsync(IList<PackNameShoppingCartItem> packNameCart, Customer customer);

    /// <summary>
    /// Prepare packaging name checkout model
    /// </summary>
    /// <param name="cart">Cart</param>
    /// <param name="customer">Customer</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the packaging name checkout model
    /// </returns>
    Task<PackNameCheckoutModel> PreparePackNameCheckoutModelItemsAsync(IList<PackNameShoppingCartItem> packNameCart, Customer customer, string viewMode = "group");

    /// <summary>
    /// Prepare packaging name box model
    /// </summary>
    /// <param name="item">A cart line of the packname</param>
    /// <param name="packNameCart">Cart</param>
    /// <param name="storeSettings">Store settings</param>
    /// <param name="shippingModel">The packname's shipping, prepared for the whole cart at once</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the packaging name box model
    /// </returns>
    Task<PackNameBoxModel> PreparePackNameBoxModelAsync(PackNameShoppingCartItem item, IList<PackNameShoppingCartItem> packNameCart, MultiStoreSettings storeSettings, PackNameShippingModel shippingModel);

    Task<PackNameShippingAddressModel> PreparePackNameShippingAddressModelAsync(PackNameShippingAddressModel model, PackName packName, string overrideAddressAttributesXml = "");

    /// <summary>
    /// Rates shipping for each packname of the cart and stores the offered and selected options
    /// </summary>
    /// <param name="customer">Customer</param>
    /// <param name="storeId">Store identifier</param>
    /// <param name="cart">Cart</param>
    /// <param name="ratePackNameId">The one packname to rate again; null rates every packname</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    Task SetShippingOptionsByPackNamesAsync(Customer customer, int storeId, IList<PackNameShoppingCartItem> cart, int? ratePackNameId = null);

    /// <summary>
    /// Prepare shipping address model
    /// </summary>
    /// <param name="addressId">Address identifier</param>
    /// <returns></returns>
    Task<AddressModel> PrepareShipAddressModelAsync(int addressId);
}
