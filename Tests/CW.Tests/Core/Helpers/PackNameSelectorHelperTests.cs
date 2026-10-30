using AwesomeAssertions;
using Nop.Plugin.CW.Core.Domain.PackNames.DTOs;
using Nop.Plugin.CW.Core.Helpers;
using NUnit.Framework;

namespace CW.Tests.Core.Helpers;

/// <summary>
/// Which packnames the storefront selector receives: one page by name, with the selected packname and the ones with cart
/// lines always on the first page, and the matches of a search.
/// </summary>
[TestFixture]
public class PackNameSelectorHelperTests
{
    #region Utilities

    private static List<PackNameDTO> Roster(int count)
    {
        //names out of order on purpose: the page is ordered by name, not by the roster's order
        return Enumerable.Range(1, count)
            .Select(id => new PackNameDTO { Id = id, Name = $"Employee {count + 1 - id:000}", EmployeeNumber = $"E{id:0000}" })
            .ToList();
    }

    private static readonly Dictionary<int, int> NoCart = new();

    #endregion

    [Test]
    public void A_short_roster_is_one_page_with_nothing_more()
    {
        var (page, hasMore) = PackNameSelectorHelper.SelectPage(Roster(9), null, 0, NoCart, searchEmployeeNumber: false);

        page.Should().HaveCount(9);
        hasMore.Should().BeFalse();
    }

    [Test]
    public void The_first_page_is_the_first_names_in_order()
    {
        var (page, hasMore) = PackNameSelectorHelper.SelectPage(Roster(120), "", 0, NoCart, false, pageSize: 50);

        page.Should().HaveCount(50);
        page.Select(packName => packName.Name).Should().BeInAscendingOrder();
        page.First().Name.Should().Be("Employee 001");
        hasMore.Should().BeTrue();
    }

    [Test]
    public void The_selected_packname_is_on_the_first_page_wherever_it_falls()
    {
        //id 1 is named Employee 120, the last name of the roster
        var (page, hasMore) = PackNameSelectorHelper.SelectPage(Roster(120), null, 1, NoCart, false, pageSize: 50);

        page.Should().HaveCount(51);
        page.Should().Contain(packName => packName.Id == 1);
        page.Last().Id.Should().Be(1, "the page stays ordered by name");
        hasMore.Should().BeTrue();
    }

    [Test]
    public void Packnames_with_cart_lines_are_on_the_first_page()
    {
        var cart = new Dictionary<int, int> { [2] = 3, [3] = 0 };

        var (page, _) = PackNameSelectorHelper.SelectPage(Roster(120), null, 0, cart, false, pageSize: 50);

        page.Should().Contain(packName => packName.Id == 2);
        page.Should().NotContain(packName => packName.Id == 3, "a quantity of 0 is no cart line");
        page.Should().HaveCount(51);
    }

    [Test]
    public void Nothing_more_when_the_pinned_packnames_complete_the_roster()
    {
        var (page, hasMore) = PackNameSelectorHelper.SelectPage(Roster(51), null, 1, NoCart, false, pageSize: 50);

        page.Should().HaveCount(51);
        hasMore.Should().BeFalse();
    }

    [Test]
    public void A_search_matches_names_ignoring_case()
    {
        var roster = new List<PackNameDTO>
        {
            new() { Id = 1, Name = "Anna Smith" },
            new() { Id = 2, Name = "Bob Jones" },
            new() { Id = 3, Name = "ANNABEL Lee" }
        };

        var (page, hasMore) = PackNameSelectorHelper.SelectPage(roster, " anna ", 2, NoCart, false);

        page.Select(packName => packName.Id).Should().Equal(1, 3);
        hasMore.Should().BeFalse();
    }

    [Test]
    public void A_search_does_not_add_the_selected_packname()
    {
        var (page, _) = PackNameSelectorHelper.SelectPage(Roster(10), "Employee 003", 1, new Dictionary<int, int> { [1] = 2 }, false);

        page.Select(packName => packName.Name).Should().Equal("Employee 003");
    }

    [Test]
    public void A_search_says_when_more_match_than_a_page()
    {
        var (page, hasMore) = PackNameSelectorHelper.SelectPage(Roster(120), "employee", 0, NoCart, false, pageSize: 50);

        page.Should().HaveCount(50);
        hasMore.Should().BeTrue();
    }

    [Test]
    public void Employee_numbers_match_only_when_the_store_shows_them()
    {
        PackNameSelectorHelper.SelectPage(Roster(20), "E0007", 0, NoCart, searchEmployeeNumber: true).Page
            .Select(packName => packName.Id).Should().Equal(7);

        PackNameSelectorHelper.SelectPage(Roster(20), "E0007", 0, NoCart, searchEmployeeNumber: false).Page
            .Should().BeEmpty();
    }

    [Test]
    public void The_roster_is_not_changed()
    {
        var roster = Roster(60);
        var before = roster.Select(packName => packName.Id).ToList();

        PackNameSelectorHelper.SelectPage(roster, null, 0, NoCart, false, pageSize: 50);

        roster.Select(packName => packName.Id).Should().Equal(before);
    }
}
