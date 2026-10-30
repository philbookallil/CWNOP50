using Nop.Core.Domain.Orders;
using Nop.Plugin.CW.Core.Domain.Exchanges;

namespace Nop.Plugin.CW.Core.Services.Exchanges;

public interface IExchangesDisplayService
{
    /// <summary>
    /// Extra display data for a return request, when the caller has only the id.
    /// Prefer the overload taking the entities when they are already loaded.
    /// </summary>
    /// <param name="returnRequestId">Return request identifier</param>
    /// <returns>The extra info, or null when the return request no longer exists</returns>
    Task<ReturnRequestExtraInfo> GetReturnRequestExtraInfoAsync(int returnRequestId);

    /// <summary>
    /// Extra display data for a return request, built from entities the caller already holds.
    /// Callers rendering a list should use this - the id overload re-reads the return request
    /// and order item they already have.
    /// </summary>
    /// <param name="returnRequest">The return request</param>
    /// <param name="orderItem">The order item being returned</param>
    /// <returns>The extra info, or null when the return request is null</returns>
    Task<ReturnRequestExtraInfo> GetReturnRequestExtraInfoAsync(ReturnRequest returnRequest, OrderItem orderItem);

    /// <summary>
    /// Resolves a product thumbnail, preferring the picture attached to a chosen attribute value
    /// so a red shirt shows as red rather than as the catalogue image.
    /// </summary>
    /// <param name="productId">The product</param>
    /// <param name="attributesXml">Chosen attributes; may be null or empty</param>
    /// <param name="pictureSize">Target size in pixels</param>
    /// <returns>A picture URL; the store's default image when nothing is available</returns>
    Task<string> GetProductThumbUrlAsync(int productId, string attributesXml, int pictureSize);

    /// <summary>
    /// The packname assigned to an order item, or null when the store does not use packnames
    /// or none was assigned. Callers decide how to present absence - never render a placeholder.
    /// </summary>
    /// <param name="orderItemId">Order item identifier</param>
    /// <returns>The packname, or null</returns>
    Task<string> GetPacknameAsync(int orderItemId);

    /// <summary>
    /// The selected attribute value for every attribute in the given XML, keyed by attribute id.
    /// Used to preselect the picker and to detect whether a new selection differs from the current one.
    /// </summary>
    Task<IDictionary<string, string>> GetSelectedAttributeMapAsync(string attributesXml);

    /// <summary>
    /// Formats an amount kept in the primary store currency in the customer's working currency, a minus sign before a
    /// negative one
    /// </summary>
    /// <param name="amount">Amount in the primary store currency</param>
    /// <returns>A task that represents the asynchronous operation; the task result contains the formatted amount</returns>
    Task<string> FormatPriceAsync(decimal amount);

    /// <summary>
    /// Formats an amount kept in the primary store currency in an order's currency, at the order's rate, a minus sign
    /// before a negative one
    /// </summary>
    /// <param name="amount">Amount in the primary store currency</param>
    /// <param name="order">Order</param>
    /// <returns>A task that represents the asynchronous operation; the task result contains the formatted amount</returns>
    Task<string> FormatOrderPriceAsync(decimal amount, Order order);

    /// <summary>
    /// The customer-facing name of a fee type
    /// </summary>
    Task<string> GetFeeTypeNameAsync(FeeType feeType);

    /// <summary>
    /// The customer-facing name of an exchange type
    /// </summary>
    Task<string> GetExchangeTypeNameAsync(ExchangeType exchangeType);

    /// <summary>
    /// The customer-facing name of a return mode
    /// </summary>
    Task<string> GetExchangeModeNameAsync(ExchangeReturnMode exchangeMode);
}