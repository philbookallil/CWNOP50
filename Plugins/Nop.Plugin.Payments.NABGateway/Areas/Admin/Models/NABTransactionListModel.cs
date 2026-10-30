using Nop.Web.Framework.Models;

namespace Nop.Plugin.Payments.NABGateway.Areas.Admin.Models;

/// <summary>
/// Paged list model for the NAB transaction reports.
/// </summary>
public partial record NABTransactionListModel : BasePagedListModel<NABTransactionModel>;
