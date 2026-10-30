using Nop.Plugin.CW.Core.Models.Exchanges.FeeRule;

namespace Nop.Plugin.CW.Core.Models.Exchanges;

public record ExchangesFeesModel
{
    #region Ctor

    public ExchangesFeesModel()
    {
        Fees = new List<FeeDisplayModel>();
        AdditionalFees = new List<AdditionalFeeItemModel>();
    }

    #endregion

    #region Proerties

    public IList<FeeDisplayModel> Fees { get; set; }

    public IList<AdditionalFeeItemModel> AdditionalFees { get; set; }

    #endregion
}