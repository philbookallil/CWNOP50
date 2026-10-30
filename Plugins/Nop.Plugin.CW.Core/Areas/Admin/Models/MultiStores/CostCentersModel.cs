namespace Nop.Plugin.CW.Core.Areas.Admin.Models.MultiStores;

//MIRRORS PackNameListsModel. The branch cost-centre collection is server-rendered now, so it no
//longer needs CostCenterSearchModel's paging state - that model stays for the store-level tab,
//which is still a grid.
public record CostCentersModel
{
    public int BranchId { get; set; }

    //Posted by the inline add row: CostCenterValidator requires StoreId NotEqual(0) and
    //FluentValidation runs before SaveCostCenter can derive it from the branch.
    public int StoreId { get; set; }

    public List<CostCenterRowModel> CostCenters { get; set; } = new();
}