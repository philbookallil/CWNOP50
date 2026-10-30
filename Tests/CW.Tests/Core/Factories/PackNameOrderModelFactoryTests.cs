using System.Reflection;
using System.Runtime.CompilerServices;
using AwesomeAssertions;
using Nop.Core.Domain.Catalog;
using Nop.Core.Domain.Orders;
using Nop.Plugin.CW.Core.Domain.Orders.DTOs;
using Nop.Plugin.CW.Core.Domain.Orders.Views;
using Nop.Plugin.CW.Core.Domain.PackNames;
using Nop.Plugin.CW.Core.Domain.Subsidies;
using Nop.Plugin.CW.Core.Factories;
using Nop.Plugin.CW.Core.Models.Orders;
using NUnit.Framework;

namespace CW.Tests.Core.Factories;

/// <summary>
/// The storefront order page's packname groups
/// </summary>
[TestFixture]
public class PackNameOrderModelFactoryTests
{
    [Test]
    public async Task A_line_whose_product_is_gone_is_left_out_of_its_group()
    {
        var factory = (PackNameOrderModelFactory)RuntimeHelpers.GetUninitializedObject(typeof(PackNameOrderModelFactory));
        var group = new PackNameOrderGroupDTO { PackNameId = 1, PackName = "Ann", Items = [new PackNameFullOrderItem { Id = 11, ProductId = 99 }] };
        var method = typeof(PackNameOrderModelFactory).GetMethod("PrepareGroupModelAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;

        var model = await (Task<PackNameOrderGroupModel>)method.Invoke(factory,
        [
            group, new Order(), 1, false, new Dictionary<int, Product>(), new Dictionary<int, PackName>(),
            Array.Empty<SubsidyOrderItem>().ToLookup(item => item.OrderItemId), new Dictionary<int, ProductGroup>()
        ])!;

        model.Items.Should().BeEmpty("the view would fail on a missing line");
        model.PackName.Should().Be("Ann");
    }
}
