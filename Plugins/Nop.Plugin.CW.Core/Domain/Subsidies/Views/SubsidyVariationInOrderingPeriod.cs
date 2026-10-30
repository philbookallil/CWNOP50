using Nop.Core;

namespace Nop.Plugin.CW.Core.Domain.Subsidies.Views;

public partial class SubsidyVariationInOrderingPeriod : BaseEntity
{
    public int SubsidyId { get; set; }

    public string? Name { get; set; }

    public bool OrderOutsidePeriod { get; set; }

    public bool InOrderingPeriod { get; set; }
}
