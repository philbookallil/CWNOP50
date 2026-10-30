namespace Nop.Plugin.CW.Core.Areas.Admin.Models.MultiStores;

public class BranchTreeModel
{
    public int StoreId { get; set; }

    public List<BranchNodeModel> Branches { get; set; } = new();

    public HashSet<string> SelectedIds { get; set; } = new();
}
