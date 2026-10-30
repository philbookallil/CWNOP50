using AwesomeAssertions;
using Nop.Plugin.CW.Core.Domain.Exchanges;
using Nop.Plugin.CW.Core.Helpers;
using NUnit.Framework;

namespace CW.Tests.Core.Helpers;

/// <summary>
/// What a packname's subsidies let it see, and how that restriction reaches nop's cache keys.
/// </summary>
/// <remarks>
/// <para>
/// <b>One rule for every reader.</b> Before it, five product lists applied the allocation and every other path —
/// the product page, related products, the menu, add-to-cart — ignored it.
/// </para>
/// <para>
/// <b>Exchanges are judged differently.</b> The same product is never restricted; another product must be allocated;
/// a closed ordering period never blocks a replacement (Eduardo, 2026-10-01).
/// </para>
/// </remarks>
[TestFixture]
public class PackNameCatalogGateHelperTests
{
    #region Utilities

    private static readonly int[] Allocated = [101, 102, 103];

    #endregion

    #region Visible products

    [Test]
    public void Open_period_without_restriction_sees_everything()
    {
        PackNameCatalogGateHelper.VisibleProductIds(canOrder: true, allocatedProductIds: null, replacement: null)
            .Should().BeNull();
    }

    [Test]
    public void Open_period_restricted_sees_the_allocated_products()
    {
        PackNameCatalogGateHelper.VisibleProductIds(true, Allocated, null)
            .Should().Equal(Allocated);
    }

    [Test]
    public void Closed_period_sees_nothing_even_without_restriction()
    {
        PackNameCatalogGateHelper.VisibleProductIds(false, null, null)
            .Should().NotBeNull().And.BeEmpty();
    }

    [Test]
    public void Closed_period_sees_nothing_even_with_allocated_products()
    {
        PackNameCatalogGateHelper.VisibleProductIds(false, Allocated, null)
            .Should().BeEmpty();
    }

    [TestCase(ExchangeType.Exact)]
    [TestCase(ExchangeType.SameStyle)]
    public void Same_product_replacement_is_never_restricted(ExchangeType replacement)
    {
        PackNameCatalogGateHelper.VisibleProductIds(false, Allocated, replacement)
            .Should().BeNull();
    }

    [Test]
    public void Different_style_replacement_follows_the_allocation_even_when_the_period_is_closed()
    {
        PackNameCatalogGateHelper.VisibleProductIds(false, Allocated, ExchangeType.DifferentStyle)
            .Should().Equal(Allocated);
    }

    [Test]
    public void Different_style_replacement_without_restriction_sees_everything()
    {
        PackNameCatalogGateHelper.VisibleProductIds(false, null, ExchangeType.DifferentStyle)
            .Should().BeNull();
    }

    #endregion

    #region Cache key

    [Test]
    public void Fingerprint_ignores_order_and_duplicates()
    {
        PackNameCatalogGateHelper.Fingerprint([3, 1, 2, 2])
            .Should().Be(PackNameCatalogGateHelper.Fingerprint([1, 2, 3]));
    }

    [Test]
    public void Fingerprint_changes_when_one_id_changes()
    {
        PackNameCatalogGateHelper.Fingerprint([1, 2, 3])
            .Should().NotBe(PackNameCatalogGateHelper.Fingerprint([1, 2, 4]));
    }

    [Test]
    public void No_restriction_has_no_marker()
    {
        PackNameCatalogGateHelper.RestrictionMarker(null).Should().BeNull();
    }

    [Test]
    public void Empty_and_allocated_restrictions_have_different_markers()
    {
        PackNameCatalogGateHelper.RestrictionMarker([])
            .Should().NotBe(PackNameCatalogGateHelper.RestrictionMarker(Allocated));
    }

    [Test]
    public void Marker_lies_far_below_any_negated_role_id()
    {
        foreach (var ids in new[] { Array.Empty<int>(), Allocated, new[] { 7 }, Enumerable.Range(1, 500).ToArray() })
            PackNameCatalogGateHelper.RestrictionMarker(ids)
                .Should().BeInRange(int.MinValue, int.MinValue + 999_999_999);
    }

    [Test]
    public void Marker_is_the_same_for_the_same_set()
    {
        PackNameCatalogGateHelper.RestrictionMarker([103, 101, 102])
            .Should().Be(PackNameCatalogGateHelper.RestrictionMarker(Allocated));
    }

    #endregion

    #region Grouped products

    //Grouped parent 13 with associated 14 and 15; parent 30 with associated 31.
    private static readonly (int, int)[] Relations = [(14, 13), (15, 13), (31, 30)];

    [Test]
    public void An_allocated_parent_brings_its_associated_products()
    {
        PackNameCatalogGateHelper.WithGroupedRelatives([13], Relations)
            .Should().Equal(13, 14, 15);
    }

    [Test]
    public void An_allocated_associated_product_brings_its_parent_only()
    {
        PackNameCatalogGateHelper.WithGroupedRelatives([14], Relations)
            .Should().Equal(13, 14);
    }

    [Test]
    public void Unrelated_groups_stay_out()
    {
        PackNameCatalogGateHelper.WithGroupedRelatives([14, 101], Relations)
            .Should().NotContain([30, 31, 15]);
    }

    [Test]
    public void Products_outside_any_group_are_kept()
    {
        PackNameCatalogGateHelper.WithGroupedRelatives([101, 102], Relations)
            .Should().Equal(101, 102);
    }

    #endregion

    #region Categories

    private static readonly IReadOnlyDictionary<int, int> Tree = new Dictionary<int, int>
    {
        [1] = 0,  // Apparel
        [2] = 1,  // Apparel > Casual
        [3] = 2,  // Apparel > Casual > Shirts
        [4] = 0,  // Electronics
        [5] = 4   // Electronics > Phones
    };

    [Test]
    public void A_category_brings_its_ancestors()
    {
        PackNameCatalogGateHelper.WithAncestors([3], Tree)
            .Should().BeEquivalentTo([1, 2, 3]);
    }

    [Test]
    public void Branches_without_a_visible_product_stay_hidden()
    {
        PackNameCatalogGateHelper.WithAncestors([3], Tree)
            .Should().NotContain([4, 5]);
    }

    [Test]
    public void Several_categories_merge()
    {
        PackNameCatalogGateHelper.WithAncestors([3, 5], Tree)
            .Should().BeEquivalentTo([1, 2, 3, 4, 5]);
    }

    [Test]
    public void A_parent_loop_does_not_hang()
    {
        var loop = new Dictionary<int, int> { [1] = 2, [2] = 1 };

        PackNameCatalogGateHelper.WithAncestors([1], loop)
            .Should().BeEquivalentTo([1, 2]);
    }

    [Test]
    public void A_missing_parent_ends_the_walk()
    {
        PackNameCatalogGateHelper.WithAncestors([9], Tree)
            .Should().BeEquivalentTo([9]);
    }

    #endregion
}
