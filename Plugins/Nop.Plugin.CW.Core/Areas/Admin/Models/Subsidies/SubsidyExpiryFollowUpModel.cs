using Nop.Web.Framework.Models;
using Nop.Web.Framework.Mvc.ModelBinding;

namespace Nop.Plugin.CW.Core.Areas.Admin.Models.Subsidies;

/// <summary>
/// One follow-up of the subsidy card of Promotions › Reminders
/// </summary>
public partial record SubsidyExpiryFollowUpModel : BaseNopModel
{
    /// <summary>
    /// Gets or sets the follow-up number (1 to 3); shown in the label, never posted
    /// </summary>
    public int Number { get; set; }

    [NopResourceDisplayName("Plugins.CW.Core.Admin.Reminders.SubsidyExpiry.FollowUp.Enabled")]
    public bool Enabled { get; set; }

    [NopResourceDisplayName("Plugins.CW.Core.Admin.Reminders.SubsidyExpiry.FollowUp.DelayBeforeSend")]
    public int DelayBeforeSend { get; set; }

    public int DelayPeriodId { get; set; }
}