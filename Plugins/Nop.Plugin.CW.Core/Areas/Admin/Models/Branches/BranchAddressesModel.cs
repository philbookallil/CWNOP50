using Nop.Plugin.CW.Core.Areas.Admin.Models.Common;

namespace Nop.Plugin.CW.Core.Areas.Admin.Models.Branches;

//MIRRORS PackNameListsModel AND CostCentersModel. No row model of its own: CWAddressModel already
//carries AddressId, the names, the address parts and IsDefault, and every one of those is shown.
public record BranchAddressesModel
{
    public int BranchId { get; set; }

    public List<CWAddressModel> Addresses { get; set; } = new();
}