using Nop.Plugin.Integration.Odoo.Models;
using Nop.Plugin.Integration.Odoo.Models.Responses;
#pragma warning disable CS8632

namespace Nop.Plugin.Integration.Odoo.Services;

/// <summary>
/// Contract for all Odoo 19 JSON-2 API operations.
/// </summary>
public interface IOdooApiService
{
    /// <summary>
    /// Tests the connection to Odoo by:
    ///   1. Calling /web/version to retrieve server version info (unauthenticated).
    ///   2. Calling /json/2/res.users/context_get to verify the API key resolves to a real user.
    ///   3. Counting published products available in the database.
    /// </summary>
    /// <param name="odooUrl">Base URL of the Odoo instance.</param>
    /// <param name="databaseName">Odoo database name.</param>
    /// <param name="apiKey">User API key (bearer token).</param>
    /// <returns>A <see cref="OdooConnectionResult"/> containing version, user, and product count.</returns>
    Task<OdooConnectionResult> TestConnectionAsync(string odooUrl, string databaseName, string apiKey);

    /// <summary>
    /// Fetches a page of active, sellable product templates, read product by product: a product Odoo
    /// returns in an unexpected shape is reported in the page's errors instead of failing the page.
    /// </summary>
    Task<OdooRecordPage<OdooProductResponse>> FetchProductsAsync(int limit = 100, int offset = 0, int odooProductId = 0);

    Task<List<ProductVariantResponse>> GetProductVariantsAsync(int productId, int limit = 100, int offset = 0);
    Task<List<ProductTemplateAttributeLineResponse>> GetProductTemplateAttributeLinesAsync(int productId);
    Task<List<ProductTemplateAttributeValueResponse>> GetProductTemplateAttributeValuesAsync(int productId);
    Task<List<ProductAttributeResponse>> GetProductAttributesAsync(int productTemplateId = 0);

    // ?? Partner / Branch endpoints ????????????????????????????????????????????

    /// <summary>
    /// Fetches all Odoo stores (x_store), including their linked companies and departments.
    /// </summary>
    /// <returns>List of Odoo store records.</returns>
    Task<List<OdooStoreResponse>> FetchStoresAsync();

    /// <summary>
    /// Fetches all Odoo departments (x_department), including archived ones and their parent store.
    /// </summary>
    /// <returns>List of Odoo department records.</returns>
    Task<List<OdooDepartmentResponse>> FetchDepartmentsAsync();

    /// <summary>
    /// Fetches a page of archived product templates (id + default_code only), read product by product.
    /// </summary>
    Task<OdooRecordPage<OdooProductResponse>> FetchArchivedProductsAsync(int limit, int offset);

    /// <summary>
    /// Retrieves all child partners of a given parent company that have type = "delivery".
    /// These map to nopCommerce Branches.
    /// </summary>
    /// <param name="parentPartnerId">The Odoo res.partner ID of the parent company.</param>
    /// <returns>List of delivery address partners.</returns>
    Task<List<OdooPartnerResponse>> GetDeliveryAddressesAsync(int parentPartnerId);

    /// <summary>
    /// Retrieves the given res.partner records (including archived ones), e.g. a store's companies,
    /// which map to nopCommerce head-office Branches.
    /// </summary>
    /// <param name="partnerIds">The Odoo res.partner IDs.</param>
    /// <returns>The partners that exist; ids that no longer exist in Odoo are simply absent.</returns>
    Task<List<OdooPartnerResponse>> GetPartnersByIdsAsync(IList<int> partnerIds);
}

/// <summary>
/// Result returned from a connection test.
/// </summary>
public record OdooConnectionResult(
    bool Success,
    string? OdooVersion,
    string? DatabaseName,
    string? LoggedInUser,
    int? ProductCount,
    string? ErrorMessage
);
