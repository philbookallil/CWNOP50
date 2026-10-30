namespace Nop.Plugin.CW.Core.Areas.Admin.Validators.Exchanges
{/*
    public class ReturnStatusRuleValidator : BaseNopValidator<ReturnStatusRuleModel>
    {
        private IExchangeService _exchangeService;

        public ReturnStatusRuleValidator(IExchangeService exchangeService)
        {
            _exchangeService = exchangeService;

            RuleFor(m => m.ReturnStatusId)
                .GreaterThanOrEqualTo(0).WithMessage("A Return Status must be selected")
                .MustAwait((rule, reasonId) => UniqueReturnStatusRulePerStoreAsync(rule)).WithMessage("A rule for this Return Status is already in place");
        }

        private async Task<bool> UniqueReturnStatusRulePerStoreAsync(ReturnStatusRuleModel rule)
        {
            var existingRule = await _exchangeService.GetRuleByStatusIdAsync(rule.ReturnStatusId, rule.StoreId);

            if(existingRule != null)
            {
                if(rule.Id > 0)
                {
                    return existingRule.Id == rule.Id;
                }
                else
                {
                    return false;
                }
            }

            return true;
        }
    
    }*/
}