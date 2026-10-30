namespace Nop.Plugin.CW.Core.Domain.Subsidies.DTOs;

/// <summary>
/// What the return workflow hands the subsidy service in order to give back one returned item's
/// consumption. Deliberately carries no money figure: a money-denominated restore cannot express
/// restoration in kind, and in kind is what the business requires — units restore units, a product
/// group's credit restores to that group, global credit restores to global.
/// </summary>
public record SubsidyRestoreRequest
{
    /// <summary>The ORIGINAL order item being returned.</summary>
    public required int OrderItemId { get; init; }

    /// <summary>The quantity ordered on that order item — the denominator for a partial return.</summary>
    public required int OrderedQuantity { get; init; }

    /// <summary>The quantity the customer is returning.</summary>
    public required int ReturnedQuantity { get; init; }

    /// <summary>The case line being executed. Restoration is keyed on this.</summary>
    public required int ReturnCaseLineId { get; init; }

    public int? ReturnRequestId { get; init; }

    public int? ActingCustomerId { get; init; }
}