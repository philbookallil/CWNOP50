using AwesomeAssertions;
using Nop.Plugin.CW.Core.Domain.Exchanges;
using Nop.Plugin.CW.Core.Domain.PackNames;
using Nop.Plugin.CW.Core.Services.PackNames;
using NUnit.Framework;

namespace CW.Tests.Core.Services.PackNames;

/// <summary>
/// The window in which a cart line is checked against its own packname's catalogue.
/// </summary>
/// <remarks>
/// A window left open would make every later role lookup on the same logical call see that packname's
/// catalogue, and one that blinds an outer window would check a required product against the wrong packname.
/// </remarks>
[TestFixture]
public class PackNameCatalogScopeTests
{
    [Test]
    public void Outside_a_window_there_is_no_packname()
    {
        PackNameCatalogScope.Current.Should().BeNull();
    }

    [Test]
    public void Inside_a_window_its_packname_is_current_and_closing_clears_it()
    {
        var packName = new PackName { Id = 20 };

        using (PackNameCatalogScope.Begin(packName))
            PackNameCatalogScope.Current.Should().BeSameAs(packName);

        PackNameCatalogScope.Current.Should().BeNull();
    }

    [Test]
    public void Closing_an_inner_window_restores_the_outer_one()
    {
        var outer = new PackName { Id = 20 };
        var inner = new PackName { Id = 24 };

        using (PackNameCatalogScope.Begin(outer))
        {
            using (PackNameCatalogScope.Begin(inner))
                PackNameCatalogScope.Current.Should().BeSameAs(inner);

            PackNameCatalogScope.Current.Should().BeSameAs(outer);
        }

        PackNameCatalogScope.Current.Should().BeNull();
    }

    [Test]
    public async Task The_window_survives_an_await()
    {
        var packName = new PackName { Id = 20 };

        using (PackNameCatalogScope.Begin(packName))
        {
            await Task.Yield();

            PackNameCatalogScope.Current.Should().BeSameAs(packName);
        }
    }

    [Test]
    public async Task A_window_opened_inside_an_awaited_method_does_not_reach_the_caller()
    {
        //Why the cart and the exchange picker look the packname up in a helper and open the window themselves:
        //an async method runs on a copy of the caller's context, so the window it opens is undone on return.
        static async Task<IDisposable> openInsideHelperAsync(PackName packName)
        {
            await Task.Yield();

            return PackNameCatalogScope.Begin(packName);
        }

        using (await openInsideHelperAsync(new PackName { Id = 20 }))
            PackNameCatalogScope.Current.Should().BeNull();
    }

    [Test]
    public void A_purchase_window_has_no_replacement()
    {
        using (PackNameCatalogScope.Begin(new PackName { Id = 20 }))
            PackNameCatalogScope.CurrentReplacement.Should().BeNull();
    }

    [Test]
    public void A_replacement_window_carries_its_exchange_type_and_closing_clears_it()
    {
        using (PackNameCatalogScope.Begin(new PackName { Id = 20 }, ExchangeType.DifferentStyle))
            PackNameCatalogScope.CurrentReplacement.Should().Be(ExchangeType.DifferentStyle);

        PackNameCatalogScope.CurrentReplacement.Should().BeNull();
    }

    [Test]
    public void Closing_an_inner_purchase_window_restores_the_outer_replacement()
    {
        using (PackNameCatalogScope.Begin(new PackName { Id = 20 }, ExchangeType.Exact))
        {
            using (PackNameCatalogScope.Begin(new PackName { Id = 24 }))
                PackNameCatalogScope.CurrentReplacement.Should().BeNull();

            PackNameCatalogScope.CurrentReplacement.Should().Be(ExchangeType.Exact);
        }
    }

    [Test]
    public void A_null_packname_is_refused()
    {
        var begin = () => PackNameCatalogScope.Begin(null);

        begin.Should().Throw<ArgumentNullException>();
    }
}
