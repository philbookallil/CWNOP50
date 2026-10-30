using Nop.Plugin.Payments.NABGateway.Areas.Admin.Models;

namespace Nop.Plugin.Payments.NABGateway.Areas.Admin.Factories;

/// <summary>
/// Builds the models for the NAB XML transaction reports.
/// </summary>
public interface INABXMLTransactionModelFactory
{
    /// <summary>
    /// Prepares the paged grid model.
    /// </summary>
    /// <param name="approved">Filter by approval state (true = Payments report, false = Failed report).</param>
    Task<NABTransactionListModel> PrepareTransactionListModelAsync(NABTransactionSearchModel searchModel, bool approved);

    /// <summary>
    /// Prepares the detail model for a single transaction, or <c>null</c> if not found.
    /// </summary>
    Task<NABTransactionModel> PrepareTransactionDetailsModelAsync(int id);
}
