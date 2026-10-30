using Nop.Plugin.CW.Core.Domain.Exchanges;
using Nop.Web.Models.Order;

namespace Nop.Plugin.CW.Core.Models.Exchanges;

public record CustomerExchangeRequestModel : CustomerReturnRequestsModel.ReturnRequestModel
{
    #region Ctor

    /// <remarks>nop's record copy constructor copies every value, those nop adds later included</remarks>
    public CustomerExchangeRequestModel(CustomerReturnRequestsModel.ReturnRequestModel returnRequestModel) : base(returnRequestModel)
    {
    }

    #endregion

    #region Properties

    public ExchangeType ExchangeType { get; set; }

    /// <summary>
    /// The exchange type's name, in the customer's language
    /// </summary>
    public string ExchangeTypeName { get; set; }

    /// <summary>
    /// The returned order item
    /// </summary>
    public int OrderItemId { get; set; }

    /// <summary>
    /// The order's number as the customer knows it
    /// </summary>
    public string CustomOrderNumber { get; set; }

    public ExchangeReturnMode ExchangeReturnMode { get; set; }

    public int ExchangeProductId { get; set; }

    public string ExchangeProductName { get; set; }

    public string ExchangeProductSeName { get; set; }

    public string ExchangeProductAttributesInfo { get; set; }

    public string ExchangeProductAttributesXml { get; set; }

    public int ExchangeQuantity { get; set; }

    public string ReceiptMessage { get; set; }

    public string Packname { get; set; }

    public string ExchangeItemSku { get; set; }

    public bool AllowChangeReturnedItem { get; set; }

    /// <summary>
    /// The order the returned item came from
    /// </summary>
    public int OrderId { get; set; }

    /// <summary>
    /// SKU of the item being returned
    /// </summary>
    public string ReturnedItemSku { get; set; }

    /// <summary>
    /// Attribute description of the item being returned
    /// </summary>
    public string ReturnedItemAttributes { get; set; }

    /// <summary>Thumbnail of the returned item, reflecting the attributes actually bought</summary>
    public string ReturnedItemPictureUrl { get; set; }

    /// <summary>Thumbnail of the replacement, reflecting the attributes chosen</summary>
    public string ExchangeProductPictureUrl { get; set; }

    /// <summary>
    /// The replacement's current attribute selections as JSON (attribute id -> value id), used by the
    /// Edit Exchange Item picker to detect whether the customer actually changed anything.
    /// </summary>
    public string CurrentAttributesJson { get; set; }

    #endregion
}