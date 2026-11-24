using Nop.Core.Events;
using Nop.Plugin.CW.Core.Domain.Approvals;
using Nop.Plugin.CW.Core.Domain.Branches;
using Nop.Plugin.CW.Core.Domain.MultiStores;
using Nop.Services.Events;

namespace Nop.Plugin.CW.Core.Services.Approvals;

/// <summary>
/// Switches off the approval rule of a deleted branch or department
/// </summary>
public class ApprovalRuleEventConsumer : IConsumer<EntityDeletedEvent<Branch>>,
    IConsumer<EntityDeletedEvent<Department>>
{
    #region Fields

    protected readonly IApprovalRuleService _approvalRuleService;

    #endregion

    #region Ctor

    public ApprovalRuleEventConsumer(IApprovalRuleService approvalRuleService)
    {
        _approvalRuleService = approvalRuleService;
    }

    #endregion

    #region Methods

    /// <summary>
    /// Switches off the rule of a deleted branch
    /// </summary>
    /// <param name="eventMessage">Event message</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    public async Task HandleEventAsync(EntityDeletedEvent<Branch> eventMessage)
    {
        if (eventMessage.Entity != null)
            await _approvalRuleService.DeactivateAsync(ApprovalScopeType.Branch, eventMessage.Entity.Id);
    }

    /// <summary>
    /// Switches off the rule of a deleted department
    /// </summary>
    /// <param name="eventMessage">Event message</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    public async Task HandleEventAsync(EntityDeletedEvent<Department> eventMessage)
    {
        if (eventMessage.Entity != null)
            await _approvalRuleService.DeactivateAsync(ApprovalScopeType.Department, eventMessage.Entity.Id);
    }

    #endregion
}
