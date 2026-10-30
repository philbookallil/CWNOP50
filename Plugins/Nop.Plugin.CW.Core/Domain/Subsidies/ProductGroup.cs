using Nop.Core;
using Nop.Core.Domain.Common;

namespace Nop.Plugin.CW.Core.Domain.Subsidies;

public class ProductGroup : BaseEntity, ISoftDeletedEntity
{
    public int StoreId { get; set; }

    public string Name { get; set; }

    public string Description { get; set; }

    public string Icon { get; set; }

    public string Colour { get; set; }

    public int DisplayOrder { get; set; }

    public DateTime CreatedOnUtc { get; set; }

    public bool Active { get; set; }

    public bool Deleted { get; set; }
}
