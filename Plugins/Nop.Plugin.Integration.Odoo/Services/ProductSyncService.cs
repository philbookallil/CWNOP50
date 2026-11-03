using Microsoft.AspNetCore.StaticFiles;
using Nop.Core;
using Nop.Core.Domain.Catalog;
using Nop.Core.Domain.Media;
using Nop.Core.Infrastructure;
using Nop.Plugin.Integration.Odoo.Domain;
using Nop.Plugin.Integration.Odoo.Models;
using Nop.Plugin.Integration.Odoo.Models.Responses;
using Nop.Plugin.CW.Core.Services.MultiStores;
using Nop.Plugin.CW.Core.Services.Security;
using Nop.Services.Catalog;
using Nop.Services.Common;
using Nop.Services.Media;
using Nop.Services.Seo;
using Nop.Services.Stores;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using Nop.Data;

namespace Nop.Plugin.Integration.Odoo.Services;

public class ProductSyncService : IProductSyncService
{
    #region Fields

    private readonly IProductService _productService;
    private readonly IPictureService _pictureService;
    private readonly IProductAttributeService _productAttributeService;
    private readonly ICategoryService _categoryService;
    private readonly IUrlRecordService _urlRecordService;
    private readonly IStoreService _storeService;
    private readonly IStoreMappingService _storeMappingService;
    private readonly IOdooLogService _odooLog;
    private readonly ISpecificationAttributeService _specificationAttributeService;
    private readonly INopFileProvider _fileProvider;
    private readonly IOdooApiService _odooApiService;
    private readonly IOdooStoreMapService _storeMapService;
    private readonly IMultiStoreService _multiStoreService;
    private readonly IAclOverrideService _aclService;
    private readonly IGenericAttributeService _genericAttributeService;
    private readonly IRepository<Product> _productRepository;

    /// <summary>
    /// Prefix of the Odoo link kept in <c>ManufacturerPartNumber</c>: <c>ODOO_{template id}</c> on a product,
    /// <c>ODOO_{product.product id}</c> on a combination. It is the match key, ahead of SKU.
    /// </summary>
    private const string OdooLinkPrefix = "ODOO_";

    /// <summary>Per-run lookup of Odoo link (<c>ODOO_{template id}</c>) → nop product id, loaded once.</summary>
    private Dictionary<string, int> _productIdByOdooLink = new();

    /// <summary>Per-run lookup of Odoo departments by id (for product→department ACL).</summary>
    private Dictionary<int, OdooDepartmentResponse> _odooDeptById = new();

    /// <summary>
    /// Per-run lookup of Odoo store id → nop store id, loaded once so resolving a product's stores
    /// does not query the mapping table per store per product.
    /// </summary>
    private Dictionary<int, int> _nopStoreIdByOdooStoreId = new();

    /// <summary>Per-run cache: Odoo department id → resolved nop department id (null = unresolved).</summary>
    private readonly Dictionary<int, int?> _nopDeptIdCache = new();

    /// <summary>
    /// Generic-attribute key storing the product's last synced Odoo change marker
    /// (<c>x_studio_sync_revision|write_date</c>, see <see cref="GetSyncMarker"/>).
    /// </summary>
    private const string OdooProductSyncMarkerAttribute = "Odoo.ProductSyncMarker";

    /// <summary>
    /// Generic-attribute key storing a fingerprint of the Odoo main image last seen for a product,
    /// see <see cref="GetImageFingerprint"/>.
    /// </summary>
    private const string OdooImageFingerprintAttribute = "Odoo.MainImageFingerprint";

    #endregion

    #region Ctor
    public ProductSyncService(
        IProductService productService,
        IStoreService storeService,
        IStoreMappingService storeMappingService,
        IOdooLogService odooLogService,
        IProductAttributeService productAttributeService,
        IPictureService pictureService,
        ICategoryService categoryService,
        IUrlRecordService urlRecordService,
        ISpecificationAttributeService specificationAttributeService,
        INopFileProvider fileProvider,
        IOdooApiService odooApiService,
        IOdooStoreMapService storeMapService,
        IMultiStoreService multiStoreService,
        IAclOverrideService aclService,
        IGenericAttributeService genericAttributeService,
        IRepository<Product> productRepository)
    {
        _productService = productService;
        _storeService = storeService;
        _storeMappingService = storeMappingService;
        _odooLog = odooLogService;
        _productAttributeService = productAttributeService;
        _pictureService = pictureService;
        _categoryService = categoryService;
        _urlRecordService = urlRecordService;
        _specificationAttributeService = specificationAttributeService;
        _fileProvider = fileProvider;
        _odooApiService = odooApiService;
        _storeMapService = storeMapService;
        _multiStoreService = multiStoreService;
        _aclService = aclService;
        _genericAttributeService = genericAttributeService;
        _productRepository = productRepository;
    }
    #endregion

    #region Utilities

    #region Product sync

    /// <summary>
    /// Unpublishes NOP products whose Odoo template is archived. Never soft-deletes: a deleted
    /// product is invisible to <c>GetProductBySkuAsync</c>, so reactivating it in Odoo would create a
    /// duplicate. The stored sync marker is cleared so a reactivated product is fully re-synced.
    /// Adds to <paramref name="tally"/>: Updated when unpublished, Skipped when already unpublished or
    /// failed. Archived templates that are not in NOP are not counted.
    /// </summary>
    private async Task UnpublishArchivedProductsAsync(int logEventId, SyncTally tally)
    {
        const int pageSize = 200;
        var offset = 0;
        var unpublished = 0;

        while (true)
        {
            var archived = await _odooApiService.FetchArchivedProductsAsync(pageSize, offset);
            if (archived.Count == 0)
                break;

            await LogUnreadableProductsAsync(logEventId, archived, tally);

            foreach (var odooProduct in archived.Records)
            {
                var outcome = SyncOutcome.Skipped;
                var counted = false;
                try
                {
                    var sku = GetOdooSku(odooProduct);
                    var (product, _) = await FindExistingProductAsync(odooProduct);
                    if (product == null)
                        continue;

                    counted = true;

                    // Force a full re-sync if the product is reactivated in Odoo later.
                    await _genericAttributeService.SaveAttributeAsync<string>(product, OdooProductSyncMarkerAttribute, null);

                    if (!product.Published)
                        continue;

                    product.Published = false;
                    await _productService.UpdateProductAsync(product);
                    unpublished++;

                    await _odooLog.InformationAsync(
                        $"Unpublished product '{product.Name}' (SKU {sku}, ID {product.Id}) — archived in Odoo.",
                        logEventId, entityType: "Product", odooEntityId: odooProduct.Id, nopEntityId: product.Id);
                    outcome = SyncOutcome.Updated;
                }
                catch (Exception ex)
                {
                    counted = true;
                    await _odooLog.ErrorAsync($"Error unpublishing archived Odoo product {odooProduct.Id}: {ex.Message}",
                        logEventId, fullMessage: ex.ToString(), entityType: "Product", odooEntityId: odooProduct.Id);
                }
                finally
                {
                    if (counted)
                        tally.Add(outcome);
                }
            }

            if (archived.Count < pageSize)
                break;

            offset += pageSize;
        }

        if (unpublished > 0)
            await _odooLog.InformationAsync($"Unpublished {unpublished} product(s) archived in Odoo.",
                logEventId, entityType: "ProductSync");
    }

    /// <summary>
    /// Logs each product on <paramref name="page"/> that Odoo returned in a shape the plugin cannot
    /// read, and counts it as Skipped — the rest of the page has already been synced without it.
    /// </summary>
    private async Task LogUnreadableProductsAsync(int logEventId, OdooRecordPage<OdooProductResponse> page, SyncTally tally)
    {
        foreach (var error in page.Errors)
        {
            var product = error.OdooId is { } id ? $"Odoo product {id}" : "An Odoo product (id unreadable)";
            await _odooLog.ErrorAsync(
                $"{product} could not be read and was skipped: {error.Message}",
                logEventId, entityType: "Product", odooEntityId: error.OdooId);

            tally.Add(SyncOutcome.Skipped);
        }
    }

    /// <summary>
    /// The NOP SKU for an Odoo template: its default_code, else its Odoo id.
    /// </summary>
    private static string GetOdooSku(OdooProductResponse odooProduct) =>
        !string.IsNullOrWhiteSpace(odooProduct.DefaultCode) ? odooProduct.DefaultCode : odooProduct.Id.ToString();

    /// <summary>
    /// The Odoo link kept in <c>ManufacturerPartNumber</c>: <c>ODOO_{id}</c> (template id on a product,
    /// product.product id on a combination).
    /// </summary>
    private static string OdooLink(int odooId) => OdooLinkPrefix + odooId;

    /// <summary>
    /// Loads every non-deleted product carrying an Odoo link, as link → product id, in one query.
    /// If two products carry the same link, the older one wins.
    /// </summary>
    private async Task<Dictionary<string, int>> LoadOdooLinkedProductsAsync()
    {
        var linked = await _productRepository.Table
            .Where(p => !p.Deleted && p.ManufacturerPartNumber != null && p.ManufacturerPartNumber.StartsWith(OdooLinkPrefix))
            .Select(p => new { p.Id, p.ManufacturerPartNumber })
            .ToListAsync();

        return linked
            .GroupBy(p => p.ManufacturerPartNumber, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Min(p => p.Id), StringComparer.Ordinal);
    }

    /// <summary>
    /// Finds the NOP product for an Odoo template: by its Odoo link first, else by SKU — but never a
    /// product already linked to a different template (two templates sharing a code stay two products).
    /// </summary>
    /// <returns>The product, or null; and, when the SKU match was refused, the other template's link.</returns>
    private async Task<(Product Product, string SkuTakenBy)> FindExistingProductAsync(OdooProductResponse odooProduct)
    {
        var link = OdooLink(odooProduct.Id);

        if (_productIdByOdooLink.TryGetValue(link, out var linkedId))
        {
            var linked = await _productService.GetProductByIdAsync(linkedId);
            if (linked is { Deleted: false })
                return (linked, null);
        }

        var bySku = await _productService.GetProductBySkuAsync(GetOdooSku(odooProduct));
        if (bySku == null)
            return (null, null);

        var otherLink = bySku.ManufacturerPartNumber;
        if (!string.IsNullOrEmpty(otherLink)
            && otherLink.StartsWith(OdooLinkPrefix, StringComparison.Ordinal)
            && !string.Equals(otherLink, link, StringComparison.Ordinal))
            return (null, otherLink);

        return (bySku, null);
    }

    /// <summary>
    /// Synchronizes an Odoo product with the local product database, updating or creating records as needed.
    /// </summary>
    /// <param name="odooProduct">The Odoo product data to synchronize.</param>
    /// <returns>What happened to the product, for the run's totals.</returns>
    private async Task<SyncOutcome> SyncOdooProductAsync(int logEventId, OdooProductResponse odooProduct)
    {
        try
        {
            if (odooProduct == null)
            {
                await _odooLog.WarningAsync("Cannot sync product - OdooProductResponse is null", logEventId, entityType: "Product");
                return SyncOutcome.Skipped;
            }

            if (string.IsNullOrWhiteSpace(odooProduct.DisplayName) && string.IsNullOrWhiteSpace(odooProduct.Name))
            {
                await _odooLog.WarningAsync($"Skipped product (ID: {odooProduct.Id}) - Product name is required.", logEventId,
                    entityType: "Product", odooEntityId: odooProduct.Id);
                return SyncOutcome.Skipped;
            }

            var odooSku = GetOdooSku(odooProduct);

            // Find the product by its Odoo link first, then by SKU
            var (existingProduct, skuTakenBy) = await FindExistingProductAsync(odooProduct);
            if (skuTakenBy != null)
                await _odooLog.WarningAsync(
                    $"SKU {odooSku} is already used by a product linked to another Odoo template ({skuTakenBy}); creating a separate product for Odoo template {odooProduct.Id}.",
                    logEventId, entityType: "Product", odooEntityId: odooProduct.Id);

            if (existingProduct != null)
            {
                // Incremental: skip only when BOTH the revision and write_date match the last sync.
                var storedMarker = await _genericAttributeService.GetAttributeAsync<string>(existingProduct, OdooProductSyncMarkerAttribute);
                var currentMarker = GetSyncMarker(odooProduct);
                if (!string.IsNullOrEmpty(storedMarker) && string.Equals(storedMarker, currentMarker, StringComparison.Ordinal))
                {
                    await _odooLog.InformationAsync($"Product SKU {odooSku} unchanged ({currentMarker}). Skipped.",
                        logEventId, entityType: "Product", odooEntityId: odooProduct.Id, nopEntityId: existingProduct.Id);
                    return SyncOutcome.Skipped;
                }

                await _odooLog.InformationAsync($"Product with SKU {odooSku} already exists (ID: {existingProduct.Id}). Updating.",
                    logEventId, entityType: "Product", odooEntityId: odooProduct.Id, nopEntityId: existingProduct.Id);
                await UpdateExistingProduct(logEventId, existingProduct, odooProduct);
                await CheckMainImageOnUpdateAsync(logEventId, existingProduct, odooProduct);
                var attributesSynced = await SyncAttributesAndVariants(logEventId, existingProduct, odooProduct);
                await SyncProductCategory(logEventId, existingProduct, odooProduct);
                await SyncVisibilityAndMarkSyncedAsync(logEventId, existingProduct, odooProduct, attributesSynced);

                return SyncOutcome.Updated;
            }

            // Create new product
            var product = MapOdooToNopProduct(odooProduct, odooSku);
            await _productService.InsertProductAsync(product);
            _productIdByOdooLink[product.ManufacturerPartNumber] = product.Id;

            //search engine name
            var seName = await _urlRecordService.ValidateSeNameAsync(product, "", product.Name, true);
            await _urlRecordService.SaveSlugAsync(product, seName, 0);
            await _odooLog.InformationAsync($"Product created: {product.Name} (SKU: {product.Sku}, ID: {product.Id})",
                logEventId, entityType: "Product", odooEntityId: odooProduct.Id, nopEntityId: product.Id);

            // Sync main image
            if (!string.IsNullOrWhiteSpace(odooProduct.Image1920))
                await SyncProductMainImageAsync(logEventId, product, odooProduct.Image1920, product.Name);
            await _genericAttributeService.SaveAttributeAsync(product, OdooImageFingerprintAttribute, GetImageFingerprint(odooProduct));

            // Sync attributes and variants
            var attributesCreated = await SyncAttributesAndVariants(logEventId, product, odooProduct);

            // Sync category
            await SyncProductCategory(logEventId, product, odooProduct);

            // Sync store mapping (x_studio_stores) and department ACL (x_studio_departments)
            await SyncVisibilityAndMarkSyncedAsync(logEventId, product, odooProduct, attributesCreated);

            return SyncOutcome.Created;
        }
        catch (Exception ex)
        {
            await _odooLog.ErrorAsync($"Error during product sync for product {odooProduct?.Id}_{odooProduct?.Name}: {ex.Message}",
                logEventId, fullMessage: ex.ToString(), entityType: "Product", odooEntityId: odooProduct?.Id);
            return SyncOutcome.Skipped;
        }
    }

    #endregion

    #region Product Store Mapping Sync

    /// <summary>
    /// Applies the product's store visibility and department ACL, then records it as synced — but
    /// only when both resolved completely and the attributes/variants step completed
    /// (<paramref name="attributesSynced"/>). Otherwise the marker is left as it was, so the product is
    /// re-processed on the next run (it stays restricted meanwhile; both visibility steps fail closed).
    /// </summary>
    private async Task SyncVisibilityAndMarkSyncedAsync(int logEventId, Product product, OdooProductResponse odooProduct,
        bool attributesSynced)
    {
        var storesResolved = await SyncProductStoreMappingAsync(logEventId, product, odooProduct);
        var departmentsResolved = await SyncProductDepartmentsAsync(logEventId, product, odooProduct);

        if (storesResolved && departmentsResolved && attributesSynced)
            await SaveProductSyncMarkerAsync(product, odooProduct);
    }

    /// <summary>
    /// Maps the product to nopCommerce store(s) from Odoo's <c>x_studio_stores</c> (m2m → x_store).
    /// Each Odoo store id is resolved to a nop store id via <see cref="IOdooStoreMapService"/>, then
    /// <see cref="IStoreMappingService.SaveStoreMappingsAsync{TEntity}"/> sets <c>LimitedToStores</c>,
    /// inserts new mappings, and removes stale ones. An empty Odoo list leaves the product
    /// unrestricted (visible in all stores, D10).
    ///
    /// Fails closed: when some Odoo stores do not resolve, the product is limited to the ones that
    /// did — to none if none did — and is never opened up by the failure.
    /// </summary>
    /// <returns>True when every Odoo store resolved; false otherwise, and then the product must not be
    /// recorded as synced so it is retried on the next run.</returns>
    private async Task<bool> SyncProductStoreMappingAsync(int logEventId, Product nopProduct, OdooProductResponse odooProduct)
    {
        try
        {
            var odooStoreIds = odooProduct.XStudioStores ?? new List<int>();
            var matchedStoreIds = new List<int>();

            foreach (var odooStoreId in odooStoreIds)
            {
                if (_nopStoreIdByOdooStoreId.TryGetValue(odooStoreId, out var nopStoreId))
                    matchedStoreIds.Add(nopStoreId);
                else
                    await _odooLog.WarningAsync(
                        $"_____Odoo store {odooStoreId} for product {odooProduct.Id} is not mapped (run store sync first)",
                        logEventId, entityType: "StoreMapping", odooEntityId: odooProduct.Id, nopEntityId: nopProduct.Id);
            }

            await _storeMappingService.SaveStoreMappingsAsync(nopProduct, matchedStoreIds);

            // SaveStoreMappingsAsync clears LimitedToStores for an empty list. That is right when Odoo
            // lists no stores, and exposes the product everywhere when Odoo lists stores none of which
            // resolved — so restrict it to no store instead.
            if (odooStoreIds.Count > 0 && matchedStoreIds.Count == 0 && !nopProduct.LimitedToStores)
            {
                nopProduct.LimitedToStores = true;
                await _productService.UpdateProductAsync(nopProduct);
            }

            var unresolved = odooStoreIds.Count - matchedStoreIds.Count;
            if (unresolved > 0)
                await _odooLog.WarningAsync(
                    $"_____{unresolved} of {odooStoreIds.Count} Odoo store(s) for product '{nopProduct.Name}' (ID: {nopProduct.Id}) did not resolve — limited to " +
                    (matchedStoreIds.Any() ? $"store(s) {string.Join(", ", matchedStoreIds)}" : "no store (hidden)") + " until they do; retried next sync.",
                    logEventId, entityType: "StoreMapping", odooEntityId: odooProduct.Id, nopEntityId: nopProduct.Id);
            else if (matchedStoreIds.Any())
                await _odooLog.InformationAsync(
                    $"_____Store mapping applied for product '{nopProduct.Name}' (ID: {nopProduct.Id}): limited to store(s) {string.Join(", ", matchedStoreIds)}",
                    logEventId, entityType: "StoreMapping", odooEntityId: odooProduct.Id, nopEntityId: nopProduct.Id);
            else
                await _odooLog.InformationAsync(
                    $"_____Product '{nopProduct.Name}' (ID: {nopProduct.Id}) has no store limitation (visible in all stores).",
                    logEventId, entityType: "StoreMapping", odooEntityId: odooProduct.Id, nopEntityId: nopProduct.Id);

            return unresolved == 0;
        }
        catch (Exception ex)
        {
            await _odooLog.ErrorAsync(
                $"_____Error syncing store mapping for product {odooProduct.Id}: {ex.Message}",
                logEventId, fullMessage: ex.ToString(), entityType: "StoreMapping", odooEntityId: odooProduct.Id);
            return false;
        }
    }

    /// <summary>
    /// Maps the product to CW.Core department(s) from Odoo's <c>x_studio_departments</c> (m2m →
    /// x_department). Each Odoo department id is resolved to a nop department id, then CW.Core's
    /// <see cref="IAclOverrideService.SaveAclDepartmentAsync{TEntity}"/> sets <c>SubjectToAcl</c>,
    /// inserts new department ACL records, and removes stale ones. An empty Odoo list clears the
    /// department ACL (product not department-restricted).
    ///
    /// Fails closed: when some Odoo departments do not resolve, the product is restricted to the ones
    /// that did — to nobody if none did — and is never opened up by the failure.
    /// </summary>
    /// <returns>True when every Odoo department resolved; false otherwise, and then the product must not
    /// be recorded as synced so it is retried on the next run.</returns>
    private async Task<bool> SyncProductDepartmentsAsync(int logEventId, Product nopProduct, OdooProductResponse odooProduct)
    {
        try
        {
            var odooDeptIds = odooProduct.XStudioDepartments ?? new List<int>();
            var nopDeptIds = new List<int>();

            foreach (var odooDeptId in odooDeptIds)
            {
                // Logs its own warning when the department cannot be resolved.
                var nopDeptId = await ResolveNopDepartmentIdAsync(logEventId, odooDeptId, odooProduct.Id);
                if (nopDeptId.HasValue)
                    nopDeptIds.Add(nopDeptId.Value);
            }

            await _aclService.SaveAclDepartmentAsync(nopProduct, nopDeptIds);

            // SaveAclDepartmentAsync clears SubjectToAcl for an empty list (unless other roles remain).
            // That is right when Odoo lists no departments, and shows the product to every customer when
            // Odoo lists departments none of which resolved — so keep it subject to ACL with no dept roles.
            if (odooDeptIds.Count > 0 && nopDeptIds.Count == 0 && !nopProduct.SubjectToAcl)
            {
                nopProduct.SubjectToAcl = true;
                await _productService.UpdateProductAsync(nopProduct);
            }

            var unresolved = odooDeptIds.Count - nopDeptIds.Count;
            if (unresolved > 0)
                await _odooLog.WarningAsync(
                    $"_____{unresolved} of {odooDeptIds.Count} Odoo department(s) for product '{nopProduct.Name}' (ID: {nopProduct.Id}) did not resolve — restricted to " +
                    (nopDeptIds.Any() ? $"department(s) {string.Join(", ", nopDeptIds)}" : "no department (hidden)") + " until they do; retried next sync.",
                    logEventId, entityType: "Department", odooEntityId: odooProduct.Id, nopEntityId: nopProduct.Id);
            else if (nopDeptIds.Any())
                await _odooLog.InformationAsync(
                    $"_____Department ACL applied for product '{nopProduct.Name}' (ID: {nopProduct.Id}): department(s) {string.Join(", ", nopDeptIds)}",
                    logEventId, entityType: "Department", odooEntityId: odooProduct.Id, nopEntityId: nopProduct.Id);

            return unresolved == 0;
        }
        catch (Exception ex)
        {
            await _odooLog.ErrorAsync(
                $"_____Error syncing department ACL for product {odooProduct.Id}: {ex.Message}",
                logEventId, fullMessage: ex.ToString(), entityType: "Department", odooEntityId: odooProduct.Id);
            return false;
        }
    }

    /// <summary>
    /// Resolves an Odoo department id to a nop department id: look up the Odoo department (id → store,
    /// name), resolve its nop store via the store mapping, then find the CW.Core department by
    /// (StoreId, Name). Cached per sync run. Returns null if it cannot be resolved.
    /// </summary>
    private async Task<int?> ResolveNopDepartmentIdAsync(int logEventId, int odooDeptId, int odooProductId)
    {
        if (_nopDeptIdCache.TryGetValue(odooDeptId, out var cached))
            return cached;

        int? resolved = null;

        if (_odooDeptById.TryGetValue(odooDeptId, out var odooDept) && odooDept.XStudioStore != null)
        {
            var name = (odooDept.XName ?? odooDept.DisplayName)?.Trim();

            if (_nopStoreIdByOdooStoreId.TryGetValue(odooDept.XStudioStore.Id, out var nopStoreId)
                && !string.IsNullOrWhiteSpace(name))
            {
                var dept = await _multiStoreService.GetDepartmentByStoreAndNameAsync(nopStoreId, name);
                resolved = dept?.Id;
            }
        }

        if (resolved == null)
            await _odooLog.WarningAsync(
                $"_____Odoo department {odooDeptId} for product {odooProductId} could not be resolved to a nop department (run department sync first) — skipped",
                logEventId, entityType: "Department", odooEntityId: odooDeptId);

        _nopDeptIdCache[odooDeptId] = resolved;
        return resolved;
    }

    /// <summary>
    /// The change marker for an Odoo template: <c>x_studio_sync_revision|write_date</c>. What bumps the
    /// revision is configured in Odoo and not guaranteed to cover every edit; write_date is maintained
    /// by the ORM on every write, so the pair only matches when neither has moved.
    /// </summary>
    private static string GetSyncMarker(OdooProductResponse odooProduct) =>
        $"{odooProduct.XStudioSyncRevision}|{odooProduct.WriteDate}";

    /// <summary>
    /// Records the product's change marker as a generic attribute, so an unchanged product can be
    /// skipped on the next sync.
    /// </summary>
    private Task SaveProductSyncMarkerAsync(Product product, OdooProductResponse odooProduct) =>
        _genericAttributeService.SaveAttributeAsync(product, OdooProductSyncMarkerAttribute, GetSyncMarker(odooProduct));

    #endregion

    #region Product Category Sync

    /// <summary>
    /// Syncs the Odoo product category to nopCommerce.
    /// Odoo's <c>categ_id</c> is a Many2One field containing [id, "Category Name"].
    /// This method finds or creates the matching nopCommerce <see cref="Category"/>
    /// and ensures a <see cref="ProductCategory"/> mapping exists for the product.
    /// </summary>
    private async Task SyncProductCategory(int logEventId, Product nopProduct, OdooProductResponse odooProduct)
    {
        if (odooProduct.CategId == null)
        {
            await _odooLog.InformationAsync(
                $"_____No category assigned for Odoo product {odooProduct.Id}",
                logEventId, entityType: "Category", odooEntityId: odooProduct.Id);
            return;
        }

        try
        {
            var odooCategoryName = odooProduct.CategId.Name?.Trim();
            if (string.IsNullOrWhiteSpace(odooCategoryName))
            {
                await _odooLog.WarningAsync(
                    $"_____Odoo category ID {odooProduct.CategId.Id} has no name — skipping category sync for product {odooProduct.Id}",
                    logEventId, entityType: "Category", odooEntityId: odooProduct.CategId.Id);
                return;
            }

            // Step 1: Find or create the nopCommerce Category by name
            var nopCategory = await FindOrCreateCategoryAsync(logEventId, odooProduct.CategId);

            // Step 2: Check if the product-category mapping already exists
            var existingMappings = await _categoryService.GetProductCategoriesByProductIdAsync(nopProduct.Id, showHidden: true);
            var alreadyMapped = existingMappings.Any(pc => pc.CategoryId == nopCategory.Id);

            if (alreadyMapped)
            {
                await _odooLog.InformationAsync(
                    $"_____Product {nopProduct.Id} is already mapped to category '{nopCategory.Name}' (ID: {nopCategory.Id})",
                    logEventId, entityType: "Category", odooEntityId: odooProduct.CategId.Id, nopEntityId: nopCategory.Id);
                return;
            }

            // Step 3: Create the product-category mapping
            var productCategory = new ProductCategory
            {
                ProductId = nopProduct.Id,
                CategoryId = nopCategory.Id,
                IsFeaturedProduct = false,
                DisplayOrder = 0
            };
            await _categoryService.InsertProductCategoryAsync(productCategory);

            await _odooLog.InformationAsync(
                $"_____Mapped product {nopProduct.Name} (ID: {nopProduct.Id}) to category '{nopCategory.Name}' (ID: {nopCategory.Id})",
                logEventId, entityType: "Category", odooEntityId: odooProduct.CategId.Id, nopEntityId: nopCategory.Id);
        }
        catch (Exception ex)
        {
            await _odooLog.ErrorAsync(
                $"_____Error syncing category for product {odooProduct.Id}: {ex.Message}",
                logEventId, fullMessage: ex.ToString(), entityType: "Category", odooEntityId: odooProduct.CategId?.Id);
        }
    }

    /// <summary>
    /// Finds an existing nopCommerce <see cref="Category"/> by name, or creates a new one.
    /// Also generates and saves a valid SEO-friendly URL slug for new categories.
    /// </summary>
    private async Task<Category> FindOrCreateCategoryAsync(int logEventId, Many2One odooCategory)
    {
        var categoryName = odooCategory.Name.Trim();

        // Search existing categories by exact name match
        var existingCategories = await _categoryService.GetAllCategoriesAsync(
            categoryName: categoryName, showHidden: true, pageSize: 1);

        if (existingCategories.Any())
            return existingCategories.First();

        // Create new category
        var nopCategory = new Category
        {
            Name = categoryName,
            Description = $"Synced from Odoo. Original category ID: {odooCategory.Id}",
            Published = true,
            Deleted = false,
            AllowCustomersToSelectPageSize = true,
            PageSize = 6,
            PageSizeOptions = "6, 3, 9",
            DisplayOrder = 0,
            CreatedOnUtc = DateTime.UtcNow,
            UpdatedOnUtc = DateTime.UtcNow
        };

        await _categoryService.InsertCategoryAsync(nopCategory);

        // Generate and save the SEO slug so the category has a proper URL
        var seName = await _urlRecordService.ValidateSeNameAsync(nopCategory, string.Empty, nopCategory.Name, ensureNotEmpty: true);
        await _urlRecordService.SaveSlugAsync(nopCategory, seName, 0);

        await _odooLog.InformationAsync(
            $"_____Created Category: '{nopCategory.Name}' (ID: {nopCategory.Id}, Slug: {seName})",
            logEventId, entityType: "Category", odooEntityId: odooCategory.Id, nopEntityId: nopCategory.Id);

        return nopCategory;
    }

    #endregion

    #region Product mapping

    private Product MapOdooToNopProduct(OdooProductResponse odooProduct, string odooSku)
    {
        var product = new Product
        {
            Name = (odooProduct.DisplayName?.Trim() ?? odooProduct.Name ?? "Unknown Product"),
            Sku = odooSku,
            Price = odooProduct.ListPrice > 0 ? (decimal)odooProduct.ListPrice : decimal.Zero,
            OldPrice = odooProduct.CompareListPrice > 0 ? (decimal)odooProduct.CompareListPrice : 0m,
            ProductCost = odooProduct.StandardPrice > 0 ? (decimal)odooProduct.StandardPrice : 0m,
            ShortDescription = !string.IsNullOrWhiteSpace(odooProduct.DescriptionSale)
                ? odooProduct.DescriptionSale : string.Empty,
            FullDescription = !string.IsNullOrWhiteSpace(odooProduct.Description)
                ? odooProduct.Description
                : !string.IsNullOrWhiteSpace(odooProduct.DescriptionEcommerce)
                    ? odooProduct.DescriptionEcommerce
                    : odooProduct.WebsiteDescription ?? string.Empty,
            Published = odooProduct.WebsitePublished || odooProduct.IsPublished,
            Deleted = !odooProduct.Active,
            VisibleIndividually = true,
            ProductType = ProductType.SimpleProduct,
            AdminComment = $"Synced from Odoo. Original ID: {odooProduct.Id}, DefaultCode: {odooProduct.DefaultCode}",
            DisableBuyButton = !odooProduct.SaleOk,
            DisableWishlistButton = !odooProduct.SaleOk,
            Weight = (decimal)odooProduct.Weight,
            Gtin = !string.IsNullOrWhiteSpace(odooProduct.Barcode) ? odooProduct.Barcode : string.Empty,
            ManufacturerPartNumber = OdooLink(odooProduct.Id),
            AllowCustomerReviews = true,
            IsShipEnabled = odooProduct.Weight > 0,
            OrderMinimumQuantity = 1,
            OrderMaximumQuantity = 10000,
            MarkAsNew = true,
            CreatedOnUtc = ParseOdooDateTime(odooProduct.CreateDate),
            UpdatedOnUtc = ParseOdooDateTime(odooProduct.WriteDate),

            // Created restricted: the store and department steps then open it exactly as far as Odoo
            // says, so a failure in between can never leave a new product visible everywhere.
            LimitedToStores = true,
            SubjectToAcl = odooProduct.XStudioDepartments?.Count > 0,
        };
        product.MarkAsNewStartDateTimeUtc = product.CreatedOnUtc;
        product.MarkAsNewEndDateTimeUtc = product.CreatedOnUtc.AddMonths(1);

        return product;
    }

    private async Task UpdateExistingProduct(int logEventId, Product product, OdooProductResponse odooProduct)
    {
        // Keep the Odoo link and code current: re-links a product matched by SKU, restores a link
        // cleared or edited in NOP, and follows a default_code change made in Odoo.
        product.ManufacturerPartNumber = OdooLink(odooProduct.Id);
        product.Sku = GetOdooSku(odooProduct);
        _productIdByOdooLink[product.ManufacturerPartNumber] = product.Id;

        product.Name = odooProduct.DisplayName?.Trim() ?? odooProduct.Name ?? product.Name;
        product.Price = odooProduct.ListPrice > 0 ? (decimal)odooProduct.ListPrice : product.Price;
        product.OldPrice = odooProduct.CompareListPrice > 0 ? (decimal)odooProduct.CompareListPrice : product.OldPrice;
        product.ProductCost = odooProduct.StandardPrice > 0 ? (decimal)odooProduct.StandardPrice : product.ProductCost;
        product.ShortDescription = !string.IsNullOrWhiteSpace(odooProduct.DescriptionSale)
            ? odooProduct.DescriptionSale : product.ShortDescription;
        product.FullDescription = !string.IsNullOrWhiteSpace(odooProduct.WebsiteDescription)
            ? odooProduct.WebsiteDescription : product.FullDescription;
        product.Published = odooProduct.WebsitePublished || odooProduct.IsPublished;
        product.Deleted = !odooProduct.Active;
        product.DisableBuyButton = !odooProduct.SaleOk;
        product.DisableWishlistButton = !odooProduct.SaleOk;
        product.Weight = (decimal)odooProduct.Weight;
        product.UpdatedOnUtc = ParseOdooDateTime(odooProduct.WriteDate);

        await _productService.UpdateProductAsync(product);
        await _odooLog.InformationAsync($"Product updated: {product.Name} (ID: {product.Id})",
            logEventId, entityType: "Product", nopEntityId: product.Id);
    }

    #endregion

    #region Attribute and variant sync

    /// <summary>
    /// Orchestrates attribute creation and variant sync for a product.
    /// 
    /// Odoo structure:
    ///   product.template → has attribute_line_ids (ProductTemplateAttributeLine)
    ///     each line → links one attribute (e.g. "Color") with specific values for this product
    ///   product.template.attribute.value → the actual values (e.g. "Red", "Blue") per template
    ///   product.product (variants) → each has product_template_attribute_value_ids
    ///
    /// nopCommerce structure:
    ///   ProductAttribute → global attribute definition (e.g. "Color")
    ///   ProductAttributeMapping → links attribute to a specific product
    ///   ProductAttributeValue → values under a mapping (e.g. "Red", "Blue")
    ///   ProductAttributeCombination → a specific variant with AttributesXml
    /// </summary>
    /// <returns>False when the step failed as a whole (the error is logged); the product must then not
    /// be recorded as synced, or the next run would skip it as unchanged and its attributes and
    /// variants would never be created.</returns>
    private async Task<bool> SyncAttributesAndVariants(int logEventId, Product nopProduct, OdooProductResponse odooProduct)
    {
        if (odooProduct.ProductVariantCount <= 0 && (odooProduct.AttributeLineIds == null || !odooProduct.AttributeLineIds.Any()))
        {
            await _odooLog.InformationAsync($"_____No attributes or variants for product {nopProduct.Name}",
                logEventId, entityType: "Product", odooEntityId: odooProduct.Id, nopEntityId: nopProduct.Id);
            return true;
        }

        try
        {
            // Step 1: Fetch Odoo attribute data
            var attributeLines = await _odooApiService.GetProductTemplateAttributeLinesAsync(odooProduct.Id);
            var templateAttrValues = await _odooApiService.GetProductTemplateAttributeValuesAsync(odooProduct.Id);
            var odooAttributes = await _odooApiService.GetProductAttributesAsync(odooProduct.Id);

            if (!attributeLines.Any())
            {
                await _odooLog.InformationAsync($"_____No attribute lines found for product {odooProduct.Id}",
                    logEventId, entityType: "Product", odooEntityId: odooProduct.Id);
                return true;
            }

            // Step 2: Create/find nopCommerce attributes and mappings
            // Maps: Odoo attribute ID → nopCommerce ProductAttributeMapping ID
            var attributeMappingLookup = new Dictionary<int, int>();
            // Maps: Odoo ProductTemplateAttributeValue ID → nopCommerce ProductAttributeValue ID
            var attrValueLookup = new Dictionary<int, int>();

            foreach (var line in attributeLines)
            {
                var odooAttr = odooAttributes.FirstOrDefault(a => a.Id == line.Attribute.Id);

                if (odooAttr == null)
                {
                    await _odooLog.WarningAsync($"_____Odoo attribute {line.Attribute.Id} ({line.Attribute.Name}) not found in fetched attributes",
                        logEventId, entityType: "Attribute", odooEntityId: line.Attribute.Id);
                    continue;
                }
                var visibility = odooAttr.Visibility.Contains("visible", StringComparison.OrdinalIgnoreCase);

                // Find or create the global ProductAttribute in nopCommerce
                var nopAttributes = await FindOrCreateProductAttributeAsync(logEventId, odooAttr);
                var nopAttribute = nopAttributes.pa;
                var nopSpecificationAttribute = nopAttributes.sa;
                if (nopAttribute != null)
                {
                    // Find or create the ProductAttributeMapping for this product
                    var nopMapping = await FindOrCreateAttributeMappingAsync(logEventId, nopProduct.Id, nopAttribute.Id, odooAttr);

                    attributeMappingLookup[odooAttr.Id] = nopMapping.Id;

                    // Create ProductAttributeValues for each template attribute value on this line
                    var lineValues = templateAttrValues
                        .Where(v => v.Attribute.Id == odooAttr.Id)
                        .ToList();

                    foreach (var odooValue in lineValues)
                    {
                        var nopValue = await FindOrCreateAttributeValueAsync(nopMapping.Id, odooValue);
                        attrValueLookup[odooValue.Id] = nopValue.Id;
                    }
                }

                if (nopSpecificationAttribute != null)
                {

                    var lineValues = templateAttrValues
                        .Where(v => v.Attribute.Id == odooAttr.Id)
                        .ToList();
                    var odooValues = string.Empty;
                    var attrType = (int)SpecificationAttributeType.CustomText;
                    var optionValueId = 0;
                    if (lineValues.Count > 1)
                    {
                        attrType = (int)SpecificationAttributeType.Option;
                    }
                    foreach (var odooValue in lineValues)
                    {
                        var valueName = odooValue.Name ?? odooValue.DisplayName ?? "Unknown";
                        odooValues = odooValues + " " + valueName;
                        var optionValue = await FindOrCreateSpecAttributeValueAsync(nopSpecificationAttribute.Id, valueName);
                        optionValueId = optionValue.Id;
                    }
                    var psa = new ProductSpecificationAttribute
                    {
                        ProductId = nopProduct.Id,
                        AttributeTypeId = attrType,
                        SpecificationAttributeOptionId = optionValueId,
                        CustomValue = odooValues,
                        AllowFiltering = false,
                        ShowOnProductPage = true,
                        DisplayOrder = nopSpecificationAttribute.DisplayOrder
                    };
                    await _specificationAttributeService.InsertProductSpecificationAttributeAsync(psa);

                }
            }

            // Step 3: Sync variants as ProductAttributeCombinations
            if (odooProduct.ProductVariantCount > 0)
            {
                var variants = await _odooApiService.GetProductVariantsAsync(odooProduct.Id);
                await SyncProductVariantsAsync(logEventId, nopProduct, variants, templateAttrValues, attributeMappingLookup, attrValueLookup);
            }

            return true;
        }
        catch (Exception ex)
        {
            await _odooLog.ErrorAsync($"_____Error syncing attributes/variants for product {odooProduct.Id}: {ex.Message}",
                logEventId, fullMessage: ex.ToString(), entityType: "Product", odooEntityId: odooProduct.Id);
            return false;
        }
    }

    /// <summary>
    /// Finds an existing nopCommerce ProductAttribute by name, or creates a new one.
    /// </summary>
    private async Task<(ProductAttribute pa, SpecificationAttribute sa)> FindOrCreateProductAttributeAsync(int logEventId, ProductAttributeResponse odooAttr)
    {
        var attrName = odooAttr.Name ?? odooAttr.DisplayName ?? "Unknown";

        ProductAttribute pa = null;
        SpecificationAttribute sa = null;

        var createVariant = odooAttr.CreateVariant.Contains("always", StringComparison.OrdinalIgnoreCase);
        var hasCombinations = odooAttr.AttributeLineIds.Count > 0;
        var visibility = odooAttr.Visibility.Contains("visible", StringComparison.OrdinalIgnoreCase);

        if (createVariant && hasCombinations && visibility)
        {
            // Search existing attributes by name
            var existingAttributes = await _productAttributeService.GetAllProductAttributesAsync(name: attrName, pageSize: 1);
            if (existingAttributes.Any())
                pa = existingAttributes.First();

            if (pa == null)
            {
                // Create new
                var nopAttribute = new ProductAttribute
                {
                    Name = attrName,
                    Description = $"Synced from Odoo. Original ID: {odooAttr.Id}",
                };

                await _productAttributeService.InsertProductAttributeAsync(nopAttribute);
                await _odooLog.InformationAsync($"_____Created ProductAttribute: {nopAttribute.Name} (ID: {nopAttribute.Id})",
                    logEventId, entityType: "Attribute", odooEntityId: odooAttr.Id, nopEntityId: nopAttribute.Id);
                pa = nopAttribute;

            }
        }
        else
        {
            if (!createVariant)
            {
                // Search existing attributes by name
                var existingSAttributes = await _specificationAttributeService.GetSpecificationAttributesByNameAsync(name: attrName, pageSize: 1);
                if (existingSAttributes.Any())
                    sa = existingSAttributes.First();

                if (sa == null)
                {
                    // Create new
                    var nopSAttribute = new SpecificationAttribute
                    {
                        Name = attrName,
                        //Description = $"Synced from Odoo. Original ID: {odooAttr.Id}",
                    };

                    await _specificationAttributeService.InsertSpecificationAttributeAsync(nopSAttribute);
                    await _odooLog.InformationAsync($"_____Created ProductAttribute: {nopSAttribute.Name} (ID: {nopSAttribute.Id})",
                        logEventId, entityType: "Attribute", odooEntityId: odooAttr.Id, nopEntityId: nopSAttribute.Id);
                    sa = nopSAttribute;
                }
            }
        }
        return (pa, sa);
    }

    /// <summary>
    /// Finds an existing mapping of an attribute to a product, or creates one.
    /// </summary>
    private async Task<ProductAttributeMapping> FindOrCreateAttributeMappingAsync(int logEventId,
        int productId, int productAttributeId, ProductAttributeResponse odooAttr)
    {
        var existingMappings = await _productAttributeService.GetProductAttributeMappingsByProductIdAsync(productId);
        var existing = existingMappings.FirstOrDefault(m => m.ProductAttributeId == productAttributeId);
        if (existing != null)
            return existing;

        // Determine control type based on Odoo display_type
        var controlType = odooAttr.DisplayType switch
        {
            "color" => AttributeControlType.ColorSquares,
            "radio" => AttributeControlType.RadioList,
            "select" => AttributeControlType.DropdownList,
            "pills" => AttributeControlType.RadioList,
            _ => AttributeControlType.DropdownList
        };

        var mapping = new ProductAttributeMapping
        {
            ProductId = productId,
            ProductAttributeId = productAttributeId,
            AttributeControlType = controlType,
            IsRequired = !string.IsNullOrEmpty(odooAttr.CreateVariant) && odooAttr.CreateVariant.Contains("always", StringComparison.OrdinalIgnoreCase), //Validate which Attributes are required or not
            DisplayOrder = odooAttr.Sequence,
        };
        await _productAttributeService.InsertProductAttributeMappingAsync(mapping);
        await _odooLog.InformationAsync($"_____Created ProductAttributeMapping: attribute {productAttributeId} → product {productId} (ID: {mapping.Id})",
            logEventId, entityType: "AttributeMapping", nopEntityId: mapping.Id);
        return mapping;
    }

    /// <summary>
    /// Finds an existing attribute value under a mapping, or creates one.
    /// </summary>
    private async Task<ProductAttributeValue> FindOrCreateAttributeValueAsync(
        int mappingId, ProductTemplateAttributeValueResponse odooValue)
    {
        var valueName = odooValue.Name ?? odooValue.DisplayName ?? "Unknown";

        var existingValues = await _productAttributeService.GetProductAttributeValuesAsync(mappingId);
        var existing = existingValues.FirstOrDefault(v =>
            string.Equals(v.Name, valueName, StringComparison.OrdinalIgnoreCase));
        if (existing != null)
            return existing;

        var nopValue = new ProductAttributeValue
        {
            ProductAttributeMappingId = mappingId,
            Name = valueName,
            ColorSquaresRgb = !string.IsNullOrWhiteSpace(odooValue.HtmlColor) ? odooValue.HtmlColor : null,
            PriceAdjustment = (decimal)odooValue.PriceExtra,
            WeightAdjustment = 0,
            IsPreSelected = false,
            DisplayOrder = 0,
            AttributeValueType = AttributeValueType.Simple,
        };
        await _productAttributeService.InsertProductAttributeValueAsync(nopValue);
        return nopValue;
    }

    /// <summary>
    /// Finds an existing attribute value under a mapping, or creates one.
    /// </summary>
    private async Task<SpecificationAttributeOption> FindOrCreateSpecAttributeValueAsync(
        int mappingId, string valueName)
    {
        var existingValues = await _specificationAttributeService.GetSpecificationAttributeOptionsBySpecificationAttributeAsync(mappingId);
        var existing = existingValues.FirstOrDefault(v =>
            string.Equals(v.Name, valueName, StringComparison.OrdinalIgnoreCase));
        if (existing != null)
            return existing;

        var nopValue = new SpecificationAttributeOption
        {
            SpecificationAttributeId = mappingId,
            Name = valueName,
            DisplayOrder = 0,
        };
        await _specificationAttributeService.InsertSpecificationAttributeOptionAsync(nopValue);
        return nopValue;
    }

    #endregion

    #region Variant sync

    private async Task SyncProductVariantsAsync(int logEventId,
        Product parentProduct,
        List<ProductVariantResponse> variants,
        List<ProductTemplateAttributeValueResponse> templateAttrValues,
        Dictionary<int, int> attributeMappingLookup,
        Dictionary<int, int> attrValueLookup)
    {
        var synced = 0;
        var failed = 0;

        // The product's combinations, loaded once; copied because the service may return a cached list.
        var existingCombinations = (await _productAttributeService.GetAllProductAttributeCombinationsAsync(parentProduct.Id)).ToList();

        foreach (var variant in variants)
        {
            try
            {
                if (!variant.Active)
                {
                    await _odooLog.InformationAsync($"_____Skipping inactive variant {variant.Id}",
                        logEventId, entityType: "Variant", odooEntityId: variant.Id);
                    continue;
                }

                var variantSku = GenerateVariantSku(variant, parentProduct.Sku);

                // Check if the combination already exists (by its Odoo link, see FindExistingCombination)
                var existingCombination = FindExistingCombination(existingCombinations, variant);
                if (existingCombination != null)
                {
                    // Update existing combination
                    UpdateExistingCombination(existingCombination, variant);
                    await _productAttributeService.UpdateProductAttributeCombinationAsync(existingCombination);
                    synced++;
                    continue;
                }

                // Build AttributesXml from variant's attribute value IDs
                var attributesXml = await BuildAttributesXmlAsync(logEventId,
                    variant, templateAttrValues, attributeMappingLookup, attrValueLookup);

                if (string.IsNullOrEmpty(attributesXml) && variant.ProductTemplateAttributeValueIds?.Any() == true)
                {
                    await _odooLog.WarningAsync($"_____Could not build AttributesXml for variant {variant.Id} - skipping",
                        logEventId, entityType: "Variant", odooEntityId: variant.Id);
                    failed++;
                    continue;
                }

                var combination = new ProductAttributeCombination
                {
                    ProductId = parentProduct.Id,
                    AttributesXml = attributesXml,
                    Sku = !string.IsNullOrWhiteSpace(variant.DefaultCode) ? variant.DefaultCode : variantSku,
                    StockQuantity = (int)variant.QuantityAvailable,
                    AllowOutOfStockOrders = true,
                    ManufacturerPartNumber = OdooLink(variant.Id),
                    Gtin = !string.IsNullOrWhiteSpace(variant.Barcode) ? variant.Barcode : string.Empty,
                    OverriddenPrice = variant.LstPrice > 0 ? (decimal)variant.LstPrice : null,
                    MinStockQuantity = 0,
                    NotifyAdminForQuantityBelow = 1,
                };

                await _productAttributeService.InsertProductAttributeCombinationAsync(combination);
                existingCombinations.Add(combination);
                await _odooLog.InformationAsync($"_____Created variant combination: {variant.DisplayName} (SKU: {combination.Sku})",
                    logEventId, entityType: "Variant", odooEntityId: variant.Id, nopEntityId: combination.Id);

                // Sync variant image
                if (!string.IsNullOrWhiteSpace(variant.ImageVariant1920))
                    await SyncVariantImageAsync(logEventId, parentProduct, variant, combination);

                synced++;
            }
            catch (Exception ex)
            {
                await _odooLog.WarningAsync($"_____Failed to sync variant {variant.Id}: {ex.Message}",
                    logEventId, fullMessage: ex.ToString(), entityType: "Variant", odooEntityId: variant.Id);
                failed++;
            }
        }

        await _odooLog.InformationAsync($"_____Variant sync for {parentProduct.Name}: {synced} synced, {failed} failed",
            logEventId, entityType: "Product", nopEntityId: parentProduct.Id);
    }

    /// <summary>
    /// Finds the combination for an Odoo variant (product.product) among the product's combinations:
    /// by its Odoo link <c>ODOO_{id}</c>; else by the link the original sync wrote
    /// (<c>{parentSku}_ODOOVAR_{id}_{code}</c>), so its combinations are adopted rather than duplicated;
    /// else by the variant's code, on a combination not linked to another variant.
    /// </summary>
    private static ProductAttributeCombination FindExistingCombination(
        IList<ProductAttributeCombination> combinations, ProductVariantResponse variant)
    {
        var link = OdooLink(variant.Id);
        var legacyLink = $"_ODOOVAR_{variant.Id}_";

        return combinations.FirstOrDefault(c => string.Equals(c.ManufacturerPartNumber, link, StringComparison.Ordinal))
            ?? combinations.FirstOrDefault(c => c.ManufacturerPartNumber?.Contains(legacyLink, StringComparison.Ordinal) == true)
            ?? (string.IsNullOrWhiteSpace(variant.DefaultCode)
                ? null
                : combinations.FirstOrDefault(c =>
                    string.Equals(c.Sku, variant.DefaultCode, StringComparison.Ordinal) && !IsLinkedToAnotherVariant(c, link)));
    }

    /// <summary>
    /// True when the combination already carries an Odoo link (current or legacy form) for a different variant.
    /// </summary>
    private static bool IsLinkedToAnotherVariant(ProductAttributeCombination combination, string link)
    {
        var mpn = combination.ManufacturerPartNumber;
        return !string.IsNullOrEmpty(mpn)
            && (mpn.StartsWith(OdooLinkPrefix, StringComparison.Ordinal) || mpn.Contains("_ODOOVAR_", StringComparison.Ordinal))
            && !string.Equals(mpn, link, StringComparison.Ordinal);
    }

    private void UpdateExistingCombination(ProductAttributeCombination combination, ProductVariantResponse variant)
    {
        // Keep the Odoo link and code current (also converts a legacy link to the ODOO_{id} form).
        combination.ManufacturerPartNumber = OdooLink(variant.Id);
        if (!string.IsNullOrWhiteSpace(variant.DefaultCode))
            combination.Sku = variant.DefaultCode;

        combination.StockQuantity = (int)variant.QuantityAvailable;
        combination.Gtin = !string.IsNullOrWhiteSpace(variant.Barcode) ? variant.Barcode : combination.Gtin;
        combination.OverriddenPrice = variant.LstPrice > 0 ? (decimal)variant.LstPrice : combination.OverriddenPrice;
    }

    /// <summary>
    /// Builds the AttributesXml string that nopCommerce uses to identify a specific variant combination.
    /// 
    /// nopCommerce format:
    ///   <Attributes>
    ///     <ProductAttribute ID="{mappingId}">
    ///       <ProductAttributeValue><Value>{valueId}</Value></ProductAttributeValue>
    ///     </ProductAttribute>
    ///   </Attributes>
    ///
    /// The mapping: variant.ProductTemplateAttributeValueIds contains Odoo PTAV IDs.
    /// Each PTAV has an Attribute (e.g. Color) and a value name (e.g. Red).
    /// We look up the corresponding nopCommerce mapping ID and value ID.
    /// </summary>
    private async Task<string> BuildAttributesXmlAsync(int logEventId,
        ProductVariantResponse variant,
        List<ProductTemplateAttributeValueResponse> templateAttrValues,
        Dictionary<int, int> attributeMappingLookup,
        Dictionary<int, int> attrValueLookup)
    {
        var ptavIds = variant.ProductTemplateAttributeValueIds;
        if (ptavIds == null || !ptavIds.Any())
            return string.Empty;

        var root = new XElement("Attributes");

        // Group by attribute to create one ProductAttribute element per attribute
        var grouped = new Dictionary<int, List<int>>(); // nopMappingId → list of nopValueIds

        foreach (var ptavId in ptavIds)
        {
            if (!attrValueLookup.TryGetValue(ptavId, out var nopValueId))
            {
                await _odooLog.WarningAsync($"_____No nopCommerce value mapping for Odoo PTAV {ptavId}",
                    logEventId, entityType: "AttributeValue", odooEntityId: ptavId);
                continue;
            }

            // Find which attribute this PTAV belongs to
            var ptav = templateAttrValues.FirstOrDefault(v => v.Id == ptavId);
            if (ptav == null)
            {
                await _odooLog.WarningAsync($"_____PTAV {ptavId} not found in template attribute values",
                    logEventId, entityType: "AttributeValue", odooEntityId: ptavId);
                continue;
            }

            var odooAttrId = ptav.Attribute.Id;
            if (!attributeMappingLookup.TryGetValue(odooAttrId, out var nopMappingId))
            {
                await _odooLog.WarningAsync($"_____No nopCommerce mapping for Odoo attribute {odooAttrId}",
                    logEventId, entityType: "Attribute", odooEntityId: odooAttrId);
                continue;
            }

            if (!grouped.ContainsKey(nopMappingId))
                grouped[nopMappingId] = new List<int>();

            grouped[nopMappingId].Add(nopValueId);
        }

        foreach (var (mappingId, valueIds) in grouped)
        {
            var attrElement = new XElement("ProductAttribute",
                new XAttribute("ID", mappingId.ToString()));

            foreach (var valueId in valueIds)
            {
                attrElement.Add(new XElement("ProductAttributeValue",
                    new XElement("Value", valueId.ToString())));
            }

            root.Add(attrElement);
        }

        return root.ToString(SaveOptions.DisableFormatting);
    }

    #endregion

    #region Image sync

    /// <summary>
    /// A fingerprint of the Odoo template's main image ("none" when it has none), stored when the
    /// product is synced so a later change in Odoo can be detected without keeping the image itself.
    /// </summary>
    private static string GetImageFingerprint(OdooProductResponse odooProduct) =>
        string.IsNullOrWhiteSpace(odooProduct.Image1920)
            ? "none"
            : Convert.ToHexString(SHA256.HashData(Encoding.ASCII.GetBytes(odooProduct.Image1920)));

    /// <summary>
    /// Odoo sets a product's first image; after that NOP owns its images. On update: a product with
    /// no picture yet gets Odoo's image as its first one; otherwise a change to Odoo's main image is
    /// only reported as a warning (once per change) — NOP's pictures are never replaced.
    /// </summary>
    private async Task CheckMainImageOnUpdateAsync(int logEventId, Product product, OdooProductResponse odooProduct)
    {
        try
        {
            var fingerprint = GetImageFingerprint(odooProduct);
            var pictures = await _productService.GetProductPicturesByProductIdAsync(product.Id);

            if (pictures.Count == 0 && !string.IsNullOrWhiteSpace(odooProduct.Image1920))
            {
                await SyncProductMainImageAsync(logEventId, product, odooProduct.Image1920, product.Name);
            }
            else
            {
                // No fingerprint yet (product synced before this check existed): record a baseline, no warning.
                var recorded = await _genericAttributeService.GetAttributeAsync<string>(product, OdooImageFingerprintAttribute);
                if (recorded != null && !string.Equals(recorded, fingerprint, StringComparison.Ordinal))
                    await _odooLog.WarningAsync(
                        $"Odoo's main image for product '{product.Name}' (ID: {product.Id}) has changed since NOP last saw it. NOP keeps its own images — update the picture manually if the new one is wanted.",
                        logEventId, entityType: "Product", odooEntityId: odooProduct.Id, nopEntityId: product.Id);
            }

            await _genericAttributeService.SaveAttributeAsync(product, OdooImageFingerprintAttribute, fingerprint);
        }
        catch (Exception ex)
        {
            await _odooLog.WarningAsync($"_____Could not check the main image for product {product.Id}: {ex.Message}",
                logEventId, fullMessage: ex.ToString(), entityType: "Product", nopEntityId: product.Id);
        }
    }

    private async Task SyncProductMainImageAsync(int logEventId, Product product, string base64Image, string productName)
    {
        try
        {
            var imageBinary = DecodeBase64Image(base64Image);
            if (imageBinary == null) return;

            var seoFilename = await _pictureService.GetPictureSeNameAsync(productName);
            var picture = await InsertPicture(productName, imageBinary, GetImageMimeType(imageBinary), seoFilename);
            if (picture == null) return;

            var productPicture = new ProductPicture
            {
                ProductId = product.Id,
                PictureId = picture.Id,
                DisplayOrder = 0
            };
            await _productService.InsertProductPictureAsync(productPicture);
            await _odooLog.InformationAsync($"_____Synced main image for product {product.Name} (Picture ID: {picture.Id})",
                logEventId, entityType: "Product", nopEntityId: product.Id);
        }
        catch (Exception ex)
        {
            await _odooLog.WarningAsync($"_____Failed to sync main product image for {product.Id}: {ex.Message}",
                logEventId, fullMessage: ex.ToString(), entityType: "Product", nopEntityId: product.Id);
        }
    }

    private async Task<bool> SyncVariantImageAsync(int logEventId,
        Product parentProduct, ProductVariantResponse variant, ProductAttributeCombination combination)
    {
        try
        {
            // Use variant-specific image if available, otherwise fall back to template image
            var imageData = !string.IsNullOrWhiteSpace(variant.ImageVariant1920)
                ? variant.ImageVariant1920
                : variant.Image1920;

            if (string.IsNullOrWhiteSpace(imageData))
                return false;

            var imageBinary = DecodeBase64Image(imageData);
            if (imageBinary == null) return false;

            var seoFilename = await _pictureService.GetPictureSeNameAsync(variant.DisplayName);
            var picture = await InsertPicture(variant.DisplayName, imageBinary, GetImageMimeType(imageBinary), seoFilename);
            if (picture == null) return false;

            var combinationPicture = new ProductAttributeCombinationPicture
            {
                PictureId = picture.Id,
                ProductAttributeCombinationId = combination.Id
            };
            await _productAttributeService.InsertProductAttributeCombinationPictureAsync(combinationPicture);
            return true;
        }
        catch (Exception ex)
        {
            await _odooLog.WarningAsync($"_____Failed to sync variant image for variant {variant.Id}: {ex.Message}",
                logEventId, fullMessage: ex.ToString(), entityType: "Variant", odooEntityId: variant.Id);
            return false;
        }
    }

    /// <summary>
    /// The MIME type of an Odoo image, read from its leading bytes — Odoo stores whatever was
    /// uploaded (JPEG, PNG, WebP, GIF…). Falls back to JPEG for anything unrecognised.
    /// nop only re-encodes pictures larger than its maximum size, so a wrong type would otherwise
    /// leave e.g. WebP bytes labelled and served as JPEG.
    /// </summary>
    private static string GetImageMimeType(byte[] image)
    {
        static bool StartsWith(byte[] data, int offset, params byte[] signature) =>
            data.Length >= offset + signature.Length && data.AsSpan(offset, signature.Length).SequenceEqual(signature);

        if (StartsWith(image, 0, 0x89, 0x50, 0x4E, 0x47))
            return MimeTypes.ImagePng;
        if (StartsWith(image, 0, 0x47, 0x49, 0x46, 0x38))
            return MimeTypes.ImageGif;
        if (StartsWith(image, 0, 0x52, 0x49, 0x46, 0x46) && StartsWith(image, 8, 0x57, 0x45, 0x42, 0x50))
            return MimeTypes.ImageWebp;

        return MimeTypes.ImageJpeg;
    }

    private byte[] DecodeBase64Image(string base64)
    {
        try
        {
            var bytes = Convert.FromBase64String(base64);
            return bytes.Length > 0 ? bytes : null;
        }
        catch (FormatException)
        {
            return null;
        }
    }

    private async Task<Picture> InsertPicture(
        string name, byte[] imageBinary, string mimeType, string seoFilename)
    {
        //return new Picture();
        return await _pictureService.InsertPictureAsync(
            imageBinary,
            mimeType,
            seoFilename,
            altAttribute: name,
            titleAttribute: name,
            isNew: true,
            validateBinary: true);
    }

    #endregion

    #region SKU generation

    private string GenerateVariantSku(ProductVariantResponse variant, string parentProductSku)
    {

        var odooSku = !string.IsNullOrWhiteSpace(variant.DefaultCode) ? variant.DefaultCode : "NONE";

        return $"{parentProductSku}_ODOOVAR_{variant.Id}_{odooSku}";
    }

    #endregion

    #region Helpers

    private DateTime ParseOdooDateTime(string odooDateTimeString)
    {
        if (string.IsNullOrWhiteSpace(odooDateTimeString))
            return DateTime.UtcNow;

        if (DateTime.TryParse(odooDateTimeString, out var result))
            return DateTime.SpecifyKind(result, DateTimeKind.Utc);

        return DateTime.UtcNow;
    }

    protected virtual string GetMimeTypeFromFilePath(string filePath)
    {
        new FileExtensionContentTypeProvider().TryGetContentType(filePath, out var mimeType);
        return mimeType ?? _pictureService.GetPictureContentTypeByFileExtension(_fileProvider.GetFileExtension(filePath));
    }

    #endregion

    #endregion

    #region Methods

    /// <inheritdoc/>
    public async Task<ProductSyncResult> SyncOdooProductsAsync(int logEventId)
    {
        // Reset per-run caches and preload the lookups used for every product: Odoo departments
        // (for department ACL) and the Odoo→nop store mapping (for store visibility).
        _nopDeptIdCache.Clear();
        var odooDepartments = await _odooApiService.FetchDepartmentsAsync();
        _odooDeptById = odooDepartments.ToDictionary(d => d.Id);
        _nopStoreIdByOdooStoreId = await _storeMapService.GetNopStoreIdLookupAsync();
        _productIdByOdooLink = await LoadOdooLinkedProductsAsync();

        // Every Odoo template the run looks at is counted exactly once (see SyncTally).
        var tally = new SyncTally();

        const int pageSize = 25;
        var productId = 0; //245; // for testing purposes: sync this one Odoo template only

        // Page through the active, sellable templates until Odoo returns a short page.
        for (var offset = 0; ; offset += pageSize)
        {
            var page = await _odooApiService.FetchProductsAsync(pageSize, offset, productId);

            foreach (var odooProduct in page.Records)
                tally.Add(await SyncOdooProductAsync(logEventId, odooProduct));

            await LogUnreadableProductsAsync(logEventId, page, tally);

            // Count, not Records.Count: an unreadable product must not look like the end of the data.
            if (page.Count < pageSize || productId > 0)
                break;
        }

        if (tally.Created + tally.Updated + tally.Skipped == 0)
            await _odooLog.InformationAsync("No active, sellable products found in Odoo to sync.", logEventId, entityType: "ProductSync");

        // Archived templates are excluded from the fetch above, so unpublish them explicitly.
        if (productId == 0)
            await UnpublishArchivedProductsAsync(logEventId, tally);

        return new ProductSyncResult(tally.Created, tally.Updated, tally.Skipped);
    }

    #endregion
}
