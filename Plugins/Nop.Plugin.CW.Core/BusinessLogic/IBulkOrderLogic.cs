using Nop.Plugin.CW.Core.BusinessLogic.Requests;

namespace Nop.Plugin.CW.Core.BusinessLogic;

/// <summary>Turns a bulk request into cart lines, and reports what each one did.</summary>
public interface IBulkOrderLogic
{
    Task<BulkOrderAddResult> AddToCartAsync(BulkOrderAddRequest request);
}