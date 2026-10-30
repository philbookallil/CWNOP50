namespace Nop.Plugin.CW.Core.Areas.Admin.Validators.Exchanges
{
    /* public class ReturnActionRuleValidator : BaseNopValidator<ReturnActionRuleModel>
     {
         private IExchangeService _exchangeService;

         public ReturnActionRuleValidator(IExchangeService exchangeService)
         {
             _exchangeService = exchangeService;

             RuleFor(m => m.ReturnActionId)
                 .NotEmpty().WithMessage("A Return Action must be selected")
                 .MustAwait((rule, reasonId) => UniqueReturnActionRulePerStoreAsync(rule)).WithMessage("A rule for this Return Action is already in place");
         }

         private async Task<bool> UniqueReturnActionRulePerStoreAsync(ReturnActionRuleModel rule)
         {
             var existingRule = await _exchangeService.GetRuleByActionIdAsync(rule.ReturnActionId, rule.StoreId);

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