using Nop.Web.Framework.Models;
using Nop.Web.Models.Catalog;

namespace Nop.Plugin.CW.Core.Models.Exchanges;

/// <summary>
/// Stage 2 of the replacement picker: one product, its attribute choices, and the picture that
/// belongs to each choice.
/// </summary>
public record ReplacementProductOptionsModel : BaseNopModel
{
    public ReplacementProductOptionsModel()
    {
        AttributeValuePictures = new Dictionary<int, string>();
        UncollectableRequiredOptions = new List<string>();
    }

    public int OrderItemId { get; set; }

    public ProductDetailsModel Product { get; set; }

    /// <summary>The product's own picture, used until an attribute overrides it</summary>
    public string DefaultPictureUrl { get; set; }

    /// <summary>Attribute value id -> its picture. Only values that have one appear here.</summary>
    public IDictionary<int, string> AttributeValuePictures { get; set; }

    /// <summary>
    /// Names of the options this product requires that the picker cannot put on screen. Non-empty
    /// means the product must not be selectable: the customer is shown these names as the reason
    /// rather than the product being hidden, which would read as the product being unavailable.
    /// </summary>
    public IList<string> UncollectableRequiredOptions { get; set; }
}