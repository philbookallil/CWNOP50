using Nop.Plugin.CW.Core.Domain.CreditNotes;
using Nop.Services.Caching;

namespace Nop.Plugin.CW.Core.Services.CreditNotes.Caching;

public partial class CreditNoteHistoryCacheEventConsumer : CacheEventConsumer<CreditNoteHistory>
{
    protected override async Task ClearCacheAsync(CreditNoteHistory entity)
    {
        await RemoveAsync(CreditNotesServicesDefaults.CreditNoteBalanceByCustomerIdCacheKey, entity.CustomerId);
        await RemoveAsync(CreditNotesServicesDefaults.CreditNoteByIdCacheKey, entity.CreditNoteId);
        await RemoveAsync(CreditNotesServicesDefaults.CustomerHasCreditNotesCacheKey, entity.CustomerId);

        await base.ClearCacheAsync(entity);
    }
}