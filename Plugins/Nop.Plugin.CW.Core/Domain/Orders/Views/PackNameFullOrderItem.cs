using Nop.Core.Domain.Orders;

namespace Nop.Plugin.CW.Core.Domain.Orders.Views;

public class PackNameFullOrderItem : OrderItem
{
    public int PackNameId { get; set; }

    public string PackName { get; set; }

    public int? PackNameCustomerId { get; set; }

    public int BranchId { get; set; }

    public string BranchCode { get; set; }

    public string Branch { get; set; }

    public int DepartmentId { get; set; }

    public string Department { get; set; }

    public int CostCenterId { get; set; }

    public string CostCenter { get; set; }
}
