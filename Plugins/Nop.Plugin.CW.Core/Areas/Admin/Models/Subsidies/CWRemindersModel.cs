using Nop.Web.Framework.Models;

namespace Nop.Plugin.CW.Core.Areas.Admin.Models.Subsidies;

/// <summary>
/// The CW cards of Promotions › Reminders
/// </summary>
/// <remarks>
/// The card's fields are named through this model's property (<c>SubsidyExpiry.Enabled</c>, …) so that nop's own
/// fields on the same form keep their names, and so that nop's nested-setting tag helper, which reads the property
/// path rather than the HTML field prefix, finds the switch it toggles on.
/// </remarks>
public partial record CWRemindersModel : BaseNopModel
{
    public SubsidyExpiryRemindersModel SubsidyExpiry { get; set; } = new();
}