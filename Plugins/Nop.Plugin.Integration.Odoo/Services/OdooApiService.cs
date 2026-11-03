using Microsoft.Extensions.Logging;
using Nop.Plugin.Integration.Odoo.Domain;
using Nop.Plugin.Integration.Odoo.Models.Responses;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Nop.Plugin.Integration.Odoo.Services;

/// <summary>
/// Implements communication with the Odoo 19 JSON-2 REST API.
///
/// Authentication: Bearer token via the Authorization HTTP header.
///   Authorization: bearer {apiKey}
///
/// Base endpoint pattern:
///   POST {odooUrl}/json/2/{model}/{method}
///
/// Required headers:
///   Authorization: bearer {apiKey}
///   Content-Type: application/json; charset=utf-8
///   X-Odoo-Database: {databaseName}   (required on SaaS multi-db instances)
///
/// Identify the integration for Odoo server logs
///   User-Agent: NopCommerce-OdooIntegration/1.0
///
/// Version endpoint (unauthenticated):
///   GET/POST {odooUrl}/web/version
/// </summary>
public class OdooApiService : IOdooApiService
{
    #region Fields

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly OdooSettings _settings;
    private readonly ILogger<OdooApiService> _logger;


    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false
    };
    #endregion

    #region Ctor

    public OdooApiService(
        IHttpClientFactory httpClientFactory,
        OdooSettings settings,
        ILogger<OdooApiService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _settings = settings;
        _logger = logger;
    }

    #endregion

    #region Utilities

    /// Retrieves the Odoo server version from the specified URL asynchronously.
    /// </summary>
    /// <param name="odooUrl">The base URL of the Odoo server.</param>
    /// <returns>The version string if available; otherwise, null.</returns>
    /// <exception cref="OdooApiException">Thrown when the Odoo server cannot be reached or does not respond successfully.</exception>
    private async Task<string> GetVersionAsync(string odooUrl)
    {
        // /web/version is a lightweight, unauthenticated endpoint that returns
        // {"version_info": [19, 0, 0, "final", 0, ""], "version": "19.0"}
        var endpoint = $"{odooUrl}/web/version";
        var client = _httpClientFactory.CreateClient(OdooConstants.HttpClientName);

        var response = await client.PostAsync(
            endpoint,
            new StringContent("{}", Encoding.UTF8, "application/json"));

        if (!response.IsSuccessStatusCode)
        {
            // Fallback: try GET
            response = await client.GetAsync(endpoint);
        }

        if (!response.IsSuccessStatusCode)
            throw new OdooApiException($"Unable to reach Odoo at {odooUrl}. HTTP {(int)response.StatusCode}.");

        var content = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);

        if (doc.RootElement.TryGetProperty("version", out var versionEl))
            return versionEl.GetString();

        return null;
    }

    /// <summary>
    /// Asynchronously retrieves the total number of products from the Odoo server.
    /// </summary>
    /// <param name="odooUrl">The base URL of the Odoo server.</param>
    /// <param name="databaseName">The name of the Odoo database.</param>
    /// <param name="apiKey">The API key used for authentication.</param>
    /// <returns>The total number of products if successful; otherwise, null.</returns>
    private async Task<int?> CountProductsAsync(string odooUrl, string databaseName, string apiKey)
    {
        // Use search_count on product.template to return the total number of products.
        // Domain [["website_published", "=", true]] restricts to website-visible products.
        // We intentionally use an empty domain to return all products so the test is more
        // informative; a future sync will apply website filters.
        var endpoint = $"{odooUrl}/json/2/product.template/search_count";

        var bodyObj = new
        {
            domain = Array.Empty<object>()   // empty = count all accessible products
        };

        var json = JsonSerializer.Serialize(bodyObj, _jsonOptions);
        var response = await SendRequestAsync(endpoint, json);
        var content = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
            return null;

        using var doc = JsonDocument.Parse(content);
        if (doc.RootElement.ValueKind == JsonValueKind.Number)
            return doc.RootElement.GetInt32();

        return null;
    }

    /// <summary>
    /// Sends an HTTP POST request with a JSON payload to the specified endpoint using Odoo authentication headers.
    /// </summary>
    /// <param name="endpoint">The URL of the endpoint to which the request is sent.</param>
    /// <param name="jsonBody">The JSON-formatted string to include in the request body.</param>
    /// <returns>A task representing the asynchronous operation, containing the HTTP response message.</returns>
    private async Task<HttpResponseMessage> SendRequestAsync(string endpoint, string jsonBody)
    {

        var databaseName = _settings.DatabaseName;
        var apiKey = _settings.ApiKey;

        var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = new StringContent(jsonBody, Encoding.UTF8, "application/json")
        };

        // Odoo 19 JSON-2 authentication: bearer token
        request.Headers.TryAddWithoutValidation("Authorization", $"bearer {apiKey}");

        // Required on SaaS when the domain resolves multiple databases
        if (!string.IsNullOrWhiteSpace(databaseName))
            request.Headers.TryAddWithoutValidation("X-Odoo-Database", _settings.DatabaseName);

        // Identify the integration for Odoo server logs
        request.Headers.TryAddWithoutValidation("User-Agent", "NopCommerce-OdooIntegration/1.0");

        var client = _httpClientFactory.CreateClient(OdooConstants.HttpClientName);
        client.Timeout = TimeSpan.FromMinutes(5);  // Set a longer timeout for potentially long-running requests

        return await client.SendAsync(request);
    }

    /// <summary>
    /// Invokes an asynchronous call to the Odoo API and deserializes the response to the specified type.
    /// </summary>
    /// <typeparam name="T">The type to which the API response is deserialized.</typeparam>
    /// <param name="model">The Odoo model to target.</param>
    /// <param name="method">The method to invoke on the specified model.</param>
    /// <param name="body">The request payload to send, or null for an empty body.</param>
    /// <param name="odooUrl">The base URL of the Odoo instance, or null to use the default.</param>
    /// <param name="databaseName">The name of the Odoo database, or null to use the default.</param>
    /// <param name="apiKey">The API key for authentication, or null to use the default.</param>
    /// <returns>A task representing the asynchronous operation, containing the deserialized response or null.</returns>
    /// <exception cref="OdooApiException">Thrown when the API response indicates an error.</exception>
    private async Task<T> CallAsync<T>(string model, string method, object body = null, string odooUrl = null,
        string databaseName = null, string apiKey = null)
    {
        var url = NormaliseUrl(odooUrl ?? _settings.OdooUrl);

        var endpoint = $"{url}/json/2/{model}/{method}";
        var json = body is null
            ? "{}"
            : JsonSerializer.Serialize(body, _jsonOptions);

        var response = await SendRequestAsync(endpoint, json);
        var content = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
            throw new OdooApiException(ParseOdooError(content) ?? $"HTTP {(int)response.StatusCode}: {response.ReasonPhrase}");

        using var doc = JsonDocument.Parse(content);
        return doc.RootElement.Deserialize<T>(_jsonOptions);
    }

    private static string NormaliseUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            throw new OdooApiException("Odoo URL is not configured.");

        return url.TrimEnd('/');
    }

    private static string ParseOdooError(string content)
    {
        try
        {
            using var doc = JsonDocument.Parse(content);
            if (doc.RootElement.TryGetProperty("error", out var error))
            {
                if (error.TryGetProperty("message", out var msg))
                    return msg.GetString();
                if (error.TryGetProperty("data", out var data) &&
                    data.TryGetProperty("message", out var dataMsg))
                    return dataMsg.GetString();
            }
        }
        catch { /* ignore parse failures */ }

        return null;
    }

    /// <summary>
    /// Reads a <c>search_read</c> response record by record, so one record Odoo returns in an
    /// unexpected shape is reported (with its id and the offending field's JSON path) instead of
    /// failing every record on the page.
    /// </summary>
    /// <typeparam name="T">The response class each record is read into.</typeparam>
    /// <param name="content">The response body: a JSON array of records.</param>
    private static OdooRecordPage<T> ReadRecordPage<T>(string content)
    {
        var records = new List<T>();
        var errors = new List<OdooRecordReadError>();

        using var doc = JsonDocument.Parse(content);
        foreach (var element in doc.RootElement.EnumerateArray())
        {
            try
            {
                records.Add(element.Deserialize<T>(_jsonOptions));
            }
            catch (JsonException ex)
            {
                int? odooId = element.ValueKind == JsonValueKind.Object
                    && element.TryGetProperty("id", out var id)
                    && id.TryGetInt32(out var value) ? value : null;

                errors.Add(new OdooRecordReadError(odooId, ex.Message));
            }
        }

        return new OdooRecordPage<T>(records, errors);
    }

    #endregion


    #region Product related enpoints

    /// <summary>
    /// Retrieves a list of products from the Odoo API asynchronously.
    /// </summary>
    /// <param name="limit">The maximum number of products to retrieve.</param>
    /// <param name="offset">The number of products to skip before starting to collect the result set.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the page, read product by
    /// product: a product Odoo returned in an unexpected shape is in its errors, not thrown.</returns>
    public async Task<OdooRecordPage<OdooProductResponse>> FetchProductsAsync(int limit = 100, int offset = 0, int odooProductId = 0)
    {
        var endpoint = $"{_settings.OdooUrl}/json/2/product.template/search_read";
        /*var limit = 100;
        var bodyObj = new
        {
            //fields = new[] { "id", "name", "display_name", "list_price", "default_code", "description_sale", "attribute_line_ids", "image_1920", "product_variant_ids" },

            //domain = new[] { new object[] { "sale_ok", "=", true } },
            domain = new object[] { new object[] { "active", "=", true } },
        //domain = Array.Empty<object>()   // empty = count all accessible products
        };*/
        endpoint = $"{_settings.OdooUrl}/json/2/product.template/search_read";
        var fields = new[] { "id", "name", "display_name", "default_code", "is_published", "active", "sale_ok",
            "categ_id","weight","barcode", "create_date", "write_date",
            "description_sale", "description", "description_ecommerce", "website_description", "website_published",
            "list_price","compare_list_price", "standard_price", "image_1920",
            "attribute_line_ids", "product_variant_count", "product_variant_ids",
            "x_studio_stores", "x_studio_departments", "x_studio_sync_revision" };

        var domain = new object[]
        {
            new object[] { "active", "=", true },
            new object[] { "sale_ok", "=", true }
        };

        if (odooProductId > 0)
        {
            domain = new object[]
            {
                new object[] { "active", "=", true },
                new object[] { "sale_ok", "=", true },
                new object[] { "id", "=", odooProductId }
            };
        }

        var bodyObj = new
        {
            fields = fields,
            limit = limit,
            offset = offset,
            domain = domain,
        };

        var json = JsonSerializer.Serialize(bodyObj, _jsonOptions);

        var response = await SendRequestAsync(endpoint, json);
        //var result = JsonConvert.DeserializeObject<OdooResult<T>>(responseString);
        //var response = await _httpClient.PostAsync(endpoint, body);
        var content = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
            throw new OdooApiException(ParseOdooError(content) ?? $"HTTP {(int)response.StatusCode}: {response.ReasonPhrase}");

        return ReadRecordPage<OdooProductResponse>(content);
    }

    /// <summary>
    /// Retrieves a list of product variants for the specified product template asynchronously.
    /// </summary>
    /// <param name="productId">The identifier of the product template to retrieve variants for.</param>
    /// <param name="limit">The maximum number of product variants to return.</param>
    /// <param name="offset">The number of product variants to skip before starting to collect the result set.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains a list of product variant responses.</returns>
    public async Task<List<ProductVariantResponse>> GetProductVariantsAsync(int productId, int limit = 100, int offset = 0)
    {
        var endpoint = $"{_settings.OdooUrl}/json/2/product.product/search_read";

        var bodyObj = new
        {
            // Only the fields the variant sync reads. Without a list Odoo returns every product.product
            // field (~175 KB per product, the image at five sizes), and each one must then match the
            // response class exactly — one Odoo-side type change (e.g. "tracking" = false) failed the
            // whole product's variants.
            fields = new[] { "id", "display_name", "active", "default_code", "barcode", "lst_price",
                "qty_available", "product_template_attribute_value_ids", "image_variant_1920", "image_1920" },
            domain = new object[] { new object[] { "product_tmpl_id", "=", productId } },
            limit = 200,
        };

        var json = JsonSerializer.Serialize(bodyObj, _jsonOptions);
        var response = await SendRequestAsync(endpoint, json);
        var content = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
            throw new OdooApiException(ParseOdooError(content) ?? $"HTTP {(int)response.StatusCode}: {response.ReasonPhrase}");

        return JsonSerializer.Deserialize<List<ProductVariantResponse>>(content, _jsonOptions)
            ?? new List<ProductVariantResponse>();
    }

    /// <summary>
    /// Retrieves product template attribute lines for a specific product template.
    /// </summary>
    /// <param name="productId">The product template ID from Odoo</param>
    /// <returns>List of product template attribute lines</returns>
    public async Task<List<ProductTemplateAttributeLineResponse>> GetProductTemplateAttributeLinesAsync(int productId)
    {
        try
        {
            var endpoint = $"{_settings.OdooUrl}/json/2/product.template.attribute.line/search_read";

            var bodyObj = new
            {
                // Only the fields the sync reads (see GetProductVariantsAsync for why).
                fields = new[] { "id", "display_name", "sequence", "attribute_id" },
                domain = new object[] { new object[] { "product_tmpl_id", "=", productId } }
            };

            var json = JsonSerializer.Serialize(bodyObj, _jsonOptions);
            var response = await SendRequestAsync(endpoint, json);

            var jsonResponse = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                throw new OdooApiException(ParseOdooError(jsonResponse) ?? $"HTTP {(int)response.StatusCode}: {response.ReasonPhrase}");

            var attributeLines = JsonSerializer.Deserialize<List<ProductTemplateAttributeLineResponse>>(jsonResponse, _jsonOptions)
                ?? new List<ProductTemplateAttributeLineResponse>();

            return attributeLines;
        }
        catch (OdooApiException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching product template attribute lines for product ID {ProductId}", productId);
            throw new OdooApiException($"Failed to fetch product template attribute lines for product {productId}", ex);
        }
    }

    /// <summary>
    /// Retrieves product template attribute values for a specific product template.
    /// These are the combination of attribute values specific to a product template.
    /// </summary>
    /// <param name="productId">The product template ID from Odoo</param>
    /// <returns>List of product template attribute values</returns>
    public async Task<List<ProductTemplateAttributeValueResponse>> GetProductTemplateAttributeValuesAsync(int productId)
    {
        try
        {
            var endpoint = $"{_settings.OdooUrl}/json/2/product.template.attribute.value/search_read";

            var bodyObj = new
            {
                // Only the fields the sync reads (see GetProductVariantsAsync for why).
                fields = new[] { "id", "name", "display_name", "attribute_id", "price_extra", "html_color" },
                domain = new object[] { new object[] { "product_tmpl_id", "=", productId } }
            };

            var json = JsonSerializer.Serialize(bodyObj, _jsonOptions);
            var response = await SendRequestAsync(endpoint, json);

            var jsonResponse = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                throw new OdooApiException(ParseOdooError(jsonResponse) ?? $"HTTP {(int)response.StatusCode}: {response.ReasonPhrase}");

            var attributeValues = JsonSerializer.Deserialize<List<ProductTemplateAttributeValueResponse>>(jsonResponse, _jsonOptions)
                ?? new List<ProductTemplateAttributeValueResponse>();

            return attributeValues;
        }
        catch (OdooApiException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching product template attribute values for product ID {ProductId}", productId);
            throw new OdooApiException($"Failed to fetch product template attribute values for product {productId}", ex);
        }
    }

    /// <summary>
    /// Retrieves product attributes for a specific product template.
    /// Returns all attributes associated with the given product template ID using an "in" domain operator.
    /// </summary>
    /// <param name="productTemplateId">The product template ID from Odoo</param>
    /// <returns>List of product attributes</returns>
    public async Task<List<ProductAttributeResponse>> GetProductAttributesAsync(int productTemplateId = 0)
    {
        try
        {
            var endpoint = $"{_settings.OdooUrl}/json/2/product.attribute/search_read";

            // Only the fields the sync reads (see GetProductVariantsAsync for why).
            var fields = new[] { "id", "name", "display_name", "create_variant", "display_type", "sequence",
                "attribute_line_ids", "visibility" };

            var bodyObj = new
            {
                fields,
                domain = productTemplateId > 0
                    ? new object[] { new object[] { "product_tmpl_ids", "in", productTemplateId } }
                    : Array.Empty<object>()
            };


            var json = JsonSerializer.Serialize(bodyObj, _jsonOptions);
            var response = await SendRequestAsync(endpoint, json);

            var jsonResponse = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                throw new OdooApiException(ParseOdooError(jsonResponse) ?? $"HTTP {(int)response.StatusCode}: {response.ReasonPhrase}");

            var attributes = JsonSerializer.Deserialize<List<ProductAttributeResponse>>(jsonResponse, _jsonOptions)
                ?? new List<ProductAttributeResponse>();

            return attributes;
        }
        catch (OdooApiException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching product attributes for product template ID {ProductTemplateId}", productTemplateId);
            throw new OdooApiException($"Failed to fetch product attributes for product template {productTemplateId}", ex);
        }
    }

    #endregion

    #region Partner / Branch endpoints

    /// <summary>
    /// Fetches all Odoo stores (x_store), including their linked companies and departments.
    /// </summary>
    public async Task<List<OdooStoreResponse>> FetchStoresAsync()
    {
        var endpoint = $"{_settings.OdooUrl}/json/2/x_store/search_read";

        var fields = new[]
        {
            "id", "display_name", "x_name", "x_active", "x_studio_sequence",
            "x_studio_sync_revision", "x_studio_store_url",
            "x_studio_departments", "x_studio_companies", "write_date"
        };

        var bodyObj = new
        {
            fields,
            // Naming the active field explicitly disables Odoo's implicit "hide archived" filter,
            // so archived stores are returned too (and can be recognised as archived).
            domain = new object[] { new object[] { "x_active", "in", new[] { true, false } } }
        };

        var json = JsonSerializer.Serialize(bodyObj, _jsonOptions);
        var response = await SendRequestAsync(endpoint, json);
        var content = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
            throw new OdooApiException(ParseOdooError(content) ?? $"HTTP {(int)response.StatusCode}: {response.ReasonPhrase}");

        return JsonSerializer.Deserialize<List<OdooStoreResponse>>(content, _jsonOptions)
            ?? new List<OdooStoreResponse>();
    }

    /// <summary>
    /// Fetches all Odoo departments (x_department), including their parent store.
    /// </summary>
    public async Task<List<OdooDepartmentResponse>> FetchDepartmentsAsync()
    {
        var endpoint = $"{_settings.OdooUrl}/json/2/x_department/search_read";

        var fields = new[]
        {
            "id", "display_name", "x_name", "x_active", "x_studio_sequence",
            "x_studio_sync_revision", "x_studio_store"
        };

        var bodyObj = new
        {
            fields,
            // Include archived departments so they can be deactivated in NOP (see FetchStoresAsync).
            domain = new object[] { new object[] { "x_active", "in", new[] { true, false } } }
        };

        var json = JsonSerializer.Serialize(bodyObj, _jsonOptions);
        var response = await SendRequestAsync(endpoint, json);
        var content = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
            throw new OdooApiException(ParseOdooError(content) ?? $"HTTP {(int)response.StatusCode}: {response.ReasonPhrase}");

        return JsonSerializer.Deserialize<List<OdooDepartmentResponse>>(content, _jsonOptions)
            ?? new List<OdooDepartmentResponse>();
    }

    /// <summary>
    /// Fetches a page of archived product templates (id + default_code only), so products archived
    /// in Odoo can be unpublished in NOP. Read product by product, like <see cref="FetchProductsAsync"/>.
    /// </summary>
    public async Task<OdooRecordPage<OdooProductResponse>> FetchArchivedProductsAsync(int limit, int offset)
    {
        var endpoint = $"{_settings.OdooUrl}/json/2/product.template/search_read";

        var bodyObj = new
        {
            fields = new[] { "id", "default_code" },
            // An explicit condition on "active" disables the implicit filter, so archived rows return.
            domain = new object[] { new object[] { "active", "=", false } },
            limit,
            offset,
            order = "id"
        };

        var json = JsonSerializer.Serialize(bodyObj, _jsonOptions);
        var response = await SendRequestAsync(endpoint, json);
        var content = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
            throw new OdooApiException(ParseOdooError(content) ?? $"HTTP {(int)response.StatusCode}: {response.ReasonPhrase}");

        return ReadRecordPage<OdooProductResponse>(content);
    }

    /// <inheritdoc/>
    public async Task<List<OdooPartnerResponse>> GetDeliveryAddressesAsync(int parentPartnerId)
    {
        try
        {
            var endpoint = $"{_settings.OdooUrl}/json/2/res.partner/search_read";

            var bodyObj = new
            {
                domain = new object[]
                {
                    new object[] { "parent_id", "in", parentPartnerId },
                    new object[] { "type", "=", "delivery" },
                    // Include archived addresses so their branches can be deactivated in NOP.
                    new object[] { "active", "in", new[] { true, false } }
                },
                fields = PartnerFields
            };

            var json = JsonSerializer.Serialize(bodyObj, _jsonOptions);
            var response = await SendRequestAsync(endpoint, json);
            var content = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                throw new OdooApiException(ParseOdooError(content) ?? $"HTTP {(int)response.StatusCode}: {response.ReasonPhrase}");

            return JsonSerializer.Deserialize<List<OdooPartnerResponse>>(content, _jsonOptions)
                ?? new List<OdooPartnerResponse>();
        }
        catch (OdooApiException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching delivery addresses for Odoo partner {PartnerId}", parentPartnerId);
            throw new OdooApiException($"Failed to fetch delivery addresses for partner {parentPartnerId}", ex);
        }
    }

    /// <inheritdoc/>
    public async Task<List<OdooPartnerResponse>> GetPartnersByIdsAsync(IList<int> partnerIds)
    {
        if (partnerIds == null || partnerIds.Count == 0)
            return new List<OdooPartnerResponse>();

        var endpoint = $"{_settings.OdooUrl}/json/2/res.partner/search_read";

        var bodyObj = new
        {
            domain = new object[]
            {
                new object[] { "id", "in", partnerIds },
                // Include archived partners so their branches can be deactivated in NOP.
                new object[] { "active", "in", new[] { true, false } }
            },
            fields = PartnerFields
        };

        var json = JsonSerializer.Serialize(bodyObj, _jsonOptions);
        var response = await SendRequestAsync(endpoint, json);
        var content = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
            throw new OdooApiException(ParseOdooError(content) ?? $"HTTP {(int)response.StatusCode}: {response.ReasonPhrase}");

        return JsonSerializer.Deserialize<List<OdooPartnerResponse>>(content, _jsonOptions)
            ?? new List<OdooPartnerResponse>();
    }

    /// <summary>
    /// The res.partner fields read for companies (head offices) and delivery addresses (branches) —
    /// only what the branch sync actually uses. Odoo rejects the whole request if any requested field
    /// does not exist (saas~19.4 dropped <c>company_type</c>), so nothing speculative belongs here.
    /// Fields used only in a domain (<c>type</c>, <c>parent_id</c>) need not be fetched.
    /// </summary>
    private static readonly string[] PartnerFields =
    {
        "id", "name", "active",
        "street", "street2", "city", "zip", "state_id", "country_id",
        "phone", "email"
    };

    #endregion


    #region Connection

    /// Tests the connection to an Odoo server and retrieves server version, user context, and product count.
    /// </summary>
    /// <param name="odooUrl">The base URL of the Odoo server.</param>
    /// <param name="databaseName">The name of the Odoo database to connect to.</param>
    /// <param name="apiKey">The API key used for authentication.</param>
    /// <returns>An OdooConnectionResult containing connection status, server version, user information, product count, and error
    /// details if any.</returns>

    public async Task<OdooConnectionResult> TestConnectionAsync(
        string odooUrl,
        string databaseName,
        string apiKey)
    {
        odooUrl = NormaliseUrl(odooUrl);

        try
        {
            // ── Step 1: Retrieve server version (no auth required) ────────────
            var versionInfo = await GetVersionAsync(odooUrl);

            // ── Step 2: Verify API key by resolving the current user context ──
            var userContext = await CallAsync<OdooUserContext>(
                "res.users",
                "context_get",
                body: new { },
                odooUrl: odooUrl,
                databaseName: databaseName,
                apiKey: apiKey);

            // ── Step 3: Count published products ─────────────────────────────
            var productCount = await CountProductsAsync(odooUrl, databaseName, apiKey);

            return new OdooConnectionResult(
                Success: true,
                OdooVersion: versionInfo,
                DatabaseName: databaseName,
                // context_get answers the API user's id, language and time zone, not the login
                LoggedInUser: userContext?.Uid is int uid ? $"user id {uid}" : "unknown",
                ProductCount: productCount,
                ErrorMessage: null
            );
        }
        catch (OdooApiException ex)
        {
            _logger.LogWarning(ex, "Odoo connection test failed: {Message}", ex.Message);
            return new OdooConnectionResult(
                Success: false,
                OdooVersion: null,
                DatabaseName: databaseName,
                LoggedInUser: null,
                ProductCount: null,
                ErrorMessage: ex.Message
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during Odoo connection test");
            return new OdooConnectionResult(
                Success: false,
                OdooVersion: null,
                DatabaseName: databaseName,
                LoggedInUser: null,
                ProductCount: null,
                ErrorMessage: $"Unexpected error: {ex.Message}"
            );
        }
    }

    #endregion

    #region Internal DTOs used by the service

    private sealed class OdooUserContext
    {
        [JsonPropertyName("lang")]
        public string Lang { get; set; }

        [JsonPropertyName("tz")]
        public string Timezone { get; set; }

        [JsonPropertyName("uid")]
        public int? Uid { get; set; }
    }

    #endregion
}

/// <summary>
/// Thrown when the Odoo API returns an error response.
/// </summary>
public class OdooApiException : Exception
{
    public OdooApiException(string message) : base(message) { }
    public OdooApiException(string message, Exception innerException) : base(message, innerException) { }
}



