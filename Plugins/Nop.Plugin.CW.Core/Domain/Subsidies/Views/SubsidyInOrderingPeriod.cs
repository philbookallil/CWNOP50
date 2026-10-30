using Nop.Core;

namespace Nop.Plugin.CW.Core.Domain.Subsidies.Views;

public partial class SubsidyInOrderingPeriod : BaseEntity
{
    public string? Name { get; set; }

    public bool OrderOutsidePeriod { get; set; }

    public bool InOrderingPeriod { get; set; }
}
