using Nop.Core;
using Nop.Core.Domain.Customers;
using Nop.Core.Domain.Orders;
using Nop.Data;
using Nop.Plugin.CW.Core.Domain.Approvals;
using Nop.Plugin.CW.Core.Domain.Approvals.DTOs;

namespace Nop.Plugin.CW.Core.Services.Approvals;

/// <summary>
/// Reads the approvals of the orders for the admin list
/// </summary>
public class OrderApprovalReportService : IOrderApprovalReportService
{
    #region Fields

    protected readonly IRepository<Customer> _customerRepository;
    protected readonly IRepository<Order> _orderRepository;
    protected readonly IRepository<OrderApproval> _orderApprovalRepository;
    protected readonly IRepository<OrderApprovalRequest> _requestRepository;

    #endregion

    #region Ctor

    public OrderApprovalReportService(IRepository<Customer> customerRepository,
        IRepository<Order> orderRepository,
        IRepository<OrderApproval> orderApprovalRepository,
        IRepository<OrderApprovalRequest> requestRepository)
    {
        _customerRepository = customerRepository;
        _orderRepository = orderRepository;
        _orderApprovalRepository = orderApprovalRepository;
        _requestRepository = requestRepository;
    }

    #endregion

    #region Methods

    public virtual async Task<bool> HasApprovalsAsync() => await _orderApprovalRepository.Table.AnyAsync();

    public virtual async Task<IPagedList<OrderApprovalLineDTO>> SearchApprovalsAsync(OrderApprovalStatus? status = null, int storeId = 0, string orderNumber = null,
        string customer = null, int approverId = 0, int pageIndex = 0, int pageSize = int.MaxValue)
    {
        orderNumber = orderNumber?.Trim();
        customer = customer?.Trim();

        var query = from approval in _orderApprovalRepository.Table
                    join order in _orderRepository.Table on approval.OrderId equals order.Id
                    where !order.Deleted
                    select new { approval, order };

        if (status.HasValue)
        {
            var statusId = (int)status.Value;
            query = query.Where(line => line.approval.StatusId == statusId);
        }

        if (storeId > 0)
            query = query.Where(line => line.order.StoreId == storeId);

        if (!string.IsNullOrEmpty(orderNumber))
            query = query.Where(line => line.order.CustomOrderNumber == orderNumber);

        if (!string.IsNullOrEmpty(customer))
        {
            query = from line in query
                    join person in _customerRepository.Table on line.order.CustomerId equals person.Id
                    where person.Email.Contains(customer) || (person.FirstName + " " + person.LastName).Contains(customer)
                    select line;
        }

        if (approverId > 0)
        {
            query = query.Where(line => _requestRepository.Table.Any(request => request.OrderApprovalId == line.approval.Id
                && request.ApproverId == approverId
                && (request.RevokedOnUtc == null || line.approval.StatusId != (int)OrderApprovalStatus.Pending)));
        }

        return await query
            .OrderByDescending(line => line.approval.CreatedOnUtc)
            .ThenByDescending(line => line.approval.Id)
            .Select(line => new OrderApprovalLineDTO { Approval = line.approval, Order = line.order })
            .ToPagedListAsync(pageIndex, pageSize);
    }

    #endregion
}
