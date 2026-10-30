using Nop.Plugin.CW.Core.Areas.Admin.Models.Exchanges;
using Nop.Plugin.CW.Core.Areas.Admin.Models.Exchanges.FeeRule;
using Nop.Plugin.CW.Core.Areas.Admin.Models.Exchanges.ReturnActionRule;
using Nop.Plugin.CW.Core.Areas.Admin.Models.Exchanges.ReturnReasonRule;
using Nop.Plugin.CW.Core.Areas.Admin.Models.Exchanges.ReturnStatusRule;

namespace Nop.Plugin.CW.Core.Areas.Admin.Factories;

public interface IExchangeSettingsModelFactory
{
    Task<ReturnReasonRuleListModel> PrepareReturnReasonRuleListModelAsync(ReturnReasonRuleSearchModel searchModel);

    Task<ReturnActionRuleListModel> PrepareReturnActionRuleListModelAsync(ReturnActionRuleSearchModel searchModel);

    Task<ReturnStatusRuleListModel> PrepareReturnStatusRuleListModelAsync(ReturnStatusRuleSearchModel searchModel);

    Task<FeeRuleListModel> PrepareFeeRuleListModelAsync(FeeRuleSearchModel searchModel);

    Task<IList<CWReturnRequestModel>> PrepareOrderReturnRequestGridAsync(int orderId);
}

