using Nop.Core.Domain.Catalog;
using Nop.Core.Domain.Customers;
using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Stores;
using Nop.Plugin.CW.Core.Domain.Exchanges;
using Nop.Plugin.CW.Core.Domain.Products;
using Nop.Plugin.CW.Core.Services.Exchanges;

namespace Nop.Plugin.CW.Core.BusinessLogic;

/// <summary>
/// Orchestrates the Return/Exchange workflow across the Order, Exchange, Credit Note and Subsidy services.
/// </summary>
public interface IExchangesLogic
{
    /// <summary>
    /// Builds a transient single-product shopping cart, reused to leverage NOP's price calculation.
    /// </summary>
    IList<ShoppingCartItem> BuildSingleProductItemCart(Store store, int productId, Customer customer,
        string attributesXml, int quantity = 1);

    /// <summary>
    /// Sub-total of the given cart (excludes shipping and other additional amounts), including tax.
    /// </summary>
    Task<decimal?> CalculateCartItemsTotalAmountAsync(IList<ShoppingCartItem> cart);

    /// <summary>
    /// The value of the quantity being returned — unit price incl tax × returned quantity. This is the
    /// ceiling of any credit note for the line; the note actually raised is this figure minus whatever
    /// subsidy went back in kind (ExecuteApprovedLineAsync).
    /// </summary>
    Task<decimal?> DetermineExchangeCreditNoteAmountAsync(ReturnRequest returnRequest);

    Task<string> GenerateProductAttributesXmlAsync(Product product, IEnumerable<SelectedProductAttribute> selectedAttributes);

    Task<bool> ModifyExchangedItemDetailsAsync(ReturnRequestItem returnRequestItem, IEnumerable<SelectedProductAttribute> selectedAttributes);

    /// <summary>
    /// Runs the approval workflow for one return request: raises the credit note if the rules call for
    /// one, places the exchange order or puts the replacement in the customer's cart, and notifies the
    /// customer.
    /// </summary>
    /// <remarks>
    /// Called from the administrator's save, for the line being saved, once its data has been committed —
    /// which is why it takes no form data and reads persisted state exclusively. It is a no-op unless
    /// <paramref name="newStatusId"/> is Return Authorized.
    /// </remarks>
    /// <param name="newStatusId">
    /// The status this save is setting. Nothing runs unless it is Return Authorized: the controller knows
    /// only that the status changed, so this is where "is this the change that authorises the return" is
    /// answered.
    /// </param>
    Task<ReturnRequestWorkflowResult> ExecuteApprovedLineAsync(ReturnRequest returnRequest, int newStatusId);

    Task UndoReturnRequestApprovalAsync(ReturnRequest returnRequest);
}