namespace Nop.Plugin.CW.Core.Domain.PackNames.DTOs;

public class PackNameDTO : PackName
{
    public string Email { get; set; }

    public string Branch { get; set; }

    public string PackNameListName { get; set; }

    public string CostCenter { get; set; }

    public string Department { get; set; }

    public string CustomerName { get; set; }
}