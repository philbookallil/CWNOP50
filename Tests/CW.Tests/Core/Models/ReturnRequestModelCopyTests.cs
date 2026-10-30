using System.Reflection;
using AwesomeAssertions;
using Nop.Plugin.CW.Core.Models.Exchanges;
using Nop.Plugin.CW.Core.Models.ReturnRequests;
using Nop.Web.Models.Order;
using NUnit.Framework;

namespace CW.Tests.Core.Models;

/// <summary>
/// CW's return page and return list models start as a copy of nop's, so every value nop put on its model, those nop
/// 5.00 added included (the withdrawal token, the page's titles, the reason and action switches), arrives on CW's
/// </summary>
[TestFixture]
public class ReturnRequestModelCopyTests
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
                : type == typeof(bool) ? true
                : type == typeof(Guid) ? Guid.NewGuid()
                : type == typeof(DateTime) ? new DateTime(2026, 1, 1).AddDays(index)
                : null;

            if (value != null)
                property.SetValue(model, value);
            index++;
        }

        return model;
    }

    [Test]
    public void The_return_page_copy_keeps_every_value_of_nops_page()
    {
        var nopModel = Filled<SubmitReturnRequestModel>();
        nopModel.Items.Add(new SubmitReturnRequestModel.OrderItemModel { Id = 3 });

        var copy = new CWSubmitReturnRequestModel(nopModel);

        copy.Should().BeEquivalentTo(nopModel);
        copy.WithdrawalToken.Should().Be(nopModel.WithdrawalToken).And.NotBeNullOrEmpty();
        copy.ReturnRequestTitle.Should().Be(nopModel.ReturnRequestTitle).And.NotBeNullOrEmpty();
    }

    [Test]
    public void The_return_page_line_copy_keeps_every_value_of_nops_line()
    {
        var nopLine = Filled<SubmitReturnRequestModel.OrderItemModel>();

        var copy = new CWSubmitReturnRequestModel.CWOrderItemModel(nopLine);

        copy.Should().BeEquivalentTo(nopLine);
    }

    [Test]
    public void The_return_list_copy_keeps_every_value_of_nops_line()
    {
        var nopLine = Filled<CustomerReturnRequestsModel.ReturnRequestModel>();

        var copy = new CustomerExchangeRequestModel(nopLine);

        copy.Should().BeEquivalentTo(nopLine);
    }
}
