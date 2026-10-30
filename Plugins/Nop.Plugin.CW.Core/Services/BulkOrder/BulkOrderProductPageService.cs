using Nop.Core;
using Nop.Core.Domain.Catalog;
using Nop.Plugin.CW.Core.Domain.Settings;
using Nop.Plugin.CW.Core.Factories;
using Nop.Plugin.CW.Core.Models.BulkOrder;
using Nop.Plugin.CW.Core.Services.Security;
using Nop.Plugin.CW.Core.Services.Settings;
using Nop.Services.Common;
using Nop.Services.Configuration;
using Nop.Services.Security;

namespace Nop.Plugin.CW.Core.Services.BulkOrder;

public class BulkOrderProductPageService : IBulkOrderProductPageService
{
    #region Fields

    protected readonly IBulkOrderModelFactory _bulkOrderModelFactory;
    protected readonly ICWFeatureService _cwFeatureService;
    protected readonly IGenericAttributeService _genericAttributeService;
    protected readonly IPermissionService _permissionService;
    protected readonly ISettingService _settingService;
    protected readonly IStoreContext _storeContext;

    #endregion

    #region Ctor

    public BulkOrderProductPageService(IBulkOrderModelFactory bulkOrderModelFactory,
        ICWFeatureService cwFeatureService,
        IGenericAttributeService genericAttributeService,
        IPermissionService permissionService,
        ISettingService settingService,
        IStoreContext storeContext)
    {
        _bulkOrderModelFactory = bulkOrderModelFactory;
        _cwFeatureService = cwFeatureService;
        _genericAttributeService = genericAttributeService;
        _permissionService = permissionService;
        _settingService = settingService;
        _storeContext = storeContext;
    }

    #endregion

    #region Utilities

    /// <summary>
    /// The store default, overridden per product by a tri-state GenericAttribute.
    /// </summary>
    /// <remarks>
    /// The store fallback is written out here because nop does not do it: <c>GetAttributeAsync</c> filters
    /// <c>props.Where(x => x.StoreId == storeId)</c> and stops, so a store-agnostic row is invisible to a
    /// store-scoped read. Store-specific first, then store 0, then the setting.
    /// Read straight from <c>GetAttributesForEntityAsync</c> rather than through two
    /// <c>GetAttributeAsync</c> calls — that is what those calls do internally, and going直 direct means the
    /// gate never needs a Product instance. It is one query either way: the list is held in the SHORT-TERM
    /// (per-request) cache keyed by entity id and key group.
    /// That cache is also why this must never run in a loop over a product list — one product per request
    /// is free, fifteen products is fifteen queries.
    /// </remarks>
    protected virtual async Task<bool> IsEnabledForProductAsync(int productId, int storeId, MultiStoreSettings settings)
    {
        var attributes = await _genericAttributeService.GetAttributesForEntityAsync(productId, nameof(Product));

        var value = attributes.FirstOrDefault(a =>
                a.Key == CWCoreDefaults.BULKORDER_SHOW_ON_PRODUCT_PAGE_PROPERTY && a.StoreId == storeId)?.Value;

        if (string.IsNullOrEmpty(value))
            value = attributes.FirstOrDefault(a =>
                a.Key == CWCoreDefaults.BULKORDER_SHOW_ON_PRODUCT_PAGE_PROPERTY && a.StoreId == 0)?.Value;

        return value switch
        {
            "1" => true,
            "0" => false,
            _ => settings.BulkOrderEnableOnProductPage
        };
    }

    /// <summary>
    /// Whether this grid can only ever contain a single quantity cell.
    /// </summary>
    /// <remarks>
    /// <b>Structural, not a count of the boxes on screen.</b> Counting rendered inputs is the obvious
    /// implementation and it is wrong: a cell renders no input when it is out of stock, call-for-price,
    /// awaiting a required pin choice, or needs text the grid cannot take — all of which change with stock
    /// and with what the customer has selected. A product can show zero boxes on load because a
    /// required pin has no default and every cell reads "Choose above"; choose the pin and it becomes a full
    /// grid. A box-count rule would have banished it from its own product page permanently, and it would
    /// have banished any product that happened to be out of stock the moment the page was opened.
    ///
    /// The number of CELLS is fixed by the axes and does not move: <c>IsSingleCell</c> is
    /// <c>grid.Axes.Count == 0</c> (BulkOrderModelFactory :496), which covers a product with no attributes
    /// and a pin-only product alike — in both, every attribute is a control above one box, which is exactly
    /// what nop's own page renders, only in its standard form.
    ///
    /// The Rows/Columns clause covers a degenerate axis with a single value: a 1x1 table. No product in the
    /// measured catalogue has that shape, so this is written for correctness rather than for a known case,
    /// and it is one comparison.
    /// </remarks>
    protected virtual bool RendersAtMostOneCell(BulkOrderGridModel model)
    {
        return model.IsSingleCell || (model.Rows.Count <= 1 && model.Columns.Count <= 1);
    }

    #endregion

    #region Methods

    /// <summary>
    /// The conditions are ordered by cost, cheapest first, because this runs on every product page in the
    /// store and most of them will not qualify. The feature flag and the permission are cached; the
    /// override is one query; the build is the expensive one and comes last.
    /// </summary>
    /// <remarks>
    /// There is deliberately no product-type check here. It would be the second one: the build's very first
    /// act is <c>UnsupportedReasonFor</c>, whose opening line returns <c>Grouped</c> for anything that is
    /// not a simple product (BulkOrderGridBuilder :551). That gate is enforced there,
    /// where the list enforces it too, rather than in two places that can drift.
    /// </remarks>
    public virtual async Task<BulkOrderGridModel> TryPrepareAsync(int productId)
    {
        if (productId <= 0)
            return null;

        if (!await _cwFeatureService.IsEnabledAsync(CWFeature.BulkOrder))
            return null;

        //the same permission the /bulkordering route carries as an attribute (BulkOrderController :47).
        //The grid must not appear on a product page for a customer who could not reach the list.
        if (!await _permissionService.AuthorizeAsync(CWPermissionConfigManager.PUBLIC_ACCESS_BULKORDERING))
            return null;

        var store = await _storeContext.GetCurrentStoreAsync();
        var settings = await _settingService.LoadSettingAsync<MultiStoreSettings>(store.Id);

        if (!await IsEnabledForProductAsync(productId, store.Id, settings))
            return null;

        //No pins: the page has just loaded and nothing has been chosen. Every later selection goes through
        //the existing Grid endpoint, exactly as the list card does.
        var model = await _bulkOrderModelFactory.PrepareGridModelAsync(productId, pins: null);

        //null means the product is not visible in this store; a reason means the grid cannot express it.
        //Both leave the page alone, and the second is the interesting one: on the LIST this renders an
        //unsupported card, because the alternative is the product silently vanishing from a page that
        //counted it. Here there is something to fall back TO - nop's own controls, which work - so replacing
        //them with a card explaining why the customer cannot buy would be a downgrade we delivered.
        //The rule differs from the list because the situation does.
        if (model == null || model.UnsupportedReason != null)
            return null;

        //A grid that can only ever hold ONE quantity box is not a grid, and on a page that already has a
        //perfectly good quantity box it is a worse version of what nop ships. 53 of the 100 products in the
        //test catalogue are this shape. THE LIST IS UNAFFECTED: there the single cell is how a product with
        //no attributes takes part in a page of matrices, and removing it would drop the product from a page
        //that counted it. Here the product has a page of its own.
        if (RendersAtMostOneCell(model))
            return null;

        return model;
    }

    #endregion
}