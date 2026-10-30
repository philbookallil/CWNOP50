using Nop.Core;

namespace Nop.Plugin.CW.Core.Domain.Exchanges;

/// <summary>
/// Represents the relationship between a return reason and return action
/// </summary>
public class ReasonActionRelationship : BaseEntity
{
    /// <summary>
    /// Gets or sets the reason identifier
    /// </summary>
    public int ReasonId { get; set; }

    /// <summary>
    /// Gets or sets the action identifier
    /// </summary>
    public int ActionId { get; set; }

    /// <summary>
    /// Gets or sets the store identifier
    /// </summary>
    public int StoreId { get; set; }
}