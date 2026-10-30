using AwesomeAssertions;
using Moq;
using Nop.Core.Caching;
using Nop.Core.Domain.Gdpr;
using Nop.Core.Domain.Orders;
using Nop.Data;
using Nop.Plugin.CW.Core.Services.Orders;
using Nop.Services.Orders;
using NUnit.Framework;

namespace CW.Tests.Core.Services.Orders;

/// <summary>
/// A permanent customer delete (GDPR) leaves none of the customer's cart lines behind, the packname lines CW's cart
/// override keeps from nop's own delete included
/// </summary>
[TestFixture]
public class CustomerPermanentlyDeletedEventConsumerTests
{
    [Test]
    public async Task Every_line_still_in_the_deleted_customers_cart_is_deleted_through_the_cart_service()
    {
        var lines = new List<ShoppingCartItem>
        {
            new() { Id = 1, CustomerId = 7, ShoppingCartTypeId = (int)ShoppingCartType.ShoppingCart },
            new() { Id = 2, CustomerId = 7, ShoppingCartTypeId = (int)ShoppingCartType.Wishlist },
            new() { Id = 3, CustomerId = 8, ShoppingCartTypeId = (int)ShoppingCartType.ShoppingCart }
        };

        var repository = new Mock<IRepository<ShoppingCartItem>>();
        repository.Setup(repo => repo.GetAllAsync(It.IsAny<Func<IQueryable<ShoppingCartItem>, IQueryable<ShoppingCartItem>>>(),
                It.IsAny<Func<ICacheKeyService, CacheKey>>(), It.IsAny<bool>()))
            .ReturnsAsync((Func<IQueryable<ShoppingCartItem>, IQueryable<ShoppingCartItem>> func, Func<ICacheKeyService, CacheKey> getCacheKey, bool includeDeleted) =>
                (IList<ShoppingCartItem>)func(lines.AsQueryable()).ToList());

        var deleted = new List<int>();
        var cart = new Mock<IShoppingCartService>();
        cart.Setup(service => service.DeleteShoppingCartItemAsync(It.IsAny<ShoppingCartItem>(), It.IsAny<bool>(), It.IsAny<bool>()))
            .Callback<ShoppingCartItem, bool, bool>((item, resetCheckoutData, ensureOnlyActiveCheckoutAttributes) => deleted.Add(item.Id))
            .Returns(Task.CompletedTask);

        await new CustomerPermanentlyDeletedEventConsumer(repository.Object, cart.Object)
            .HandleEventAsync(new CustomerPermanentlyDeleted(7, "ann@cw.test"));

        deleted.Should().BeEquivalentTo(new[] { 1, 2 });
    }
}
