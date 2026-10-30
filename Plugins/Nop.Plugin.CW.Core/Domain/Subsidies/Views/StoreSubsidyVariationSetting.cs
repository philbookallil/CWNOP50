using Nop.Core;

namespace Nop.Plugin.CW.Core.Domain.Subsidies.Views;

public class StoreSubsidyVariationSetting : BaseEntity
{
    public int StoreId { get; set; }

    public bool PackNamesOwnGlobalValue { get; set; }

    public bool PackNamesOwnStartAndFinishDates { get; set; }
}
