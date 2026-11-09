using AwesomeAssertions;
using Moq;
using Nop.Core.Domain.Messages;
using Nop.Plugin.CW.Core.Areas.Admin.Models.Subsidies;
using Nop.Plugin.CW.Core.Areas.Admin.Validators.Subsidies;
using Nop.Services.Localization;
using NUnit.Framework;

namespace CW.Tests.Core.Validators;

/// <summary>
/// The rules of the subsidy card of Promotions › Reminders: each enabled follow-up has a lead time between an hour and
/// a year, and #1 is sent the longest before the end, #3 the shortest; nothing is checked while the card is off
/// </summary>
[TestFixture]
public class SubsidyExpiryRemindersValidatorTests
{
    private static SubsidyExpiryRemindersValidator Validator()
    {
        var localizationService = new Mock<ILocalizationService>();
        localizationService.Setup(service => service.GetResourceAsync(It.IsAny<string>())).ReturnsAsync((string key) => key);

        return new SubsidyExpiryRemindersValidator(localizationService.Object);
    }

    private static SubsidyExpiryFollowUpModel FollowUp(int number, bool enabled, int delay, MessageDelayPeriod period = MessageDelayPeriod.Days)
        => new() { Number = number, Enabled = enabled, DelayBeforeSend = delay, DelayPeriodId = (int)period };

    private static SubsidyExpiryRemindersModel Card(bool enabled, params SubsidyExpiryFollowUpModel[] followUps)
        => new() { Enabled = enabled, FollowUps = followUps.ToList() };

    [Test]
    public async Task The_default_lead_times_pass()
    {
        var result = await Validator().ValidateAsync(Card(true, FollowUp(1, true, 30), FollowUp(2, true, 7), FollowUp(3, true, 1)));

        result.Errors.Should().BeEmpty();
    }

    [Test]
    public async Task Hours_and_days_are_compared_as_hours()
    {
        var result = await Validator().ValidateAsync(Card(true, FollowUp(1, true, 2), FollowUp(2, true, 36, MessageDelayPeriod.Hours), FollowUp(3, true, 12, MessageDelayPeriod.Hours)));

        result.Errors.Should().BeEmpty();
    }

    [Test]
    public async Task Follow_ups_out_of_order_are_refused()
    {
        var result = await Validator().ValidateAsync(Card(true, FollowUp(1, true, 1), FollowUp(2, true, 7), FollowUp(3, true, 30)));

        result.Errors.Should().ContainSingle()
            .Which.PropertyName.Should().Be(nameof(SubsidyExpiryRemindersModel.FollowUps));
    }

    [Test]
    public async Task A_disabled_follow_up_does_not_count_in_the_order()
    {
        var result = await Validator().ValidateAsync(Card(true, FollowUp(1, true, 30), FollowUp(2, false, 60), FollowUp(3, true, 1)));

        result.Errors.Should().BeEmpty();
    }

    [Test]
    public async Task An_enabled_follow_up_needs_a_lead_time()
    {
        var result = await Validator().ValidateAsync(Card(true, FollowUp(1, true, 30), FollowUp(2, true, 0), FollowUp(3, false, 0)));

        result.Errors.Select(error => error.PropertyName).Should().Contain("FollowUps[1].DelayBeforeSend");
    }

    [Test]
    public async Task A_follow_up_without_a_lead_time_shows_one_error_not_two()
    {
        //#2 at 0 would also put #3 (1 day) after it; its own error is enough
        var result = await Validator().ValidateAsync(Card(true, FollowUp(1, true, 30), FollowUp(2, true, 0), FollowUp(3, true, 1)));

        result.Errors.Should().ContainSingle()
            .Which.PropertyName.Should().Be("FollowUps[1].DelayBeforeSend");
    }

    [Test]
    public async Task A_lead_time_over_a_year_is_refused()
    {
        var result = await Validator().ValidateAsync(Card(true, FollowUp(1, true, 400), FollowUp(2, false, 0), FollowUp(3, false, 0)));

        result.Errors.Select(error => error.PropertyName).Should().Contain("FollowUps[0].DelayBeforeSend");
    }

    [Test]
    public async Task A_period_that_is_neither_hours_nor_days_is_refused()
    {
        var followUp = FollowUp(1, true, 3);
        followUp.DelayPeriodId = 7;

        var result = await Validator().ValidateAsync(Card(true, followUp));

        result.Errors.Select(error => error.PropertyName).Should().Contain("FollowUps[0].DelayPeriodId");
    }

    [Test]
    public async Task Nothing_is_checked_while_the_card_is_off()
    {
        //the follow-ups are not saved while the switch is off, as nop's own cards
        var result = await Validator().ValidateAsync(Card(false, FollowUp(1, true, 0), FollowUp(2, true, 7), FollowUp(3, true, 30)));

        result.Errors.Should().BeEmpty();
    }
}
