using Nop.Plugin.CW.Core.Areas.Admin.Models.CreditNotes;

namespace Nop.Plugin.CW.Core.Areas.Admin.Factories;

public interface ICreditNotesModelFactory
{
    Task<CreditNoteAddFormModel> PrepareCreditNoteAddFormModelAsync(int? customerId = null);
    Task<CreditNoteListModel> PrepareCreditNoteListModelAsync(CreditNoteSearchModel searchModel);
    Task<CreditNoteHistoryListModel> PrepareCreditNoteHistoryListModelAsync(CreditNoteHistorySearchModel searchModel);
    Task<CreditNotesCustomerSummaryModel> PrepareCreditNotesCustomerSummaryModelAsync(int customerId);

    // Liability report
    Task<CreditNotesMainReportSearchModel> PrepareCreditNotesReportModelAsync();
    Task<StoreLiabilityListModel> PrepareStoreLiabilityListModelAsync(StoreLiabilitySearchModel searchModel);
    Task<StoreCustomerCreditNotesListModel> PrepareStoreCustomerCreditNotesListModelAsync(StoreLiabilitySearchModel searchModel);
}
