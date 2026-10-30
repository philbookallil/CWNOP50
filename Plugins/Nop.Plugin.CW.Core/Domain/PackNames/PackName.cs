using LinqToDB.Mapping;
using Nop.Core;
using Nop.Core.Domain.Common;
using Nop.Plugin.CW.Core.Domain.Common;

namespace Nop.Plugin.CW.Core.Domain.PackNames;

public class PackName : BaseEntity, ISoftDeletedEntity, INamedEntity
{
    public string Name { get; set; }

    public string Colour { get; set; }

    public string Gender { get; set; }

    public string PrintName { get; set; }

    public bool Group { get; set; }

    public bool Active { get; set; }

    public int PackNameListId { get; set; }

    public int? CustomerId { get; set; }

    public int? DepartmentId { get; set; }

    public int? CostCenterId { get; set; }

    public bool Manager { get; set; }

    public string EmployeeNumber { get; set; }

    public string POEntry { get; set; }

    public string Comments { get; set; }

    public bool Deleted { get; set; }

    #region Custom properties

    [NotColumn]
    public bool IsCustomer
    {
        get => CustomerId.HasValue && CustomerId.Value > 0;
    }

    #endregion
}
