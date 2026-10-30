using FluentValidation;
using Nop.Core.Domain.Orders;
using Nop.Plugin.CW.Core.Areas.Admin.Models.Exchanges;
using Nop.Plugin.CW.Core.BusinessLogic;
using Nop.Plugin.CW.Core.Domain.Exchanges;
using Nop.Plugin.CW.Core.Helpers;
using Nop.Plugin.CW.Core.Services.Exchanges;
using Nop.Services.Localization;
using Nop.Services.Orders;
using Nop.Web.Framework.Validators;

namespace Nop.Plugin.CW.Core.Areas.Admin.Validators.Exchanges;

public class ReturnExchangeRequestValidator : BaseNopValidator<CWReturnRequestModel>
{
    #region Utilities

    private bool ValidStatusTransition(CWReturnRequestModel model)
    {
        var currentStatus = (ReturnRequestStatus)model.OriginalRequestStatusId;
        var nextStatus = (ReturnRequestStatus)model.ReturnRequestStatusId;

        if (currentStatus != nextStatus)
        {
            // If the current status is a final status we reject the transition
            if (ExchangesHelper.IsFinalReturnRequestStatus(currentStatus))
            {
                return false;
            }
        }

        // If we get this far means no status transition restrictions were met (transition is allowed)
        return true;
    }

    private async Task<bool> ValidFeesAmountAsync(CWReturnRequestModel model, IReturnRequestService returnRequestService,
        IExchangeService exchangeService,
        IExchangesLogic exchangesLogic)
    {
        var returnRequest = await returnRequestService.GetReturnRequestByIdAsync(model.Id);
        if (returnRequest != null)
        {
            var returnRequestItem = await exchangeService.GetReturnRequestItemAsync(returnRequest.Id);
            if (returnRequestItem != null)
            {
                var creditNoteAmount = await exchangesLogic.DetermineExchangeCreditNoteAmountAsync(returnRequest);
                if (creditNoteAmount.HasValue)
                {
                    /*
                     * Overwrite the returnRequest item's fees data with the ones from the model that's coming in to
                     * allow the fees to be calculated properly using the data just entered by the user
                     */
                    if (model.ReturnRequestReasonId > 0)
                        returnRequestItem.ReturnReasonId = model.ReturnRequestReasonId;

                    if (model.ReturnRequestActionId > 0)
                        returnRequestItem.ReturnActionId = model.ReturnRequestActionId;

                    returnRequestItem.StaffRepackQty = model.StaffRepackQty;
                    returnRequestItem.StaffShipQty = model.StaffShipQty;
                    returnRequestItem.AdditionalFeeAmount = model.AdditionalFeeAmount;

                    //Priced straight from the instance overlaid above, not through the case. Since the
                    //revert every fee is priced over its own line, so this item is all the pricing needs.
                    //Going via the case would also return the FROZEN charges once it has resolved, and
                    //ignore the values the administrator is typing — which is the one thing this rule
                    //exists to check.
                    var feeAmounts = await exchangeService.CalculateExchangeFeeAmountsForItemAsync(returnRequestItem, returnRequest);

                    return creditNoteAmount.Value > feeAmounts.Sum(fee => fee.Amount);
                }
            }
        }

        return true;
    }

    #endregion

    public ReturnExchangeRequestValidator(IReturnRequestService returnRequestService,
        IExchangeService exchangeService,
        IExchangesLogic exchangesLogic,
        ILocalizationService localizationService)
    {
        RuleFor(m => m.ReturnRequestReasonId).GreaterThan(0).WithMessage("Please select a reason for return");
        RuleFor(m => m.ReturnRequestActionId).GreaterThan(0).WithMessage("Please select a requested action");

        //nop's own rule: its validator is not run for this model, and the save puts the difference back in stock
        RuleFor(m => m.ReturnedQuantity).Must((model, value) => value <= model.Quantity)
            .WithMessageAwait(async model => string.Format(await localizationService.GetResourceAsync("Admin.ReturnRequests.Fields.ReturnedQuantity.MustBeLessOrEqualQuantityField"), model.Quantity));

        RuleFor(m => m.StaffRepackQty).GreaterThanOrEqualTo(0).When(model => model.RepackagingFeeConfig != null).WithMessage("The repackaging quantity must be 0 or greater");
        RuleFor(m => m.StaffShipQty).GreaterThanOrEqualTo(0).When(model => model.ShippingFeeConfig != null).WithMessage("The redelivery quantity must be 0 or greater");
        RuleFor(m => m.ReturnRequestStatusId).Must((model, id) => ValidStatusTransition(model))
            .WithMessage(model => string.Format("Cannot transition from status {0} to {1}", (ReturnRequestStatus)model.OriginalRequestStatusId, (ReturnRequestStatus)model.ReturnRequestStatusId));
        RuleFor(m => m.AdditionalFeeName).NotEmpty().When(model => model.EnableAdditionalFee).WithMessage("Please enter a name for the Additional Fee");
        RuleFor(m => m.AdditionalFeeAmount).GreaterThan(0).When(model => model.EnableAdditionalFee).WithMessage("The Additional Fee amount must be greater than zero (0)");
        RuleFor(m => m.AdditionalFeeAmount)
            .MustAsync((model, id, _) => ValidFeesAmountAsync(model, returnRequestService, exchangeService, exchangesLogic))
            .When(model => model.ExchangeReturnMode == ExchangeReturnMode.CreditNote)
            .WithMessage("The total amount in fees is equal or greater than the Credit Notes allocation, please reduce the fees amounts so " +
                            "the fees total is less than the Credit Notes amount");
    }
}