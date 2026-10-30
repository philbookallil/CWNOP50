using Nop.Core;

namespace Nop.Plugin.CW.Core.Domain.Exchanges;

/// <summary>
/// Represents a return action rule
/// </summary>
public class ReturnActionRule : BaseEntity
{
    /// <summary>
    /// Gets or sets the return action identifier
    /// </summary>
    public int ReturnActionId { get; set; }

    /// <summary>
    /// Gets or sets the store identifier
    /// </summary>
    public int StoreId { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether store credit is allowed
    /// </summary>
    public bool AllowStoreCredit { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether exchange is allowed
    /// </summary>
    public bool AllowExchange { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether repackaging fee is allowed
    /// </summary>
    public bool AllowRepackagingFee { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether shipping fee is allowed
    /// </summary>
    public bool AllowShippingFee { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether item goes back in stock
    /// </summary>
    public bool BackInStock { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this rule is disabled
    /// </summary>
    public bool IsDisabled { get; set; }
}