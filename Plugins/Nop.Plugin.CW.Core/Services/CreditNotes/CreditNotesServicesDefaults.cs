using Nop.Core.Caching;

namespace Nop.Plugin.CW.Core.Services.CreditNotes;

public static class CreditNotesServicesDefaults
{
    /// <summary>
    /// Key for credit note balances by customer Id
    /// </summary>
    /// <remarks>
    /// {0} : Customer id
    /// </remarks>
    public static CacheKey CreditNoteBalanceByCustomerIdCacheKey => new("CW.creditnotes.balance.bycustomerid.{0}");

    /// <summary>
    /// Key for credit note by Id
    /// </summary>
    /// <remarks>
    /// {0} : Credit note id
    /// </remarks>
    public static CacheKey CreditNoteByIdCacheKey => new("CW.creditnotes.byid.{0}");

    /// <summary>
    /// Key for credit note histories by credit note Id
    /// </summary>
    /// <remarks>
    /// {0} : Customer id
    /// </remarks>
    public static CacheKey CustomerHasCreditNotesCacheKey => new("CW.creditnotes.hasany.bycustomerid.{0}");
}