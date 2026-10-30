using Nop.Core;

namespace Nop.Plugin.CW.Core.Domain.Exchanges;

/// <summary>
/// Represents a return request item (extended attributes for ReturnRequest from NOP)
/// </summary>
public class ReturnRequestItem : BaseEntity
{
    /// <summary>
    /// Gets or sets the return request identifier (FK to NOP ReturnRequest table)
    /// </summary>
    public int ReturnRequestId { get; set; }

    /// <summary>
    /// Gets or sets the exchange mode identifier
    /// </summary>
    public int ExchangeModeId { get; set; }

    /// <summary>
    /// Gets or sets the exchange type identifier
    /// </summary>
    public int ExchangeTypeId { get; set; }

    /// <summary>
    /// Gets or sets the return reason identifier (references NOP ReturnRequestReason).
    /// Always set at creation; may reference a reason that was deleted afterwards.
    /// </summary>
    public int ReturnReasonId { get; set; }

    /// <summary>
    /// Gets or sets the return action identifier (references NOP ReturnRequestAction).
    /// Always set at creation; may reference an action that was deleted afterwards.
    /// </summary>
    public int ReturnActionId { get; set; }

    /// <summary>
    /// Gets or sets the staff ship quantity (qty of items to be shipped after staff review)
    /// </summary>
    public int? StaffShipQty { get; set; }

    /// <summary>
    /// Gets or sets the staff repack quantity (qty of items to be repackaged after staff review)
    /// </summary>
    public int? StaffRepackQty { get; set; }

    /// <summary>
    /// Gets or sets the return request group identifier (groups returned items from same batch)
    /// </summary>
    public Guid? ReturnRequestGroup { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the return request needs completion after authorization
    /// </summary>
    public bool NeedsCompletion { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the return request is completed
    /// </summary>
    public bool IsCompleted { get; set; }

    /// <summary>
    /// Gets or sets the shopping cart item identifier (for exchanged items cart)
    /// </summary>
    public int? ShoppingCartItemId { get; set; }

    /// <summary>
    /// Gets or sets the order item identifier (for completed exchange items)
    /// </summary>
    public int? OrderItemId { get; set; }

    /// <summary>
    /// Gets or sets the returned order item identifier (the item being returned)
    /// </summary>
    public int ReturnedOrderItemId { get; set; }

    /// <summary>
    /// Gets or sets the name of the additional fee
    /// </summary>
    public string AdditionalFeeName { get; set; }

    /// <summary>
    /// Gets or sets the amount of the additional fee
    /// </summary>
    public decimal? AdditionalFeeAmount { get; set; }

    #region Custom properties

    /// <summary>
    /// Gets or sets the exchange mode
    /// </summary>
    public ExchangeReturnMode ExchangeMode
    {
        get
        {
            return (ExchangeReturnMode)this.ExchangeModeId;
        }
        set
        {
            this.ExchangeModeId = (int)value;
        }
    }

    /// <summary>
    /// Gets or sets the exchange type
    /// </summary>
    public ExchangeType ExchangeType
    {
        get
        {
            return (ExchangeType)this.ExchangeTypeId;
        }
        set
        {
            this.ExchangeTypeId = (int)value;
        }
    }

    #endregion 
}
