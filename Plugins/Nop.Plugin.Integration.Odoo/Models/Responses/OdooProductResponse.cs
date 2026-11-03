using System.Text.Json;
using System.Text.Json.Serialization;

namespace Nop.Plugin.Integration.Odoo.Models.Responses;

public class OdooProductResponse
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("display_name")]
    public string DisplayName { get; set; }

    [JsonPropertyName("website_id")]
    public Many2One WebsiteId { get; set; }

    [JsonPropertyName("website_published")]
    public bool WebsitePublished { get; set; }

    [JsonPropertyName("is_published")]
    public bool IsPublished { get; set; }

    [JsonPropertyName("can_publish")]
    public bool CanPublish { get; set; }

    [JsonPropertyName("website_url")]
    public string WebsiteUrl { get; set; }

    [JsonPropertyName("website_absolute_url")]
    public string WebsiteAbsoluteUrl { get; set; }

    [JsonPropertyName("is_seo_optimized")]
    public bool IsSeoOptimized { get; set; }

    [JsonPropertyName("website_meta_title")]
    public bool WebsiteMetaTitle { get; set; }

    [JsonPropertyName("website_meta_description")]
    public bool WebsiteMetaDescription { get; set; }

    [JsonPropertyName("website_meta_keywords")]
    public bool WebsiteMetaKeywords { get; set; }

    [JsonPropertyName("website_meta_og_img")]
    public bool WebsiteMetaOgImg { get; set; }

    [JsonPropertyName("seo_name")]
    [JsonConverter(typeof(StringOrFalseJsonConverter))]
    public string SeoName { get; set; }

    [JsonPropertyName("image_1920")]
    [JsonConverter(typeof(OdooBinaryJsonConverter))]
    public string Image1920 { get; set; }

    [JsonPropertyName("image_1024")]
    [JsonConverter(typeof(OdooBinaryJsonConverter))]
    public string Image1024 { get; set; }

    [JsonPropertyName("image_512")]
    [JsonConverter(typeof(OdooBinaryJsonConverter))]
    public string Image512 { get; set; }

    [JsonPropertyName("image_256")]
    [JsonConverter(typeof(OdooBinaryJsonConverter))]
    public string Image256 { get; set; }

    [JsonPropertyName("image_128")]
    [JsonConverter(typeof(OdooBinaryJsonConverter))]
    public string Image128 { get; set; }

    [JsonPropertyName("activity_ids")]
    public List<int> ActivityIds { get; set; }

    [JsonPropertyName("activity_state")]
    public bool? ActivityState { get; set; }

    [JsonPropertyName("activity_user_id")]
    public bool? ActivityUserId { get; set; }

    [JsonPropertyName("activity_type_id")]
    public bool? ActivityTypeId { get; set; }

    [JsonPropertyName("activity_type_icon")]
    public bool? ActivityTypeIcon { get; set; }

    [JsonPropertyName("activity_date_deadline")]
    public bool? ActivityDateDeadline { get; set; }

    [JsonPropertyName("my_activity_date_deadline")]
    public bool? MyActivityDateDeadline { get; set; }

    [JsonPropertyName("activity_summary")]
    public bool? ActivitySummary { get; set; }

    [JsonPropertyName("activity_exception_decoration")]
    public bool? ActivityExceptionDecoration { get; set; }

    [JsonPropertyName("activity_exception_icon")]
    public bool? ActivityExceptionIcon { get; set; }

    [JsonPropertyName("activity_calendar_event_id")]
    public bool? ActivityCalendarEventId { get; set; }

    [JsonPropertyName("message_is_follower")]
    public bool MessageIsFollower { get; set; }

    [JsonPropertyName("message_follower_ids")]
    public List<int> MessageFollowerIds { get; set; }

    [JsonPropertyName("message_partner_ids")]
    public List<int> MessagePartnerIds { get; set; }

    [JsonPropertyName("message_ids")]
    public List<int> MessageIds { get; set; }

    [JsonPropertyName("has_message")]
    public bool HasMessage { get; set; }

    [JsonPropertyName("message_needaction")]
    public bool MessageNeedAction { get; set; }

    [JsonPropertyName("message_needaction_counter")]
    public int MessageNeedActionCounter { get; set; }

    [JsonPropertyName("message_has_error")]
    public bool MessageHasError { get; set; }

    [JsonPropertyName("message_has_error_counter")]
    public int MessageHasErrorCounter { get; set; }

    [JsonPropertyName("message_attachment_count")]
    public int MessageAttachmentCount { get; set; }

    [JsonPropertyName("rating_ids")]
    public List<int> RatingIds { get; set; }

    [JsonPropertyName("website_message_ids")]
    public List<int> WebsiteMessageIds { get; set; }

    [JsonPropertyName("message_has_sms_error")]
    public bool MessageHasSmsError { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; }

    [JsonPropertyName("sequence")]
    public int Sequence { get; set; }

    [JsonPropertyName("description")]
    [JsonConverter(typeof(StringOrFalseJsonConverter))]
    public string Description { get; set; }

    [JsonPropertyName("description_purchase")]
    [JsonConverter(typeof(StringOrFalseJsonConverter))]
    public string DescriptionPurchase { get; set; }

    [JsonPropertyName("description_sale")]
    [JsonConverter(typeof(StringOrFalseJsonConverter))]
    public string DescriptionSale { get; set; }

    [JsonPropertyName("type")]
    public string Type { get; set; }

    [JsonPropertyName("combo_ids")]
    public List<int> ComboIds { get; set; }

    [JsonPropertyName("service_tracking")]
    public string ServiceTracking { get; set; }

    [JsonPropertyName("categ_id")]
    public Many2One CategId { get; set; }

    [JsonPropertyName("currency_id")]
    public JsonElement CurrencyId { get; set; }

    [JsonPropertyName("cost_currency_id")]
    public JsonElement CostCurrencyId { get; set; }

    [JsonPropertyName("list_price")]
    public double ListPrice { get; set; }

    [JsonPropertyName("standard_price")]
    public double StandardPrice { get; set; }

    [JsonPropertyName("volume")]
    public double Volume { get; set; }

    [JsonPropertyName("volume_uom_name")]
    public string VolumeUomName { get; set; }

    [JsonPropertyName("weight")]
    public double Weight { get; set; }

    [JsonPropertyName("weight_uom_name")]
    public string WeightUomName { get; set; }

    [JsonPropertyName("sale_ok")]
    public bool SaleOk { get; set; }

    [JsonPropertyName("purchase_ok")]
    public bool PurchaseOk { get; set; }

    [JsonPropertyName("uom_id")]
    public JsonElement UomId { get; set; }

    [JsonPropertyName("uom_ids")]
    public List<int> UomIds { get; set; }

    [JsonPropertyName("uom_name")]
    public string UomName { get; set; }

    [JsonPropertyName("company_id")]
    public bool? CompanyId { get; set; }

    [JsonPropertyName("seller_ids")]
    public List<int> SellerIds { get; set; }

    [JsonPropertyName("variant_seller_ids")]
    public List<int> VariantSellerIds { get; set; }

    [JsonPropertyName("active")]
    public bool Active { get; set; }

    [JsonPropertyName("color")]
    public int Color { get; set; }

    [JsonPropertyName("is_product_variant")]
    public bool IsProductVariant { get; set; }

    [JsonPropertyName("attribute_line_ids")]
    public List<int> AttributeLineIds { get; set; }

    [JsonPropertyName("valid_product_template_attribute_line_ids")]
    public List<int> ValidProductTemplateAttributeLineIds { get; set; }

    [JsonPropertyName("product_variant_ids")]
    public List<int> ProductVariantIds { get; set; }

    [JsonPropertyName("product_variant_id")]
    public JsonElement ProductVariantId { get; set; }

    [JsonPropertyName("product_variant_count")]
    public int ProductVariantCount { get; set; }

    [JsonPropertyName("barcode")]
    [JsonConverter(typeof(StringOrFalseJsonConverter))]
    public string Barcode { get; set; }

    [JsonPropertyName("default_code")]
    [JsonConverter(typeof(StringOrFalseJsonConverter))]
    public string DefaultCode { get; set; }

    [JsonPropertyName("pricelist_rule_ids")]
    public List<int> PricelistRuleIds { get; set; }

    [JsonPropertyName("product_document_ids")]
    public List<int> ProductDocumentIds { get; set; }

    [JsonPropertyName("product_document_count")]
    public int ProductDocumentCount { get; set; }

    [JsonPropertyName("can_image_1024_be_zoomed")]
    public bool CanImage1024BeZoomed { get; set; }

    [JsonPropertyName("has_configurable_attributes")]
    public bool HasConfigurableAttributes { get; set; }

    [JsonPropertyName("is_dynamically_created")]
    public bool IsDynamicallyCreated { get; set; }

    [JsonPropertyName("product_tooltip")]
    public string ProductTooltip { get; set; }

    [JsonPropertyName("is_favorite")]
    public bool IsFavorite { get; set; }

    [JsonPropertyName("product_tag_ids")]
    public List<int> ProductTagIds { get; set; }

    [JsonPropertyName("product_properties")]
    public List<int> ProductProperties { get; set; }

    [JsonPropertyName("create_uid")]
    public JsonElement CreateUid { get; set; }

    [JsonPropertyName("create_date")]
    public string CreateDate { get; set; }

    [JsonPropertyName("write_uid")]
    public JsonElement WriteUid { get; set; }

    [JsonPropertyName("write_date")]
    public string WriteDate { get; set; }

    [JsonPropertyName("taxes_id")]
    public List<int> TaxesId { get; set; }

    [JsonPropertyName("tax_string")]
    public string TaxString { get; set; }

    [JsonPropertyName("supplier_taxes_id")]
    public List<int> SupplierTaxesId { get; set; }

    [JsonPropertyName("property_account_income_id")]
    public JsonElement PropertyAccountIncomeId { get; set; }

    [JsonPropertyName("property_account_expense_id")]
    public JsonElement PropertyAccountExpenseId { get; set; }

    [JsonPropertyName("account_tag_ids")]
    public List<int> AccountTagIds { get; set; }

    [JsonPropertyName("fiscal_country_codes")]
    public string FiscalCountryCodes { get; set; }

    [JsonPropertyName("is_storable")]
    public bool IsStorable { get; set; }

    [JsonPropertyName("responsible_id")]
    public JsonElement ResponsibleId { get; set; }

    [JsonPropertyName("property_stock_production")]
    public JsonElement PropertyStockProduction { get; set; }

    [JsonPropertyName("property_stock_inventory")]
    public JsonElement PropertyStockInventory { get; set; }

    [JsonPropertyName("sale_delay")]
    public int SaleDelay { get; set; }

    [JsonPropertyName("tracking")]
    public string Tracking { get; set; }

    [JsonPropertyName("lot_sequence_id")]
    public JsonElement LotSequenceId { get; set; }

    [JsonPropertyName("serial_prefix_format")]
    public string SerialPrefixFormat { get; set; }

    [JsonPropertyName("next_serial")]
    public string NextSerial { get; set; }

    [JsonPropertyName("description_picking")]
    [JsonConverter(typeof(StringOrFalseJsonConverter))]
    public string DescriptionPicking { get; set; }

    [JsonPropertyName("description_pickingout")]
    [JsonConverter(typeof(StringOrFalseJsonConverter))]
    public string DescriptionPickingout { get; set; }

    [JsonPropertyName("description_pickingin")]
    [JsonConverter(typeof(StringOrFalseJsonConverter))]
    public string DescriptionPickingin { get; set; }

    [JsonPropertyName("qty_available")]
    public double QtyAvailable { get; set; }

    [JsonPropertyName("virtual_available")]
    public double VirtualAvailable { get; set; }

    [JsonPropertyName("incoming_qty")]
    public double IncomingQty { get; set; }

    [JsonPropertyName("outgoing_qty")]
    public double OutgoingQty { get; set; }

    [JsonPropertyName("location_id")]
    public JsonElement LocationId { get; set; }

    [JsonPropertyName("warehouse_id")]
    [JsonConverter(typeof(IntOrFalseJsonConverter))]
    public int? WarehouseId { get; set; }

    [JsonPropertyName("has_available_route_ids")]
    public bool HasAvailableRouteIds { get; set; }

    [JsonPropertyName("route_ids")]
    public List<int> RouteIds { get; set; }

    [JsonPropertyName("nbr_moves_in")]
    public int NbrMovesIn { get; set; }

    [JsonPropertyName("nbr_moves_out")]
    public int NbrMovesOut { get; set; }

    [JsonPropertyName("nbr_reordering_rules")]
    public int NbrReorderingRules { get; set; }

    [JsonPropertyName("reordering_min_qty")]
    public double ReorderingMinQty { get; set; }

    [JsonPropertyName("reordering_max_qty")]
    public double ReorderingMaxQty { get; set; }

    [JsonPropertyName("route_from_categ_ids")]
    public List<int> RouteFromCategIds { get; set; }

    [JsonPropertyName("show_on_hand_qty_status_button")]
    public bool ShowOnHandQtyStatusButton { get; set; }

    [JsonPropertyName("show_forecasted_qty_status_button")]
    public bool ShowForecastedQtyStatusButton { get; set; }

    [JsonPropertyName("show_qty_update_button")]
    public bool ShowQtyUpdateButton { get; set; }

    [JsonPropertyName("bom_line_ids")]
    public List<int> BomLineIds { get; set; }

    [JsonPropertyName("bom_ids")]
    public List<int> BomIds { get; set; }

    [JsonPropertyName("bom_count")]
    public int BomCount { get; set; }

    [JsonPropertyName("used_in_bom_count")]
    public int UsedInBomCount { get; set; }

    [JsonPropertyName("mrp_product_qty")]
    public double MrpProductQty { get; set; }

    [JsonPropertyName("is_kits")]
    public bool IsKits { get; set; }

    [JsonPropertyName("purchased_product_qty")]
    public double PurchasedProductQty { get; set; }

    [JsonPropertyName("purchase_method")]
    public string PurchaseMethod { get; set; }

    [JsonPropertyName("purchase_line_warn_msg")]
    [JsonConverter(typeof(StringOrFalseJsonConverter))]
    public string PurchaseLineWarnMsg { get; set; }

    [JsonPropertyName("cost_method")]
    public string CostMethod { get; set; }

    [JsonPropertyName("valuation")]
    public string Valuation { get; set; }

    [JsonPropertyName("lot_valuated")]
    public bool LotValuated { get; set; }

    [JsonPropertyName("property_price_difference_account_id")]
    public JsonElement PropertyPriceDifferenceAccountId { get; set; }

    [JsonPropertyName("service_type")]
    public string ServiceType { get; set; }

    [JsonPropertyName("sale_line_warn_msg")]
    [JsonConverter(typeof(StringOrFalseJsonConverter))]
    public string SaleLineWarnMsg { get; set; }

    [JsonPropertyName("expense_policy")]
    public string ExpensePolicy { get; set; }

    [JsonPropertyName("visible_expense_policy")]
    public bool VisibleExpensePolicy { get; set; }

    [JsonPropertyName("sales_count")]
    [JsonConverter(typeof(DoubleOrFalseJsonConverter))]
    public double? SalesCount { get; set; }

    [JsonPropertyName("invoice_policy")]
    public string InvoicePolicy { get; set; }

    [JsonPropertyName("optional_product_ids")]
    public List<int> OptionalProductIds { get; set; }

    [JsonPropertyName("schedule_count")]
    public int ScheduleCount { get; set; }

    [JsonPropertyName("product_add_mode")]
    public string ProductAddMode { get; set; }

    [JsonPropertyName("service_to_purchase")]
    public bool ServiceToPurchase { get; set; }

    [JsonPropertyName("service_policy")]
    [JsonConverter(typeof(StringOrFalseJsonConverter))]
    public string ServicePolicy { get; set; }

    [JsonPropertyName("landed_cost_ok")]
    public bool LandedCostOk { get; set; }

    [JsonPropertyName("split_method_landed_cost")]
    public bool SplitMethodLandedCost { get; set; }

    [JsonPropertyName("hs_code")]
    [JsonConverter(typeof(StringOrFalseJsonConverter))]
    public string HsCode { get; set; }

    [JsonPropertyName("country_of_origin")]
    public Many2One CountryOfOrigin { get; set; }

    [JsonPropertyName("rating_last_value")]
    public double RatingLastValue { get; set; }

    [JsonPropertyName("rating_last_feedback")]
    [JsonConverter(typeof(StringOrFalseJsonConverter))]
    public string RatingLastFeedback { get; set; }

    [JsonPropertyName("rating_last_image")]
    [JsonConverter(typeof(StringOrFalseJsonConverter))]
    public string RatingLastImage { get; set; }

    [JsonPropertyName("rating_count")]
    public int RatingCount { get; set; }

    [JsonPropertyName("rating_avg")]
    public double RatingAvg { get; set; }

    [JsonPropertyName("rating_avg_text")]
    public string RatingAvgText { get; set; }

    [JsonPropertyName("rating_percentage_satisfaction")]
    public double RatingPercentageSatisfaction { get; set; }

    [JsonPropertyName("rating_last_text")]
    [JsonConverter(typeof(StringOrFalseJsonConverter))]
    public string RatingLastText { get; set; }

    [JsonPropertyName("website_description")]
    [JsonConverter(typeof(StringOrFalseJsonConverter))]
    public string WebsiteDescription { get; set; }

    [JsonPropertyName("description_ecommerce")]
    [JsonConverter(typeof(StringOrFalseJsonConverter))]
    public string DescriptionEcommerce { get; set; }

    [JsonPropertyName("alternative_product_ids")]
    public List<int> AlternativeProductIds { get; set; }

    [JsonPropertyName("accessory_product_ids")]
    public List<int> AccessoryProductIds { get; set; }

    [JsonPropertyName("website_size_x")]
    public int WebsiteSizeX { get; set; }

    [JsonPropertyName("website_size_y")]
    public int WebsiteSizeY { get; set; }

    [JsonPropertyName("website_ribbon_id")]
    public Many2One WebsiteRibbonId { get; set; }

    [JsonPropertyName("website_sequence")]
    public int WebsiteSequence { get; set; }

    [JsonPropertyName("public_categ_ids")]
    public List<int> PublicCategIds { get; set; }

    [JsonPropertyName("publish_date")]
    public string PublishDate { get; set; }

    [JsonPropertyName("product_template_image_ids")]
    public List<int> ProductTemplateImageIds { get; set; }

    [JsonPropertyName("base_unit_count")]
    public double BaseUnitCount { get; set; }

    [JsonPropertyName("base_unit_id")]
    public bool? BaseUnitId { get; set; }

    [JsonPropertyName("base_unit_price")]
    public double BaseUnitPrice { get; set; }

    [JsonPropertyName("base_unit_name")]
    public string BaseUnitName { get; set; }

    [JsonPropertyName("compare_list_price")]
    public double CompareListPrice { get; set; }

    [JsonPropertyName("variants_default_code")]
    [JsonConverter(typeof(StringOrFalseJsonConverter))]
    public string VariantsDefaultCode { get; set; }

    [JsonPropertyName("project_id")]
    //[JsonConverter(typeof(IntOrFalseJsonConverter))]
    public JsonElement ProjectId { get; set; }

    [JsonPropertyName("project_template_id")]
    public JsonElement ProjectTemplateId { get; set; }

    [JsonPropertyName("task_template_id")]
    public JsonElement TaskTemplateId { get; set; }

    [JsonPropertyName("allow_out_of_stock_order")]
    public bool AllowOutOfStockOrder { get; set; }

    [JsonPropertyName("available_threshold")]
    [JsonConverter(typeof(DoubleOrFalseJsonConverter))]
    public double? AvailableThreshold { get; set; }

    [JsonPropertyName("show_availability")]
    public bool ShowAvailability { get; set; }

    [JsonPropertyName("out_of_stock_message")]
    [JsonConverter(typeof(StringOrFalseJsonConverter))]
    public string OutOfStockMessage { get; set; }

    [JsonPropertyName("service_upsell_threshold")]
    [JsonConverter(typeof(DoubleOrFalseJsonConverter))]
    public double? ServiceUpsellThreshold { get; set; }

    [JsonPropertyName("service_upsell_threshold_ratio")]
    public bool? ServiceUpsellThresholdRatio { get; set; }

    // ── CW custom Studio fields ──────────────────────────────────────────────

    /// <summary>Many-to-many ids of the stores this product is limited to (→ x_store).</summary>
    [JsonPropertyName("x_studio_stores")]
    public List<int> XStudioStores { get; set; } = new();

    /// <summary>Many-to-many ids of the departments this product belongs to (→ x_department).</summary>
    [JsonPropertyName("x_studio_departments")]
    public List<int> XStudioDepartments { get; set; } = new();

    /// <summary>Change counter — used for incremental sync (stored per product as a generic attribute).</summary>
    [JsonPropertyName("x_studio_sync_revision")]
    public int XStudioSyncRevision { get; set; }
}
