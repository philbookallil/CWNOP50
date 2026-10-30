using Nop.Web.Framework.Models;
using Nop.Web.Framework.Mvc.ModelBinding;
using Nop.Web.Models.Common;

namespace Nop.Plugin.CW.Core.Models.PackNames;

public record PackNameModel : BaseNopEntityModel
{
    #region Properties

    public int BranchId { get; set; }

    [NopResourceDisplayName("Plugins.CW.Core.PackNames.PackName.Field.Name")]
    public string Name { get; set; }

    [NopResourceDisplayName("Plugins.CW.Core.PackNames.PackName.Field.Branch")]
    public string Branch { get; set; }

    [NopResourceDisplayName("Plugins.CW.Core.PackNames.PackName.Field.Colour")]
    public string Colour { get; set; }

    public string ForeColor { get; set; }

    [NopResourceDisplayName("Plugins.CW.Core.PackNames.PackName.Field.Group")]
    public bool Group { get; set; }

    [NopResourceDisplayName("Plugins.CW.Core.PackNames.PackName.Field.IsCustomer")]
    public bool IsCustomer { get; set; }

    [NopResourceDisplayName("Plugins.CW.Core.PackNames.PackName.Field.POEntry")]
    public string POEntry { get; set; }

    public string EmployeeNumber { get; set; }

    public int InCart { get; set; }

    public bool BlockOrdering { get; set; }

    public string BlockOrderingReason { get; set; }

    public bool Editable { get; set; }

    public List<AddressModel> AvailableAddresses { get; set; } = new();

    #endregion
}
