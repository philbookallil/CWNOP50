using System.ComponentModel.DataAnnotations;
using Nop.Web.Framework.Models;
using Nop.Web.Framework.Mvc.ModelBinding;

namespace Nop.Plugin.CW.Core.Areas.Admin.Models.Subsidies;

/// <summary>
/// The subsidy card of Promotions › Reminders
/// </summary>
public partial record SubsidyExpiryRemindersModel : BaseNopModel
{
    /// <summary>
    /// Gets or sets the store scope the card was prepared for; posted back, it also tells the save filter the card was on
    /// the page
    /// </summary>
    public int ActiveStoreScopeConfiguration { get; set; }

    [NopResourceDisplayName("Plugins.CW.Core.Admin.Reminders.SubsidyExpiry.Enabled")]
    public bool Enabled { get; set; }

    public bool Enabled_OverrideForStore { get; set; }

    [NopResourceDisplayName("Plugins.CW.Core.Admin.Reminders.SubsidyExpiry.StartDate")]
    [UIHint("DateTimeNullable")]
    public DateTime? StartDateUtc { get; set; }

    public bool StartDateUtc_OverrideForStore { get; set; }

    /// <summary>
    /// Gets or sets the three follow-ups, in follow-up order
    /// </summary>
    public List<SubsidyExpiryFollowUpModel> FollowUps { get; set; } = new();

    /// <summary>
    /// Gets or sets a value indicating whether the task that sends the reminders is installed
    /// </summary>
    public bool TaskFound { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the task that sends the reminders is enabled
    /// </summary>
    public bool TaskEnabled { get; set; }

    /// <summary>
    /// Gets or sets the task's name on System › Schedule tasks
    /// </summary>
    public string TaskName { get; set; }
}