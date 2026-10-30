namespace Nop.Plugin.CW.Core.Areas.Admin.Models.Orders;

/// <summary>
/// The settlement rows of the admin order-details totals block: what the order came to before subsidy and
/// store credit, and what subsidy covered. Only built when the order has a settlement.
/// </summary>
public record OrderSubsidyCreditModel
{
    /// <summary>Goods and shipping at full price, before subsidy and store credit, in the order's currency.</summary>
    public required string FormattedGrossTotal { get; init; }

    /// <summary>The subsidy that paid for goods, or null when none did.</summary>
    public string FormattedSubsidyForStock { get; init; }

    /// <summary>The subsidy that paid for shipping, or null when none did.</summary>
    public string FormattedSubsidyForShipping { get; init; }
}