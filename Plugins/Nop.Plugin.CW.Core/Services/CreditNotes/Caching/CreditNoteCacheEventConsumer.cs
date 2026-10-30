using Nop.Plugin.CW.Core.Domain.CreditNotes;
using Nop.Services.Caching;

namespace Nop.Plugin.CW.Core.Services.CreditNotes.Caching;

public partial class CreditNoteCacheEventConsumer : CacheEventConsumer<CreditNote>
{
    protected override async Task ClearCacheAsync(CreditNote entity)
    {
        await RemoveAsync(CreditNotesServicesDefaults.CreditNoteBalanceByCustomerIdCacheKey, entity.CustomerId);
        await RemoveAsync(CreditNotesServicesDefaults.CreditNoteByIdCacheKey, entity.Id);
        await RemoveAsync(CreditNotesServicesDefaults.CustomerHasCreditNotesCacheKey, entity.CustomerId);

        await base.ClearCacheAsync(entity);
    }
}