using Nop.Plugin.CW.Core.Domain.Exchanges;

namespace Nop.Plugin.CW.Core.Models.Exchanges;

/// <summary>
/// Model class for Additional Fee items
/// </summary>
public record AdditionalFeeItemModel
{
    #region Properties

    public string FeeName { get; set; }

    public decimal FeeAmount { get; set; }

    /// <summary>
    /// The amount formatted for the page that shows it
    /// </summary>
    public string FeeAmountDisplay { get; set; }

    #endregion

    #region Ctor

    public AdditionalFeeItemModel(AdditionalFeeItem item)
    {
        this.FeeAmount = item.FeeAmount;
        this.FeeName = item.FeeName;
    }

    #endregion
}