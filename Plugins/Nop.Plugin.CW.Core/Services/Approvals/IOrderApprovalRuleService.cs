using Nop.Core.Domain.Orders;

namespace Nop.Plugin.CW.Core.Services.Approvals;

/// <summary>
/// Decides whether a placed order must wait for an approver
/// </summary>
public interface IOrderApprovalRuleService
{
    /// <summary>
    /// Decides whether an order needs approval. Only an order that leaves something to pay, with one of the store's
    /// ApprovalPaymentMethods, is judged: against the rules of the branch and of the departments of the customer who
    /// placed it, on its order total. An order with an exchange replacement needs none.
    /// </summary>
    /// <param name="order">The order just placed</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the requirement; not required when Approvals is off for the order's store
    /// </returns>
    Task<OrderApprovalRequirement> EvaluateOrderAsync(Order order);
}
