using Nop.Plugin.CW.Core.Areas.Admin.Models.PackNames;

namespace Nop.Plugin.CW.Core.Infrastructure;

public interface IPackNamePanelProvider
{
    int Order { get; }

    Task<PackNamePanelModel> GetPanelAsync(int packNameId, string prefix = null);
}
