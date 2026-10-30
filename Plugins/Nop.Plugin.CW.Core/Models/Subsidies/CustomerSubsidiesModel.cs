using Nop.Web.Framework.Models;

namespace Nop.Plugin.CW.Core.Models.Subsidies;

/// <summary>
/// The customer's subsidy page: every live assignment of the packname being ordered for, with its figures,
/// product groups and history.
/// </summary>
public record CustomerSubsidiesModel : BaseNopModel
{
    public int PackNameId { get; set; }

    public string PackNameName { get; set; }

    public string PackNameColour { get; set; }

    public string PackNameBranch { get; set; }

    public string PackNameDepartment { get; set; }

    public bool PackNameIsGroup { get; set; }

    public bool PackNameIsCustomer { get; set; }

    /// <summary>
    /// True when the packname shown is not the signed-in customer's own one (a manager ordering for an employee).
    /// </summary>
    public bool IsManagedPackName { get; set; }

    public IList<CustomerSubsidyModel> Subsidies { get; set; } = new List<CustomerSubsidyModel>();
}