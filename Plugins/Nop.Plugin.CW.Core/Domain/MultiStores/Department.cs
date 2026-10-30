using Nop.Core;
using Nop.Core.Domain.Common;

namespace Nop.Plugin.CW.Core.Domain.MultiStores;

public class Department : BaseEntity, ISoftDeletedEntity
{
    public int StoreId { get; set; }

    public string Name { get; set; }

    public int? CustomerRoleId { get; set; }

    public bool Active { get; set; }

    public bool Deleted { get; set; }
}
