using Nop.Core.Domain.Messages;

namespace Nop.Plugin.CW.Core.Domain.Subsidies.DTOs;

/// <summary>
/// One follow-up of the subsidy expiry reminders as the admin saves it
/// </summary>
/// <param name="Enabled">Whether the follow-up is sent</param>
/// <param name="DelayBeforeSend">How long before the season ends it is sent, in <paramref name="DelayPeriod"/> units</param>
/// <param name="DelayPeriod">Hours or days</param>
public record SubsidyExpiryFollowUpSetting(bool Enabled, int DelayBeforeSend, MessageDelayPeriod DelayPeriod);