using Nop.Core;

namespace Nop.Plugin.CW.Core.Domain.Exchanges;

/// <summary>
/// Represents a return status rule
/// </summary>
public class ReturnStatusRule : BaseEntity
{
    /// <summary>
    /// Gets or sets the return status identifier
    /// </summary>
    public int ReturnStatusId { get; set; }

    /// <summary>
    /// Gets or sets the store identifier
    /// </summary>
    public int StoreId { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to perform store credit
    /// </summary>
    public bool PerformStoreCredit { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to perform exchange
    /// </summary>
    public bool PerformExchange { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether item goes back in stock
    /// </summary>
    public bool BackInStock { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the return is authorized
    /// </summary>
    public bool Authorised { get; set; }
}
