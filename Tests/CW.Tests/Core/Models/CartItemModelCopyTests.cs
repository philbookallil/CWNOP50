using System.Reflection;
using AwesomeAssertions;
using Nop.Plugin.CW.Core.Models.PackNameCheckout;
using Nop.Plugin.CW.Core.Models.ShoppingCart;
using Nop.Web.Models.ShoppingCart;
using NUnit.Framework;

namespace CW.Tests.Core.Models;

/// <summary>
/// CW's cart lines are built from nop's: a line on an open return request, or a packname's line, starts as a copy of
/// nop's, so every value nop put on its line has to arrive on CW's.
/// </summary>
[TestFixture]
public class CartItemModelCopyTests
{
    /// <summary>
    /// A value of every simple settable property, each one different, so a property left behind shows as a default
    /// </summary>
    private static T Filled<T>() where T : new()
    {
        var model = new T();
        var index = 1;

        foreach (var property in typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(property => property.CanWrite))
        {
            var type = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
            object? value = type == typeof(string) ? $"value {index}"
                : type == typeof(int) ? index
                : type == typeof(decimal) ? index + 0.5m
                : type == typeof(bool) ? true
                : null;

            if (value != null)
                property.SetValue(model, value);
            index++;
        }

        return model;
    }

    [Test]
    public void A_cart_line_copy_keeps_every_value_of_nops_line()
    {
        var nopLine = Filled<ShoppingCartModel.ShoppingCartItemModel>();
        nopLine.Warnings.Add("a warning");
        nopLine.CustomProperties["key"] = "custom";

        var copy = new CWCartItemModel(nopLine);

        copy.Should().BeEquivalentTo(nopLine);
        copy.VendorId.Should().Be(nopLine.VendorId).And.NotBe(0);
    }

    [Test]
    public void A_packname_cart_line_copy_keeps_every_value_of_nops_line()
    {
        var nopLine = Filled<ShoppingCartModel.ShoppingCartItemModel>();

        var copy = new PackNameShoppingCartModel.PackNameShoppingCartItemModel(nopLine);

        copy.Should().BeEquivalentTo(nopLine);
    }

    [Test]
    public void The_order_details_copy_keeps_every_value_nop_prepared()
    {
        //5.00 added DesiredDeliveryDate and ReturnRequestText to the order page: they come across with the rest
        var nopModel = Filled<Nop.Web.Models.Order.OrderDetailsModel>();

        var copy = new Nop.Plugin.CW.Core.Models.Orders.CWOrderDetailsModel(nopModel);

        copy.Should().BeEquivalentTo(nopModel);
        copy.ReturnRequestText.Should().Be(nopModel.ReturnRequestText).And.NotBeNull();
        copy.DesiredDeliveryDate.Should().Be(nopModel.DesiredDeliveryDate).And.NotBeNull();
    }

    [Test]
    public void A_mini_cart_line_copy_keeps_every_value_of_nops_line()
    {
        var nopLine = Filled<MiniShoppingCartModel.ShoppingCartItemModel>();

        var copy = new CWMiniCartItemModel(nopLine);

        copy.Should().BeEquivalentTo(nopLine);
        copy.UnitPriceValue.Should().Be(nopLine.UnitPriceValue).And.NotBe(0);
    }
}
