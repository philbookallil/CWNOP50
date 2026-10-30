using Nop.Core.Domain.Catalog;
using Nop.Core.Domain.Customers;
using Nop.Core.Domain.Orders;
using Nop.Plugin.CW.Core.Domain.Orders.Views;
using Nop.Plugin.CW.Core.Domain.Settings;
using Nop.Services.Orders;

namespace Nop.Plugin.CW.Core.Services.Orders;

public interface IShoppingCartOverrideService : IShoppingCartService
{
    /// <summary>
    /// Gets the shopping cart items for a specific packName and store by customer. 
    /// </summary>
    /// <param name="customer">The customer for whom to retrieve shopping cart items.</param>
    /// <param name="packNameId">The ID of the packName for which to retrieve shopping cart items.</param>
    /// <param name="storeId">The ID of the store for which to retrieve shopping cart items.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task<IList<ShoppingCartItem>> GetPackNameCartAsync(Customer customer, int packNameId, int storeId = 0);

    /// <summary>
    /// Gets the shopping cart items for a specific packName and store by customer. 
    /// </summary>
    /// <param name="customer">The customer for whom to retrieve shopping cart items.</param>
    /// <param name="packNameId">The ID of the packName for which to retrieve shopping cart items.</param>
    /// <param name="storeId">The ID of the store for which to retrieve shopping cart items.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task<IList<PackNameShoppingCartItem>> GetPackNameShoppingCartItemsAsync(Customer customer, int packNameId = 0, int storeId = 0);

    /// <summary>
    /// Add a product to shopping cart for a packName
    /// </summary>
    /// <param name="customer">Customer</param>
    /// <param name="product">Product</param>
    /// <param name="shoppingCartType">Shopping cart type</param>
    /// <param name="storeId">Store identifier</param>
    /// <param name="packNameId">PackName identifier</param>
    /// <param name="attributesXml">Attributes in XML format</param>
    /// <param name="customerEnteredPrice">The price enter by a customer</param>
    /// <param name="rentalStartDate">Rental start date</param>
    /// <param name="rentalEndDate">Rental end date</param>
    /// <param name="quantity">Quantity</param>
    /// <param name="addRequiredProducts">Whether to add required products</param>
    /// <param name="wishlistId">Wishlist identifier; pass null if it's default wishlist</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the warnings
    /// </returns>
    Task<IList<string>> AddToCartAsync(Customer customer, Product product,
        ShoppingCartType shoppingCartType, int storeId, int packNameId, string attributesXml = null,
        decimal customerEnteredPrice = decimal.Zero,
        DateTime? rentalStartDate = null, DateTime? rentalEndDate = null,
        int quantity = 1, bool addRequiredProducts = true, int? wishlistId = null);

    /// <summary>
    /// Clears the selected shipping addresses for the specified store.
    /// </summary>
    /// <remarks>This method removes all selected shipping addresses associated with the specified store. 
    /// If the storeId is zero, it clears selected shipping addresses for all stores that are not overriding the settings. 
    /// This is useful when you want to reset the selected shipping addresses for a specific store or for all stores in the case of a global setting change. 
    /// Note that this method does not delete the shipping addresses themselves, but rather disassociates them from being pre-selected for checkout for the specified store or stores.
    /// store.</remarks>
    /// <param name="storeId">The unique identifier of the store.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task ClearSelectedShippingAddressesAsync(int storeId);

    /// <summary>
    /// Determines whether the specified address is valid for checkout based on the provided cardCode and packName
    /// </summary>
    /// <remarks>Read-only: nothing is cleared or saved here. A caller that gets <see langword="false"/> either
    /// refuses the address or picks another one.</remarks>
    /// <param name="branchId">The identifier of the branch associated with the address.</param>
    /// <param name="packNameId">The identifier of the pack name associated with the address.</param>
    /// <param name="addressId">The identifier of the address to validate.</param>
    /// <param name="customer">The customer for whom the address is being validated.</param>
    /// <param name="storeId">The identifier of the store in which the validation is being performed.</param>
    /// <param name="storeSettings">The store settings that define the rules for address selection.</param>
    /// <returns><see langword="true"/> if the address is valid for checkout; otherwise, <see langword="false"/>.</returns>
    Task<bool> IsAddressValidForCheckoutAsync(int branchId, int packNameId, int addressId, Customer customer, int storeId, MultiStoreSettings storeSettings);

    /// <summary>
    /// Determines whether a packName can add a new address based on the specified store settings.
    /// </summary>
    /// <param name="storeSettings">The store settings that define the rules for adding a new address.</param>
    /// <returns><see langword="true"/> if address addition is allowed based on the store settings;  otherwise, <see
    /// langword="false"/>.</returns>
    bool PackNameCanAddAddress(MultiStoreSettings storeSettings);

    /// <summary>
    /// Determines whether a packname has at least one address its orders can be shipped to: an address of its
    /// branch or, with home delivery on, one of its own or its customer's.
    /// </summary>
    /// <param name="branchId">The identifier of the branch of the packname's list.</param>
    /// <param name="packNameId">The identifier of the packname.</param>
    /// <param name="packNameCustomerId">The identifier of the packname's customer, or 0 when it has none.</param>
    /// <param name="storeSettings">The store settings that define the rules for address selection.</param>
    /// <returns><see langword="true"/> if at least one address can be used; otherwise, <see langword="false"/>.</returns>
    Task<bool> PackNameHasShippingAddressAsync(int branchId, int packNameId, int packNameCustomerId, MultiStoreSettings storeSettings);

    /// <summary>
    /// Removes every cart line that belongs to a packname, for every customer, and clears that packname's
    /// shipping assignment for each of them. Exchange replacement lines are kept.
    /// </summary>
    /// <param name="packNameId">Packname identifier</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    Task DeletePackNameCartItemsAsync(int packNameId);

    /// <summary>
    /// Checks every line of a packname cart against the allocated-quantity cap as it stands now: a line that
    /// takes more of a product than the packname's subsidy allocation still allows gets a warning.
    /// </summary>
    /// <param name="customer">The customer whose cart it is</param>
    /// <param name="packNameCart">The packname cart</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains one warning per line over the cap; empty when every line is within it
    /// </returns>
    Task<IList<string>> GetSubsidyQuantityCapWarningsAsync(Customer customer, IList<PackNameShoppingCartItem> packNameCart);
}