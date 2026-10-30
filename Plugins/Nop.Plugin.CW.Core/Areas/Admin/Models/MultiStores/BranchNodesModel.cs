namespace Nop.Plugin.CW.Core.Areas.Admin.Models.MultiStores
{
    public class BranchNodesModel
    {
        public List<BranchNodeModel> Nodes { get; set; } = new();

        public HashSet<string> SelectedIds { get; set; } = new();
    }
}
