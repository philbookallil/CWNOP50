using Nop.Core.Domain.Orders;

namespace Nop.Plugin.CW.Core.Domain.Orders.Views;

public partial class PackNameShoppingCartItem : ShoppingCartItem
{
    public int PackNameId { get; set; }

    public string Name { get; set; }

    public bool Group { get; set; }

    public string Colour { get; set; }

    public int? PackNameCustomerId { get; set; }

    public int BranchId { get; set; }

    public string BranchCode { get; set; }

    public string Branch { get; set; }

    public int DepartmentId { get; set; }

    public string Department { get; set; }

    public int CostCenterId { get; set; }

    public string CostCenter { get; set; }

    public int PackNameListId { get; set; }

    public string StoreName { get; set; }
}
