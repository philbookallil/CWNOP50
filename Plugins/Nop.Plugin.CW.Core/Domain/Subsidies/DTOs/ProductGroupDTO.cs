using Nop.Core;

namespace Nop.Plugin.CW.Core.Domain.Subsidies.DTOs;

public class ProductGroupDTO : BaseEntity
{
    public int StoreId { get; set; }

    public string Name { get; set; }

    public string Description { get; set; }

    public string Icon { get; set; }

    public string Colour { get; set; }

    public int DisplayOrder { get; set; }

    public int ProductCount { get; set; }

    public bool Active { get; set; }
}
