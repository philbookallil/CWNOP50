using Nop.Core;

namespace Nop.Plugin.CW.Core.Domain.Orders;

public partial class PackNameOrderItem : BaseEntity
{
    public int OrderItemId { get; set; }

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
