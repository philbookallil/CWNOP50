using Nop.Web.Framework.Models;

namespace Nop.Plugin.CW.Core.Areas.Admin.Models.Subsidies;

public record PackNameSubsidyModel : BaseNopEntityModel
{
    #region Ctor

    public PackNameSubsidyModel()
    {
        PackNameSubsidyProductGroupSearchModel = new PackNameSubsidyProductGroupSearchModel();
    }

    #endregion

    #region Properties

    public int PackNameId { get; set; }

    public int SubsidyId { get; set; }

    public string? Name { get; set; }

    public string? Description { get; set; }

    public decimal Credit { get; set; }

    public decimal TopupCredit { get; set; }

    public string? TopupCreditFormatted { get; set; }

    public string? CreditFormatted { get; set; }

    public string? StartDate { get; set; }

    public string? EndDate { get; set; }

    public bool AnnualRecurrence { get; set; }

    public bool OrderOutsidePeriod { get; set; }

    public bool OnlyAllocatedProducts { get; set; }

    public bool IncludeManagers { get; set; }

    public bool RestrictToAllocatedQty { get; set; }

    public bool Active { get; set; }

    public int ProductGroupsCount { get; set; }

    public string UsedPercent { get; set; }

    public bool InOrderingPeriod { get; set; }

    /// <summary>
    /// Credit plus every top-up in the current window (money). Used and Remaining follow it.
    /// </summary>
    public decimal TotalCredit { get; set; }

    public decimal UsedCredit { get; set; }

    public decimal RemainingCredit { get; set; }

    public string? TotalCreditFormatted { get; set; }

    public string? UsedCreditFormatted { get; set; }

    public string? RemainingCreditFormatted { get; set; }

    /// <summary>
    /// True when at least one product group of the subsidy allocates units, so the panel shows the
    /// unit row as well as the money row.
    /// </summary>
    public bool HasQuantity { get; set; }

    public int TotalQuantity { get; set; }

    public int UsedQuantity { get; set; }

    public int RemainingQuantity { get; set; }

    public PackNameSubsidyProductGroupSearchModel PackNameSubsidyProductGroupSearchModel { get; set; }

    #endregion
}
