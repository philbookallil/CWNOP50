using AwesomeAssertions;
using Microsoft.AspNetCore.Http;
using Nop.Plugin.CW.ThemeCore.Infrastructure;
using NUnit.Framework;

namespace CW.Tests.CW.ThemeCore;

/// <summary>
/// The live-preview middleware writes the token's admin origin into the response's <c>Content-Security-Policy</c>
/// header, so the origin must be a bare http(s) origin that cannot add a header or a CSP directive. It renders the cart
/// and one-page checkout as the hidden preview customer, so it must recognise those paths.
/// </summary>
[TestFixture]
public class CwPreviewFramingMiddlewareTests
{
    #region Safe origin

    [TestCase("https://admin.example")]
    [TestCase("https://localhost:60333")]
    [TestCase("http://admin.example:8080")]
    public void A_bare_http_or_https_origin_is_safe(string origin)
    {
        CwPreviewFramingMiddleware.IsSafeOrigin(origin).Should().BeTrue();
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("admin.example")]
    [TestCase("ftp://admin.example")]
    [TestCase("javascript:alert(1)")]
    [TestCase("//admin.example")]
    public void Anything_that_is_not_an_http_or_https_origin_is_unsafe(string? origin)
    {
        CwPreviewFramingMiddleware.IsSafeOrigin(origin!).Should().BeFalse();
    }

    [TestCase("https://admin.example\r\nSet-Cookie: x=1")]
    [TestCase("https://admin.example\nX-Other: 1")]
    [TestCase("https://admin.example; script-src *")]
    [TestCase("https://admin.example https://evil.example")]
    [TestCase("https://admin.example,https://evil.example")]
    public void An_origin_that_could_add_a_header_or_a_csp_source_is_unsafe(string origin)
    {
        CwPreviewFramingMiddleware.IsSafeOrigin(origin).Should().BeFalse();
    }

    #endregion

    #region Cart and checkout paths

    [TestCase("/cart")]
    [TestCase("/CART")]
    [TestCase("/onepagecheckout")]
    [TestCase("/checkout")]
    [TestCase("/en/cart")]
    [TestCase("/en/onepagecheckout/")]
    public void Cart_and_checkout_paths_are_recognised(string path)
    {
        CwPreviewFramingMiddleware.IsCartOrCheckout(new PathString(path)).Should().BeTrue();
    }

    [TestCase("")]
    [TestCase("/")]
    [TestCase("/wishlist")]
    [TestCase("/cart-accessories")]
    [TestCase("/checkoutcompleted")]
    [TestCase("/nab-test-product")]
    public void Other_paths_are_not(string path)
    {
        CwPreviewFramingMiddleware.IsCartOrCheckout(new PathString(path)).Should().BeFalse();
    }

    [Test]
    public void A_path_with_cart_as_any_segment_is_also_treated_as_the_cart()
    {
        //documents current behaviour: any segment matches, not only the first after an optional language code.
        //Only preview requests reach this check, and the result only swaps in the preview customer.
        CwPreviewFramingMiddleware.IsCartOrCheckout(new PathString("/category/cart")).Should().BeTrue();
    }

    #endregion
}
