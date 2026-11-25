using AwesomeAssertions;
using Nop.Plugin.Integration.Odoo.Services;
using NUnit.Framework;

namespace CW.Tests.Integration.Odoo;

/// <summary>
/// What publishes a product: being on at least one active Odoo store.
/// </summary>
[TestFixture]
public class ProductPublishingTests
{
    private static readonly HashSet<int> ActiveStores = new() { 1, 2 };

    [TestCase(new[] { 1 }, true)]
    [TestCase(new[] { 2, 9 }, true)]
    [TestCase(new[] { 9 }, false)]
    [TestCase(new int[0], false)]
    [TestCase(null, false)]
    public void A_product_is_published_while_on_an_active_store(int[]? productStores, bool published)
    {
        ProductSyncService.IsOnActiveStore(productStores!, ActiveStores).Should().Be(published);
    }
}
