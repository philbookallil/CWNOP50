using Nop.Core.Domain.Customers;
using Nop.Core.Domain.Gdpr;
using Nop.Core.Events;
using Nop.Data;
using Nop.Plugin.CW.Core.Domain.PackNames;
using Nop.Services.Events;

namespace Nop.Plugin.CW.Core.Services.PackNames;

/// <summary>
/// Breaks the packname → customer link when the customer is deleted, by the ordinary delete or by the permanent one (GDPR)
/// </summary>
/// <remarks>
/// nop's two deletes publish different events. The ordinary one deletes the customer through its repository, which
/// publishes the deleted event; the permanent one anonymises the customer, moves it to the Guests role and saves it as
/// deleted, so it publishes an updated event and <see cref="CustomerPermanentlyDeleted"/>, never the deleted one.
/// <para>
/// Not a query filter: "customer not deleted" would have to be added to every query and SQL view that reads the link,
/// and the stale id would stay in the data for the next reader to trip over.
/// </para>
/// <para>
/// The packname's own data, its orders and its <see cref="PackNameOrderItem"/> rows are deliberately untouched: a
/// packname is the employer's record of an employee and exists with or without an account, and
/// <see cref="PackNameOrderItem.PackNameCustomerId"/> is a snapshot of who ordered.
/// </para>
/// <para>
/// A guest removed by nop's bulk delete ("Delete guests") publishes no event at all: there the foreign key clears the
/// link.
/// </para>
/// </remarks>
public class PackNameCustomerDeletedEventConsumer : IConsumer<EntityDeletedEvent<Customer>>,
    IConsumer<CustomerPermanentlyDeleted>
{
    #region Fields

    protected readonly IRepository<PackName> _packNameRepository;

    #endregion

    #region Ctor

    public PackNameCustomerDeletedEventConsumer(IRepository<PackName> packNameRepository)
    {
        _packNameRepository = packNameRepository;
    }

    #endregion

    #region Utilities

    /// <summary>
    /// Clears the customer of every packname linked to it; a packname without a customer is no manager
    /// </summary>
    /// <param name="customerId">The deleted customer's identifier</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    protected virtual async Task ClearCustomerAsync(int customerId)
    {
        //deleted packnames included: a deleted packname keeps its row, and with it the link
        var packNames = await _packNameRepository.GetAllAsync(query =>
            query.Where(packName => packName.CustomerId == customerId), includeDeleted: true);

        foreach (var packName in packNames)
        {
            packName.CustomerId = null;
            packName.Manager = false;

            await _packNameRepository.UpdateAsync(packName);
        }
    }

    #endregion

    #region Methods

    /// <summary>
    /// Handles a customer being deleted
    /// </summary>
    /// <param name="eventMessage">The event message</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    public async Task HandleEventAsync(EntityDeletedEvent<Customer> eventMessage)
    {
        if (eventMessage.Entity is null)
            return;

        await ClearCustomerAsync(eventMessage.Entity.Id);
    }

    /// <summary>
    /// Handles a customer being permanently deleted (GDPR)
    /// </summary>
    /// <param name="eventMessage">The event message</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    public async Task HandleEventAsync(CustomerPermanentlyDeleted eventMessage)
    {
        await ClearCustomerAsync(eventMessage.CustomerId);
    }

    #endregion
}
