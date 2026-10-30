using Nop.Core.Domain.Gdpr;
using Nop.Core.Domain.Orders;
using Nop.Data;
using Nop.Services.Events;
using Nop.Services.Orders;

namespace Nop.Plugin.CW.Core.Services.Orders;

/// <summary>
/// Deletes the cart lines a permanent customer delete (GDPR) leaves behind
/// </summary>
/// <remarks>
/// nop deletes the lines the cart service returns for the customer, and CW's cart override leaves out of that answer the
/// packname lines the packname cart view does not show. Every line still in the customer's cart once nop has finished is
/// deleted here, through the cart service, so each line's packname mapping goes with it and its deleted events are
/// published.
/// </remarks>
public class CustomerPermanentlyDeletedEventConsumer : IConsumer<CustomerPermanentlyDeleted>
{
    #region Fields

    protected readonly IRepository<ShoppingCartItem> _shoppingCartItemRepository;
    protected readonly IShoppingCartService _shoppingCartService;

    #endregion

    #region Ctor

    public CustomerPermanentlyDeletedEventConsumer(IRepository<ShoppingCartItem> shoppingCartItemRepository,
        IShoppingCartService shoppingCartService)
    {
        _shoppingCartItemRepository = shoppingCartItemRepository;
        _shoppingCartService = shoppingCartService;
    }

    #endregion

    #region Methods

    /// <summary>
    /// Handles the event
    /// </summary>
    /// <param name="eventMessage">The event message</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    public async Task HandleEventAsync(CustomerPermanentlyDeleted eventMessage)
    {
        var leftBehind = await _shoppingCartItemRepository.GetAllAsync(query =>
            query.Where(item => item.CustomerId == eventMessage.CustomerId));

        foreach (var item in leftBehind)
            await _shoppingCartService.DeleteShoppingCartItemAsync(item, resetCheckoutData: false);
    }

    #endregion
}