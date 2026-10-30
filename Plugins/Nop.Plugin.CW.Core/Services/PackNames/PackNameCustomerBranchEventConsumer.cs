using Nop.Core.Events;
using Nop.Plugin.CW.Core.Domain.PackNames;
using Nop.Services.Events;

namespace Nop.Plugin.CW.Core.Services.PackNames;

/// <summary>
/// Keeps a packname's customer in the packname's branch: whenever a packname is saved - created, edited, moved to another
/// list singly or in bulk - or a list is saved, perhaps under another branch, the customer's branch becomes the branch
/// of the packname's list
/// </summary>
/// <remarks>
/// The other direction, a customer who changes branch taking their packname along, is done where the customer's branch is
/// saved (<see cref="IPackNameProvisioningService.ApplyCustomerBranchAsync"/>); the packname that move saves comes back
/// through here and finds its customer already in place.
/// </remarks>
public class PackNameCustomerBranchEventConsumer : IConsumer<EntityInsertedEvent<PackName>>,
    IConsumer<EntityUpdatedEvent<PackName>>,
    IConsumer<EntityUpdatedEvent<PackNameList>>
{
    #region Fields

    protected readonly IPackNameProvisioningService _packNameProvisioningService;

    #endregion

    #region Ctor

    public PackNameCustomerBranchEventConsumer(IPackNameProvisioningService packNameProvisioningService)
    {
        _packNameProvisioningService = packNameProvisioningService;
    }

    #endregion

    #region Methods

    /// <summary>
    /// Handles a packname being created
    /// </summary>
    /// <param name="eventMessage">The event message</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    public async Task HandleEventAsync(EntityInsertedEvent<PackName> eventMessage)
    {
        await _packNameProvisioningService.SetCustomerBranchFromPackNameAsync(eventMessage.Entity);
    }

    /// <summary>
    /// Handles a packname being saved
    /// </summary>
    /// <param name="eventMessage">The event message</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    public async Task HandleEventAsync(EntityUpdatedEvent<PackName> eventMessage)
    {
        await _packNameProvisioningService.SetCustomerBranchFromPackNameAsync(eventMessage.Entity);
    }

    /// <summary>
    /// Handles a packname list being saved
    /// </summary>
    /// <param name="eventMessage">The event message</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    public async Task HandleEventAsync(EntityUpdatedEvent<PackNameList> eventMessage)
    {
        await _packNameProvisioningService.SetCustomerBranchesFromPackNameListAsync(eventMessage.Entity);
    }

    #endregion
}