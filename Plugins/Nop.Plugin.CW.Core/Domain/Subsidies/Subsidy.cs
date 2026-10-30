using Nop.Core;
using Nop.Core.Domain.Common;

namespace Nop.Plugin.CW.Core.Domain.Subsidies;

public class Subsidy : BaseEntity, ISoftDeletedEntity
{
    public int StoreId { get; set; }

    public string Name { get; set; }

    public string Description { get; set; }

    public decimal Credit { get; set; }

    /// <summary>
    /// First day of the ordering period, in the store's local time. Required: the validator has always
    /// demanded both dates and the column is NOT NULL, so no reader has to handle an open-ended period.
    /// </summary>
    public DateTime DurationStart { get; set; }

    /// <summary>
    /// Last day of the ordering period (inclusive), in the store's local time. Required, as above.
    /// </summary>
    public DateTime DurationEnd { get; set; }

    public bool AnnualRecurrence { get; set; }

    public bool OrderOutsidePeriod { get; set; }

    public DateTime CreatedOnUtc { get; set; }

    public bool OnlyAllocatedProducts { get; set; }

    public bool IncludeManagers { get; set; }

    public bool RestrictToAllocatedQty { get; set; }

    public bool Active { get; set; }

    public bool Deleted { get; set; }
}