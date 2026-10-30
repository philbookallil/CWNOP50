using Nop.Core;

namespace Nop.Plugin.CW.Core.Domain.Subsidies;

/// <summary>
/// A compensating movement that gives back what a returned order item consumed. Insert-only, never a
/// mutation of the consumption it reverses — the consumption is a fact about an order and does not
/// change, while availability is a sum over facts.
/// </summary>
/// <remarks>
/// <para>
/// Amounts are stored <b>positive</b> — this row records what was restored. The availability views
/// negate them (CWDatabaseService.SubsidyNetConsumptionSource). Storing negatives would read as an
/// invoice credit and would flip the sign of any ad-hoc query written against this table.
/// </para>
/// <see cref="OrderId"/> and <see cref="OrderItemId"/> are the ORIGINAL order's, not the replacement's.
/// That is load-bearing: the views join Order for the cancelled-order and ordering-period tests, so a
/// restoration is period-scoped and cancellation-scoped to the same order as the consumption it
/// reverses. Cancel that order and both sides drop out together, which is why cancellation still needs
/// no explicit write.
/// </remarks>
public class SubsidyRestoreItem : BaseEntity
{
    /// <summary>Gets or sets the ORIGINAL order the consumption belongs to</summary>
    public int OrderId { get; set; }

    /// <summary>Gets or sets the ORIGINAL order item being returned</summary>
    public int OrderItemId { get; set; }

    public int SubsidyId { get; set; }

    /// <summary>Gets or sets the product group the consumption came from. Null for global credit.</summary>
    public int? ProductGroupId { get; set; }

    /// <summary>Gets or sets the consumption row this reverses, for traceability and for capping</summary>
    public int? SubsidyOrderItemId { get; set; }

    /// <summary>Gets or sets the return case line that caused this restoration</summary>
    public int ReturnCaseLineId { get; set; }

    /// <summary>Gets or sets the nop return request, for the admin trail</summary>
    public int? ReturnRequestId { get; set; }

    /// <summary>
    /// Gets or sets the quantity of the order item this restoration was raised for. Summed across rows
    /// to drive the cumulative target, so repeated partial returns converge on the original allocation
    /// exactly rather than drifting by a rounded cent.
    /// </summary>
    public int ReturnedQuantity { get; set; }

    public int Quantity { get; set; }

    public int QuantityTopup { get; set; }

    public decimal Credit { get; set; }

    public decimal CreditTopup { get; set; }

    public decimal ProductGroupCredit { get; set; }

    public decimal ProductGroupCreditTopup { get; set; }

    public int? CreatedByCustomerId { get; set; }

    public DateTime CreatedOnUtc { get; set; }
}