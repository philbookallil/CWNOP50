using Nop.Core;

namespace Nop.Plugin.CW.Core.Domain.Subsidies.Views;

public partial class SubsidyPackNameCanOrder : BaseEntity
{
    public bool CanOrder { get; set; }

    public bool OnlyAllocatedProducts { get; set; }
}
