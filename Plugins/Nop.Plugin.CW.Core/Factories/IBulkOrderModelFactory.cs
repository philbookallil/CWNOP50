using Nop.Plugin.CW.Core.Models.BulkOrder;

namespace Nop.Plugin.CW.Core.Factories;

public interface IBulkOrderModelFactory
{
    /// <summary>Prepares the page shell and the first page of products, rendered server-side.</summary>
    Task<BulkOrderingModel> PrepareShellModelAsync(BulkOrderCommand command);

    /// <summary>Prepares the paged product list.</summary>
    Task<BulkOrderingModel> PrepareItemsModelAsync(BulkOrderCommand command);

    /// <summary>
    /// Prepares the bulk ordering category navigation. Takes the page model rather than loose values so
    /// the category links carry the whole request state.
    /// </summary>
    Task<BulkOrderCategoryNavigationModel> PrepareCategoryNavigationModelAsync(BulkOrderingModel bulkOrdering);

    /// <summary>Prepares one product's grid for a pin change. Null when the product is not visible.</summary>
    Task<BulkOrderGridModel> PrepareGridModelAsync(int productId, string pins);
}