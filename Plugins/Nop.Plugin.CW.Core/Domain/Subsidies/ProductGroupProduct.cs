using Nop.Core;

namespace Nop.Plugin.CW.Core.Domain.Subsidies;

public class ProductGroupProduct : BaseEntity
{
    public int ProductGroupId { get; set; }

    public int ProductId { get; set; }
}
