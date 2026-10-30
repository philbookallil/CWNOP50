using Nop.Core;

namespace Nop.Plugin.CW.Core.Domain.Subsidies.Views;

public partial class SubsidyPackNameCredit : BaseEntity
{
    public int PackNameId { get; set; }

    public int SubsidyId { get; set; }

    public string SubsidyName { get; set; }

    public decimal Credit { get; set; }

    public decimal CreditTopup { get; set; }

    public decimal UsedCredit { get; set; }

    public decimal UsedCreditTopup { get; set; }

    public DateTime? DurationStart { get; set; }

    public DateTime? DurationEnd { get; set; }

    public bool AnnualRecurrence { get; set; }

    public bool OrderOutsidePeriod { get; set; }

    public bool OnlyAllocatedProducts { get; set; }

    public bool RestrictToAllocatedQty { get; set; }
}
