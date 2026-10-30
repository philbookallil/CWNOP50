using FluentValidation;
using Nop.Plugin.Payments.NABGateway.Models;
using Nop.Services.Localization;
using Nop.Web.Framework.Validators;

namespace Nop.Plugin.Payments.NABGateway.Validators;

/// <summary>
/// FluentValidation rules for the public payment information form
/// </summary>
public class PaymentInfoValidator : BaseNopValidator<PaymentInfoModel>
{
    public PaymentInfoValidator(ILocalizationService localizationService)
    {
        RuleFor(x => x.CardholderName)
            .NotEmpty()
            .WithMessageAwait(localizationService.GetResourceAsync("Plugins.Payments.NABGateway.Payment.CardholderName.Required"));

        //nop 5.00 dropped its IsCreditCard() rule; FluentValidation's own runs the same Luhn check
        RuleFor(x => x.CardNumber)
            .NotEmpty()
            .WithMessageAwait(localizationService.GetResourceAsync("Plugins.Payments.NABGateway.Payment.CardNumber.Required"))
            .CreditCard()
            .WithMessageAwait(localizationService.GetResourceAsync("Plugins.Payments.NABGateway.Payment.CardNumber.Wrong"));

        RuleFor(x => x.CardCode)
            .Matches(@"^[0-9]{3,4}$")
            .WithMessageAwait(localizationService.GetResourceAsync("Plugins.Payments.NABGateway.Payment.CardCode.Wrong"));

        RuleFor(x => x.ExpireMonth)
            .NotEmpty()
            .WithMessageAwait(localizationService.GetResourceAsync("Plugins.Payments.NABGateway.Payment.ExpireMonth.Required"));

        RuleFor(x => x.ExpireYear)
            .NotEmpty()
            .WithMessageAwait(localizationService.GetResourceAsync("Plugins.Payments.NABGateway.Payment.ExpireYear.Required"));

        RuleFor(x => x.ExpireMonth).Must((x, _) =>
        {
            //not specified yet
            if (string.IsNullOrEmpty(x.ExpireYear) || string.IsNullOrEmpty(x.ExpireMonth))
                return true;

            //a card remains valid until the last calendar day of its expiry month
            if (!int.TryParse(x.ExpireMonth, out var month) || !int.TryParse(x.ExpireYear, out var year))
                return false;

            if (month < 1 || month > 12)
                return false;

            var firstOfNextMonth = new DateTime(year, month, 1).AddMonths(1);
            return firstOfNextMonth > DateTime.Now;
        }).WithMessageAwait(localizationService.GetResourceAsync("Plugins.Payments.NABGateway.Payment.ExpirationDate.Expired"));
    }
}
