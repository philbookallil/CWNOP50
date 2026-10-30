namespace Nop.Plugin.CW.Core.Areas.Admin.Models.MultiStores;

public class BranchNodeModel
{
    public int Id { get; set; }

    public string Name { get; set; }

    public string Code { get; set; }

    public int? ParentId { get; set; }

    public int Depth { get; set; }

    public bool Active { get; set; }

    public bool HasChildren { get; set; }

    public List<BranchNodeModel> Children { get; set; } = new();
}
