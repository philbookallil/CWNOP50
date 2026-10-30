using Nop.Core;

namespace Nop.Plugin.CW.Core.Domain.Exchanges;

/// <summary>
/// Represents a return replacement item
/// </summary>
public class ReturnReplacementItem : BaseEntity
{
    /// <summary>
    /// Gets or sets the attributes information (formatted)
    /// </summary>
    public string AttributesInfo { get; set; }

    /// <summary>
    /// Gets or sets the attributes XML
    /// </summary>
    public string AttributesXml { get; set; }

    /// <summary>
    /// Gets or sets the product identifier
    /// </summary>
    public int ProductId { get; set; }

    /// <summary>
    /// Gets or sets the product name
    /// </summary>
    public string ProductName { get; set; }

    /// <summary>
    /// Gets or sets the product SEO friendly name
    /// </summary>
    public string ProductSeName { get; set; }

    /// <summary>
    /// Gets or sets the quantity
    /// </summary>
    public int Quantity { get; set; }

    /// <summary>
    /// Gets or sets the unit price
    /// </summary>
    public decimal UnitPrice { get; set; }

    /// <summary>
    /// Gets or sets the return request item
    /// </summary>
    public virtual ReturnRequestItem ReturnRequestItem { get; set; }

    /// <summary>
    /// Gets or sets the return request item identifier
    /// </summary>
    public int ReturnRequestItemId { get; set; }
}