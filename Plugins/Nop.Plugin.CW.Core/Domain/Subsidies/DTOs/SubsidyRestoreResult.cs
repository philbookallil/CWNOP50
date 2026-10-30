namespace Nop.Plugin.CW.Core.Domain.Subsidies.DTOs;

/// <summary>
/// What one call to ISubsidyCreditService.RestoreSubsidyForReturnedItemAsync gave back. The money
/// equivalent is what the credit-note side needs: a returned line's value minus this figure is the money
/// the customer actually put in, and that — only that — comes back as a credit note.
/// </summary>
public record SubsidyRestoreResult
{
    /// <summary>Nothing was restored — no consumption, no case line, or nothing left to give back.</summary>
    public static SubsidyRestoreResult Empty => new();

    /// <summary>Compensating rows written by this call.</summary>
    public int RowsWritten { get; init; }

    /// <summary>Product-group units (base + topup) given back by this call.</summary>
    public int UnitsRestored { get; init; }

    /// <summary>Money (all four credit buckets) given back by this call, incl tax.</summary>
    public decimal CreditRestored { get; init; }

    /// <summary>
    /// <see cref="CreditRestored"/> plus <see cref="UnitsRestored"/> valued at the order item's unit
    /// price incl tax — the same valuation the order screens use for a unit the order consumed.
    /// </summary>
    public decimal MoneyEquivalent { get; init; }

    /// <summary>True when this call put anything back at all.</summary>
    public bool RestoredAnything => UnitsRestored > 0 || CreditRestored > decimal.Zero;
}