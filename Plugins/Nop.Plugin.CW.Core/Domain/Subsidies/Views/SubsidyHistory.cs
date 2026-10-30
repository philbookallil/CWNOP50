using LinqToDB.Mapping;
using Nop.Core;

namespace Nop.Plugin.CW.Core.Domain.Subsidies.Views;

public partial class SubsidyHistory : BaseEntity
{
    public int PackNameId { get; set; }

    public int SubsidyId { get; set; }

    public int OrderId { get; set; }

    public string Subsidy { get; set; }

    public decimal Credit { get; set; }

    public decimal CreditTopup { get; set; }

    public int Quantity { get; set; }

    public decimal ProductGroupCredit { get; set; }

    public int QuantityTopup { get; set; }

    public decimal ProductGroupCreditTopup { get; set; }

    public string ProductGroup { get; set; }

    public string Reason { get; set; }

    public int HistoryTypeId { get; set; }

    /// <summary>
    /// True when the SubsidyPackName assignment behind this row has been removed.
    /// </summary>
    /// <remarks>
    /// The row is still reported: the money was really spent and hiding it would lose the audit trail.
    /// It is marked instead, so a figure that no longer has a live assignment behind it is visibly
    /// historical rather than silently indistinguishable from a current one.
    /// </remarks>
    public bool AssignmentRemoved { get; set; }

    public int CreatorId { get; set; }

    public string CreatorName { get; set; }

    public DateTime CreatedOn { get; set; }

    public int TotalCount { get; set; }

    #region Custom properties

    /// <summary>
    /// Gets or sets the order status
    /// </summary>
    [NotColumn]
    public HistoryType HistoryType
    {
        get => (HistoryType)HistoryTypeId;
        set => HistoryTypeId = (int)value;
    }

    #endregion
}
