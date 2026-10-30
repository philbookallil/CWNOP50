using Nop.Core.Domain.Catalog;
using Nop.Plugin.CW.Core.Domain.BulkOrder;

namespace Nop.Plugin.CW.Core.Services.BulkOrder;

/// <summary>
/// Where the grid's stock numbers come from. Exists so a CW stock integration replaces ONE class instead of
/// editing the factory. The default implementation reads nop and nothing else.
/// </summary>
public interface IBulkOrderAvailabilityProvider
{
    /// <summary>Resolves every stock figure one product's grid needs.</summary>
    /// <param name="product">The product</param>
    /// <param name="combinations">Its combinations, already loaded — do not re-query them</param>
    Task<BulkOrderAvailability> GetAsync(Product product, IList<ProductAttributeCombination> combinations);
}