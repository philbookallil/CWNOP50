using System.ComponentModel.DataAnnotations;
using Nop.Web.Framework.Models;
using Nop.Web.Framework.Mvc.ModelBinding;

namespace Nop.Plugin.CW.Core.Areas.Admin.Models.Approvals;

/// <summary>
/// The approval panel of a branch or a department
/// </summary>
public record ApprovalRuleModel : BaseNopModel
{
    /// <summary>
    /// Gets or sets a value indicating whether the form has the panel. Posted with it, so a form loaded without the
    /// panel (Approvals off) never saves an empty rule over the one it did not show.
    /// </summary>
    public bool Available { get; set; }

    [NopResourceDisplayName("Plugins.CW.Core.Admin.ApprovalRule.Required")]
    public bool Required { get; set; }

    [UIHint("DecimalNullable")]
    [NopResourceDisplayName("Plugins.CW.Core.Admin.ApprovalRule.Threshold")]
    public decimal? Threshold { get; set; }

    [NopResourceDisplayName("Plugins.CW.Core.Admin.ApprovalRule.Emails")]
    public string Emails { get; set; }
}
