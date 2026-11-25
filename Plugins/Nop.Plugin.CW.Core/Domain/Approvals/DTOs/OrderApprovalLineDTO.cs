using Nop.Core.Domain.Orders;

namespace Nop.Plugin.CW.Core.Domain.Approvals.DTOs;

/// <summary>
/// One approval of the admin list, with its order
/// </summary>
public class OrderApprovalLineDTO
{
    public OrderApproval Approval { get; set; }

    public Order Order { get; set; }
}
