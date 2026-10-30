using Nop.Web.Framework.Models;
using Nop.Web.Framework.Mvc.ModelBinding;
using System.ComponentModel.DataAnnotations;

namespace Nop.Plugin.Payments.NABGateway.Areas.Admin.Models;

/// <summary>
/// Search model shared by the Payments and Failed Transactions reports.
/// </summary>
public record NABTransactionSearchModel : BaseSearchModel
{
    [NopResourceDisplayName("Plugins.Payments.NABGateway.Reports.Fields.StartDate")]
    [UIHint("DateNullable")]
    public DateTime? StartDate { get; set; }

    [NopResourceDisplayName("Plugins.Payments.NABGateway.Reports.Fields.EndDate")]
    [UIHint("DateNullable")]
    public DateTime? EndDate { get; set; }

    [NopResourceDisplayName("Plugins.Payments.NABGateway.Reports.Fields.UserEmail")]
    public string UserEmail { get; set; }

    [NopResourceDisplayName("Plugins.Payments.NABGateway.Reports.Fields.UserName")]
    public string UserName { get; set; }

    [NopResourceDisplayName("Plugins.Payments.NABGateway.Reports.Fields.AuthId")]
    public string AuthId { get; set; }

    [NopResourceDisplayName("Plugins.Payments.NABGateway.Reports.Fields.TxnId")]
    public string TxnId { get; set; }
}
