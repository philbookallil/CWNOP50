using Nop.Plugin.CW.Core.Domain.Orders.Views;
using Nop.Plugin.CW.Core.Domain.Subsidies;
using Nop.Plugin.CW.Core.Domain.Subsidies.DTOs;
using Nop.Plugin.CW.Core.Domain.Subsidies.Views;

namespace Nop.Plugin.CW.Core.Services.Subsidies;

public interface ISubsidyCreditService
{
    Task<IList<SubsidyPackNameCredit>> GetSubsidyPackNameCreditsByPackNameIdAsync(int packNameId);

    Task<IList<ProductGroupPackNameCredit>> GetProductGroupPackNameCreditByPackNameIdsAsync(int packNameId);

    Task<PackNameCredit> GetPackNameSubsidyCreditAsync(int packNameId);

    Task<IList<PackNameCredit>> GetPackNamesSubsidyCreditAsync(IList<int> packNameIds);

    /// <summary>
    /// Reads the credit of the given packnames that this request has not read yet, in one query to each credit
    /// view, so the reads of one packname after it are answered without a query
    /// </summary>
    /// <param name="packNameIds">PackName identifiers</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    Task LoadPackNameCreditsAsync(IList<int> packNameIds);

    /// <summary>
    /// Forgets the packname credit read so far in this request, so the next read goes to the database
    /// </summary>
    void ClearPackNameCredits();

    int ApplyPackNameSubsidyQuantity(PackNameCredit packNameCredit, PackNameShoppingCartItem shoppingCartItem, int quantiy, IList<SubsidyOrderItem> orderItems);

    decimal ApplyPackNameSubsidyCredit(PackNameCredit packNameCredit, PackNameShoppingCartItem shoppingCartItem, decimal subTotalInclTax, IList<SubsidyOrderItem> orderItems);

    decimal GetPackNameShippingRemaining(int packNameId, decimal packNameShippingTotalInclTaxt, IList<PackNameCredit> packNameCredits, IList<SubsidyOrderShipping> subsidyOrderShippings);

    /// <summary>
    /// Gets the subsidy order items by order identifier
    /// </summary>
    /// <param name="orderId"></param>
    /// <returns></returns>
    Task<IList<SubsidyOrderItem>> GetSubsidyOrderItemsByOrderIdAsync(int orderId);

    Task InsertSubsidyOrderItemAsync(SubsidyOrderItem subsidyOrderItem);

    Task<IList<SubsidyOrderShipping>> GetSubsidyOrderShippingsAsync(int orderId);

    Task InsertSubsidyOrderShippingAsync(SubsidyOrderShipping subsidyOrderShipping);

    /// <summary>
    /// Gives back, in kind, what a returned order item consumed: units restore units, product-group
    /// credit restores to that exact group, global credit restores to global.
    /// Writes compensating rows; mutates and deletes nothing.
    /// </summary>
    /// <returns>
    /// What was given back, including its money equivalent — the figure the credit-note side subtracts
    /// from the returned value so that only the customer's own money comes back as a note.
    /// </returns>
    Task<SubsidyRestoreResult> RestoreSubsidyForReturnedItemAsync(SubsidyRestoreRequest request);

    /// <summary>
    /// Removes the restorations raised by one return case line, so an undone or re-opened approval
    /// leaves nothing behind.
    /// </summary>
    Task DeleteSubsidyRestoreItemsForCaseLineAsync(int returnCaseLineId);
}
