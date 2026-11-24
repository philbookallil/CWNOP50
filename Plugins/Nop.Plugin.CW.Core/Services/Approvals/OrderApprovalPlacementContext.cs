namespace Nop.Plugin.CW.Core.Services.Approvals;

/// <summary>
/// Keeps a held plain order's e-mails until the order is settled
/// </summary>
/// <remarks>
/// nop's own placement publishes <c>OrderPlacedEvent</c>, which holds the order, before CW settles the order's store
/// credit, and the e-mails show the settled order; so while that placement runs they wait for
/// <c>PlainOrderSettledEvent</c>. Registered per request (scoped).
/// </remarks>
public class OrderApprovalPlacementContext
{
    #region Fields

    private readonly HashSet<int> _customersBeingPlaced = [];
    private readonly Dictionary<int, OrderApprovalRequirement> _notificationsByOrder = new();

    #endregion

    #region Methods

    /// <summary>
    /// Starts a placement whose order is settled after OrderPlacedEvent
    /// </summary>
    /// <param name="customerId">Customer identifier</param>
    public void BeginPlainPlacement(int customerId) => _customersBeingPlaced.Add(customerId);

    /// <summary>
    /// Ends the placement, whether or not it placed an order
    /// </summary>
    /// <param name="customerId">Customer identifier</param>
    public void EndPlainPlacement(int customerId) => _customersBeingPlaced.Remove(customerId);

    /// <summary>
    /// Gets a value indicating whether the customer's order is being placed and will be settled later
    /// </summary>
    /// <param name="customerId">Customer identifier</param>
    public bool IsPlainPlacementRunning(int customerId) => _customersBeingPlaced.Contains(customerId);

    /// <summary>
    /// Keeps a held order's e-mails until the order is settled
    /// </summary>
    /// <param name="orderId">Order identifier</param>
    /// <param name="requirement">The decision the order was held on</param>
    public void SetPendingNotification(int orderId, OrderApprovalRequirement requirement)
    {
        ArgumentNullException.ThrowIfNull(requirement);

        _notificationsByOrder[orderId] = requirement;
    }

    /// <summary>
    /// Takes a held order's waiting e-mails; null when there are none
    /// </summary>
    /// <param name="orderId">Order identifier</param>
    public OrderApprovalRequirement TakePendingNotification(int orderId)
        => _notificationsByOrder.Remove(orderId, out var requirement) ? requirement : null;

    #endregion
}
