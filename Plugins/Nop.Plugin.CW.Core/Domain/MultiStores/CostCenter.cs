using Nop.Core;
using Nop.Core.Domain.Common;
using Nop.Plugin.CW.Core.Domain.Common;

namespace Nop.Plugin.CW.Core.Domain.MultiStores;

public class CostCenter : BaseEntity, ISoftDeletedEntity, INamedEntity
{
    public int StoreId { get; set; }

    public int? BranchId { get; set; }

    public string Name { get; set; }

    public bool Active { get; set; }

    public bool Deleted { get; set; }
}
