using FluentValidation;
using Nop.Core.Domain.Messages;
using Nop.Plugin.CW.Core.Areas.Admin.Models.Subsidies;
using Nop.Plugin.CW.Core.Helpers;
using Nop.Services.Localization;
using Nop.Web.Framework.Validators;

namespace Nop.Plugin.CW.Core.Areas.Admin.Validators.Subsidies;

/// <summary>
/// The rules of the subsidy card of Promotions › Reminders.
/// </summary>
/// <remarks>
/// Checked only while the card's switch is on, as the follow-ups are saved only then. nop's own action validates only
/// its parameters, so the card is validated by <see cref="Filters.SubsidyRemindersSaveFilter"/>.
/// </remarks>
public partial class SubsidyExpiryRemindersValidator : BaseNopValidator<SubsidyExpiryRemindersModel>
{
    public SubsidyExpiryRemindersValidator(ILocalizationService localizationService)
    {
        RuleForEach(x => x.FollowUps).ChildRules(followUp =>
        {
            followUp.RuleFor(x => x.DelayBeforeSend)
                .GreaterThan(0)
                .When(x => x.Enabled)
                .WithMessageAwait(localizationService.GetResourceAsync("Plugins.CW.Core.Admin.Reminders.SubsidyExpiry.FollowUp.DelayBeforeSend.MustBeGreaterThanZero"));

            followUp.RuleFor(x => x.DelayBeforeSend)
                .Must((model, delayBeforeSend) => SubsidyExpiryReminderHelper.LeadHours(delayBeforeSend, model.DelayPeriodId) <= SubsidyExpiryReminderHelper.MaximumLeadHours)
                .When(x => x.Enabled)
                .WithMessageAwait(localizationService.GetResourceAsync("Plugins.CW.Core.Admin.Reminders.SubsidyExpiry.FollowUp.DelayBeforeSend.Maximum"));

            followUp.RuleFor(x => x.DelayPeriodId)
                .Must(delayPeriodId => Enum.IsDefined(typeof(MessageDelayPeriod), delayPeriodId))
                .When(x => x.Enabled)
                .WithMessageAwait(localizationService.GetResourceAsync("Plugins.CW.Core.Admin.Reminders.SubsidyExpiry.FollowUp.DelayPeriod.Invalid"));
        }).When(x => x.Enabled);

        RuleFor(x => x.FollowUps)
            .Must(followUps => SubsidyExpiryReminderHelper.LeadTimesDecrease(followUps
                .Select(followUp => (followUp.Enabled && HasValidLeadTime(followUp), SubsidyExpiryReminderHelper.LeadHours(followUp.DelayBeforeSend, followUp.DelayPeriodId)))))
            .When(x => x.Enabled)
            .WithMessageAwait(localizationService.GetResourceAsync("Plugins.CW.Core.Admin.Reminders.SubsidyExpiry.FollowUps.Order"));
    }

    /// <summary>
    /// Whether a follow-up's lead time passes its own rules. One that does not already has its error under the field and
    /// is left out of the order check, so the page shows one error for it, not two.
    /// </summary>
    /// <param name="followUp">The follow-up</param>
    private static bool HasValidLeadTime(SubsidyExpiryFollowUpModel followUp)
    {
        if (followUp.DelayBeforeSend <= 0 || !Enum.IsDefined(typeof(MessageDelayPeriod), followUp.DelayPeriodId))
            return false;

        return SubsidyExpiryReminderHelper.LeadHours(followUp.DelayBeforeSend, followUp.DelayPeriodId) <= SubsidyExpiryReminderHelper.MaximumLeadHours;
    }
}