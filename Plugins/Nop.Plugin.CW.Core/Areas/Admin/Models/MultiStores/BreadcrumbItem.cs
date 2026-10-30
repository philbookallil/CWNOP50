namespace Nop.Plugin.CW.Core.Areas.Admin.Models.MultiStores;

public class BreadcrumbItem
{
    public string Type { get; set; }   // "store" | "branch" | "list"

    public string Id { get; set; }

    public string Name { get; set; }
}
