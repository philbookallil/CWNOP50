namespace Nop.Plugin.CW.Core.Domain.Subsidies.DTOs;

/// <summary>
/// The result of one pass of the subsidy allocation over a cart. The same record serves display and
/// order placement — only the caller decides whether the rows are persisted.
/// </summary>
public record SubsidyAllocation
{
    /// <summary>An allocation with nothing in it. A fresh instance each time — the collections below
    /// are mutable, and a shared singleton would be a footgun.</summary>
    public static SubsidyAllocation Empty => new();

    /// <summary>
    /// Per-item consumption. <c>SubsidyOrderItem.Id</c> carries the <b>ShoppingCartItemId</b>, not a
    /// primary key — these are projections until a caller copies them onto real order items.
    /// </summary>
    public IList<SubsidyOrderItem> Items { get; init; } = [];

    /// <summary>Per-packname shipping consumption. Keyed by <c>PackNameId</c>.</summary>
    public IList<SubsidyOrderShipping> ShippingCredits { get; init; } = [];

    /// <summary>Money subsidised against goods, incl tax.</summary>
    public decimal StockInclTax { get; init; }

    /// <summary>Money subsidised against shipping, incl tax.</summary>
    public decimal ShippingInclTax { get; init; }
}