using Nop.Core.Domain.Catalog;
using Nop.Plugin.CW.Core.Domain.BulkOrder;

namespace Nop.Plugin.CW.Core.Services.BulkOrder;

public interface IBulkOrderGridBuilder
{
    /// <summary>
    /// Builds one product's grid: classification, the combination index, axes, columns, rows and cells
    /// </summary>
    /// <param name="product">Product</param>
    /// <param name="pinSelections">Chosen pin values as mappingId -> valueIds; may be null or empty</param>
    /// <param name="axisAttributeIds">Configured axis priority order (MultiStoreSettings.BulkOrderAxisAttributeIds)</param>
    /// <param name="maxCells">Cell budget (MultiStoreSettings.BulkOrderMaxCellsPerProduct)</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    Task<BulkOrderGrid> BuildAsync(Product product,
        IDictionary<int, IList<int>> pinSelections,
        IList<int> axisAttributeIds,
        int maxCells,
        bool showAttributeImages);
}