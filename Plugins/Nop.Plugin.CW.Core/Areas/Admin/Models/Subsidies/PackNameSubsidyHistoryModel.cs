using Nop.Plugin.CW.Core.Domain.Subsidies.Views;
using Nop.Web.Framework.Models;

namespace Nop.Plugin.CW.Core.Areas.Admin.Models.Subsidies;

public record PackNameSubsidyHistoryModel : BaseNopModel
{
    public int PackNameId { get; set; }

    public int SubsidyId { get; set; }

    public int OrderId { get; set; }

    public string OrderIdText { get; set; }

    public string Subsidy { get; set; }

    public decimal Credit { get; set; }

    public string CreditFormatted { get; set; }

    public decimal CreditTopup { get; set; }

    public string CreditTopupFormatted { get; set; }

    public int Quantity { get; set; }

    public decimal ProductGroupCredit { get; set; }

    public string ProductGroupCreditFormatted { get; set; }

    public int QuantityTopup { get; set; }

    public decimal ProductGroupCreditTopup { get; set; }

    public string ProductGroupCreditTopupFormatted { get; set; }

    public string ProductGroup { get; set; }

    public string Reason { get; set; }

    public int HistoryTypeId { get; set; }

    /// <summary>
    /// The localised marker shown in the grid, or an empty string when the assignment is live.
    /// </summary>
    /// <remarks>
    /// A pre-formatted string rather than a bool the view renders: every other column in this grid is a
    /// plain property column, and a RenderCustom here would be the only JavaScript in it.
    /// </remarks>
    public string AssignmentRemovedText { get; set; }

    public int CreatorId { get; set; }

    public string CreatorName { get; set; }

    public DateTime CreatedOn { get; set; }

    public string CreatedOnFormatted { get; set; }

    #region Custom properties

    /// <summary>
    /// Gets or sets the order status
    /// </summary>
    public HistoryType HistoryType
    {
        get => (HistoryType)HistoryTypeId;
        set => HistoryTypeId = (int)value;
    }

    #endregion
}
