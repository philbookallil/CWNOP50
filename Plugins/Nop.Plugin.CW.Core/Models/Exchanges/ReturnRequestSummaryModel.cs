using Nop.Plugin.CW.Core.Domain.Exchanges;
using Nop.Plugin.CW.Core.Models.Exchanges.FeeRule;

namespace Nop.Plugin.CW.Core.Models.Exchanges;

public record ReturnRequestSummaryModel
{
    #region Ctor

    public ReturnRequestSummaryModel()
    {
        EstimatedFees = new List<FeeConceptItem>();
    }

    #endregion

    #region Properties

    public ExchangeReturnMode Mode { get; set; }

    public ReturnRequestGatherInfoModel GatheredInfo { get; set; }

    public IList<FeeConceptItem> EstimatedFees { get; set; }

    #endregion
}