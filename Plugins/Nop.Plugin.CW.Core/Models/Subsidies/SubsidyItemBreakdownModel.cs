namespace Nop.Plugin.CW.Core.Models.Subsidies;

/// <summary>
/// A single subsidy source row shown under a cart item's subtotal.
/// One row per distinct product group (or one row for global credit).
/// </summary>
public record SubsidyItemBreakdownModel
{
    /// <summary>Null means this row represents global credit, not a product group.</summary>
    public int? ProductGroupId { get; init; }

    public string? ProductGroupName { get; init; }

    public string? ProductGroupIcon { get; init; }

    /// <summary>Hex colour, e.g. "#e91e63".</summary>
    public string? ProductGroupColour { get; init; }

    /// <summary>Total units covered by PG quantity allocation (Quantity + QuantityTopup). Zero if none.</summary>
    public int QuantityUsed { get; init; }

    /// <summary>
    /// Total credit value covered, incl. tax.
    /// For PG rows: ProductGroupCredit + ProductGroupCreditTopup.
    /// For global rows: Credit + CreditTopup.
    /// </summary>
    public decimal CreditUsed { get; init; }

    /// <summary>Pre-formatted credit string for display, e.g. "−$12.50". Null if CreditUsed == 0.</summary>
    public string? CreditDisplay { get; init; }
}