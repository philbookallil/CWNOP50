using Nop.Core;

namespace Nop.Plugin.CW.Core.Domain.Exchanges;

/// <summary>
/// Represents a fee rule for exchanges/returns
/// </summary>
public class FeeRule : BaseEntity
{
    /// <summary>
    /// Gets or sets the store identifier
    /// </summary>
    public int StoreId { get; set; }

    /// <summary>
    /// Gets or sets the fee type identifier
    /// </summary>
    public int FeeTypeId { get; set; }

    /// <summary>
    /// Gets or sets the fee type
    /// </summary>
    public FeeType FeeType
    {
        get
        {
            return (FeeType)this.FeeTypeId;
        }
        set
        {
            this.FeeTypeId = (int)value;
        }
    }

    /// <summary>
    /// Gets or sets the fee amount
    /// </summary>
    public decimal Amount { get; set; }

    /// <summary>
    /// Gets or sets the fee rule operator identifier
    /// </summary>
    public int OperatorId { get; set; }

    /// <summary>
    /// Gets or sets the fee rule operator
    /// </summary>
    public FeeRuleOperator Operator
    {
        get
        {
            return (FeeRuleOperator)this.OperatorId;
        }
        set
        {
            this.OperatorId = (int)value;
        }
    }

    /// <summary>
    /// Gets or sets the units quantity
    /// </summary>
    public int UnitsQuantity { get; set; }
}