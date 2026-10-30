using Nop.Core;
using Nop.Plugin.CW.Core.Domain.Exchanges;

namespace Nop.Plugin.CW.Core.Domain.Returns;

/// <summary>
/// One returned item within a <see cref="ReturnCase"/> — what is coming back, what the customer
/// wants in its place, and whether staff accepted it.
/// </summary>
public class ReturnCaseLine : BaseEntity
{
    /// <summary>
    /// Gets or sets the parent return case
    /// </summary>
    public int ReturnCaseId { get; set; }

    /// <summary>
    /// Gets or sets the order item being returned
    /// </summary>
    public int OrderItemId { get; set; }

    /// <summary>
    /// Gets or sets the quantity the customer is returning
    /// </summary>
    public int Quantity { get; set; }

    /// <summary>
    /// Gets or sets the requested outcome identifier (exchange or credit note)
    /// </summary>
    public int ModeId { get; set; }

    /// <summary>
    /// Gets or sets the requested outcome
    /// </summary>
    public ExchangeReturnMode Mode
    {
        get => (ExchangeReturnMode)ModeId;
        set => ModeId = (int)value;
    }

    /// <summary>
    /// Gets or sets the exchange type identifier (exact, same style, different style)
    /// </summary>
    public int ExchangeTypeId { get; set; }

    /// <summary>
    /// Gets or sets the exchange type
    /// </summary>
    public ExchangeType ExchangeType
    {
        get => (ExchangeType)ExchangeTypeId;
        set => ExchangeTypeId = (int)value;
    }

    /// <summary>
    /// Gets or sets the replacement product identifier. Null when the customer asked for credit.
    /// </summary>
    public int? ReplacementProductId { get; set; }

    /// <summary>
    /// Gets or sets the selected attributes of the replacement product, in XML
    /// </summary>
    public string ReplacementAttributesXml { get; set; }

    /// <summary>
    /// Gets or sets the human-readable description of the replacement product's attributes
    /// </summary>
    public string ReplacementAttributesInfo { get; set; }

    /// <summary>
    /// Gets or sets the replacement quantity
    /// </summary>
    public int? ReplacementQuantity { get; set; }

    /// <summary>
    /// Gets or sets the disposition identifier
    /// </summary>
    public int DispositionId { get; set; }

    /// <summary>
    /// Gets or sets the disposition — whether staff accepted or rejected this item
    /// </summary>
    public ReturnLineDisposition Disposition
    {
        get => (ReturnLineDisposition)DispositionId;
        set => DispositionId = (int)value;
    }

    /// <summary>
    /// Gets or sets the staff note explaining the disposition, shown when an item is rejected
    /// </summary>
    public string DispositionReason { get; set; }

    /// <summary>
    /// Gets or sets when the disposition was set (UTC)
    /// </summary>
    public DateTime? DispositionOnUtc { get; set; }

    /// <summary>
    /// Gets or sets which staff member set the disposition
    /// </summary>
    public int? DispositionByCustomerId { get; set; }

    /// <summary>
    /// Gets or sets the quantity used to calculate the redelivery fee, when staff override it
    /// </summary>
    public int? StaffShipQty { get; set; }

    /// <summary>
    /// Gets or sets the quantity used to calculate the repackaging fee, when staff override it
    /// </summary>
    public int? StaffRepackQty { get; set; }

    /// <summary>
    /// Gets or sets the NOP return request this line is mirrored to. NOP's own returns system is
    /// kept as a satellite so its grid, custom numbers and message templates keep working.
    /// </summary>
    public int? ReturnRequestId { get; set; }
}