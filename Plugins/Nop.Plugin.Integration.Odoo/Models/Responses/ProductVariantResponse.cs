using System.Text.Json;
using System.Text.Json.Serialization;

namespace Nop.Plugin.Integration.Odoo.Models.Responses;

public class ProductVariantResponse
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("display_name")]
    public string DisplayName { get; set; }

    [JsonPropertyName("activity_ids")]
    public List<int> ActivityIds { get; set; }

    [JsonPropertyName("activity_state")]
    public bool? ActivityState { get; set; }

    [JsonPropertyName("activity_user_id")]
    public JsonElement ActivityUserId { get; set; }

    [JsonPropertyName("activity_type_id")]
    public JsonElement ActivityTypeId { get; set; }

    [JsonPropertyName("activity_type_icon")]
    [JsonConverter(typeof(StringOrFalseJsonConverter))]
    public string ActivityTypeIcon { get; set; }

    [JsonPropertyName("activity_date_deadline")]
    [JsonConverter(typeof(StringOrFalseJsonConverter))]
    public string ActivityDateDeadline { get; set; }

    [JsonPropertyName("my_activity_date_deadline")]
    [JsonConverter(typeof(StringOrFalseJsonConverter))]
    public string MyActivityDateDeadline { get; set; }

    [JsonPropertyName("activity_summary")]
    [JsonConverter(typeof(StringOrFalseJsonConverter))]
    public string ActivitySummary { get; set; }

    [JsonPropertyName("activity_exception_decoration")]
    [JsonConverter(typeof(StringOrFalseJsonConverter))]
    public string ActivityExceptionDecoration { get; set; }

    [JsonPropertyName("activity_exception_icon")]
    [JsonConverter(typeof(StringOrFalseJsonConverter))]
    public string ActivityExceptionIcon { get; set; }

    [JsonPropertyName("activity_calendar_event_id")]
    [JsonConverter(typeof(StringOrFalseJsonConverter))]
    public string ActivityCalendarEventId { get; set; }

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
    public bool MessageNeedaction { get; set; }

    [JsonPropertyName("message_needaction_counter")]
    public int MessageNeedactionCounter { get; set; }

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

    [JsonPropertyName("price_extra")]
    public double PriceExtra { get; set; }

    [JsonPropertyName("lst_price")]
    public double LstPrice { get; set; }

    [JsonPropertyName("default_code")]
    [JsonConverter(typeof(StringOrFalseJsonConverter))]
    public string DefaultCode { get; set; }

    [JsonPropertyName("code")]
    [JsonConverter(typeof(StringOrFalseJsonConverter))]
    public string Code { get; set; }

    [JsonPropertyName("partner_ref")]
    public string PartnerReference { get; set; }

    [JsonPropertyName("active")]
    public bool Active { get; set; }

    [JsonPropertyName("product_tmpl_id")]
    public Many2One ProductTemplate { get; set; }

    [JsonPropertyName("barcode")]
    [JsonConverter(typeof(StringOrFalseJsonConverter))]
    public string Barcode { get; set; }

    [JsonPropertyName("product_uom_ids")]
    public List<int> ProductUomIds { get; set; }

    [JsonPropertyName("product_template_attribute_value_ids")]
    public List<int> ProductTemplateAttributeValueIds { get; set; }

    [JsonPropertyName("product_template_variant_value_ids")]
    public List<int> ProductTemplateVariantValueIds { get; set; }

    [JsonPropertyName("combination_indices")]
    public string CombinationIndices { get; set; }

    [JsonPropertyName("is_product_variant")]
    public bool IsProductVariant { get; set; }

    [JsonPropertyName("standard_price")]
    public double StandardPrice { get; set; }

    [JsonPropertyName("volume")]
    public double Volume { get; set; }

    [JsonPropertyName("weight")]
    public double Weight { get; set; }

    [JsonPropertyName("pricelist_rule_ids")]
    public List<int> PricelistRuleIds { get; set; }

    [JsonPropertyName("product_document_ids")]
    public List<int> ProductDocumentIds { get; set; }

    [JsonPropertyName("product_document_count")]
    public int ProductDocumentCount { get; set; }

    [JsonPropertyName("additional_product_tag_ids")]
    public List<int> AdditionalProductTagIds { get; set; }

    [JsonPropertyName("all_product_tag_ids")]
    public List<int> AllProductTagIds { get; set; }

    [JsonPropertyName("image_variant_1920")]
    [JsonConverter(typeof(OdooBinaryJsonConverter))]
    public string ImageVariant1920 { get; set; }

    [JsonPropertyName("image_variant_1024")]
    [JsonConverter(typeof(OdooBinaryJsonConverter))]
    public string ImageVariant1024 { get; set; }

    [JsonPropertyName("image_variant_512")]
    [JsonConverter(typeof(OdooBinaryJsonConverter))]
    public string ImageVariant512 { get; set; }

    [JsonPropertyName("image_variant_256")]
    [JsonConverter(typeof(OdooBinaryJsonConverter))]
    public string ImageVariant256 { get; set; }

    [JsonPropertyName("image_variant_128")]
    [JsonConverter(typeof(OdooBinaryJsonConverter))]
    public string ImageVariant128 { get; set; }

    [JsonPropertyName("can_image_variant_1024_be_zoomed")]
    public bool CanImageVariant1024BeZoomed { get; set; }

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

    [JsonPropertyName("can_image_1024_be_zoomed")]
    public bool CanImage1024BeZoomed { get; set; }

    [JsonPropertyName("write_date")]
    public string WriteDate { get; set; }

    [JsonPropertyName("is_favorite")]
    public bool IsFavorite { get; set; }

    [JsonPropertyName("is_in_selected_section_of_order")]
    public bool IsInSelectedSectionOfOrder { get; set; }

    [JsonPropertyName("create_uid")]
    public Many2One CreateUser { get; set; }

    [JsonPropertyName("create_date")]
    public string CreateDate { get; set; }

    [JsonPropertyName("write_uid")]
    public Many2One WriteUser { get; set; }

    [JsonPropertyName("is_image_fetch_pending")]
    public bool IsImageFetchPending { get; set; }

    [JsonPropertyName("tax_string")]
    public string TaxString { get; set; }

    [JsonPropertyName("stock_quant_ids")]
    public List<int> StockQuantIds { get; set; }

    [JsonPropertyName("stock_move_ids")]
    public List<int> StockMoveIds { get; set; }

    [JsonPropertyName("qty_available")]
    public double QuantityAvailable { get; set; }

    [JsonPropertyName("virtual_available")]
    public double VirtualAvailable { get; set; }

    [JsonPropertyName("free_qty")]
    public double FreeQuantity { get; set; }

    [JsonPropertyName("incoming_qty")]
    public double IncomingQuantity { get; set; }

    [JsonPropertyName("outgoing_qty")]
    public double OutgoingQuantity { get; set; }

    [JsonPropertyName("orderpoint_ids")]
    public List<int> OrderPointIds { get; set; }

    [JsonPropertyName("nbr_moves_in")]
    public int NumberOfIncomingMoves { get; set; }

    [JsonPropertyName("nbr_moves_out")]
    public int NumberOfOutgoingMoves { get; set; }

    [JsonPropertyName("nbr_reordering_rules")]
    public int NumberOfReorderingRules { get; set; }

    [JsonPropertyName("reordering_min_qty")]
    public double ReorderingMinQuantity { get; set; }

    [JsonPropertyName("reordering_max_qty")]
    public double ReorderingMaxQuantity { get; set; }

    [JsonPropertyName("putaway_rule_ids")]
    public List<int> PutawayRuleIds { get; set; }

    [JsonPropertyName("storage_category_capacity_ids")]
    public List<int> StorageCategoryCapacityIds { get; set; }

    [JsonPropertyName("show_on_hand_qty_status_button")]
    public bool ShowOnHandQtyStatusButton { get; set; }

    [JsonPropertyName("show_forecasted_qty_status_button")]
    public bool ShowForecastedQtyStatusButton { get; set; }

    [JsonPropertyName("show_qty_update_button")]
    public bool ShowQtyUpdateButton { get; set; }

    [JsonPropertyName("valid_ean")]
    public bool ValidEan { get; set; }

    [JsonPropertyName("lot_properties_definition")]
    public List<int> LotPropertiesDefinition { get; set; }

    [JsonPropertyName("variant_bom_ids")]
    public List<int> VariantBomIds { get; set; }

    [JsonPropertyName("bom_line_ids")]
    public List<int> BomLineIds { get; set; }

    [JsonPropertyName("bom_count")]
    public int BomCount { get; set; }

    [JsonPropertyName("used_in_bom_count")]
    public int UsedInBomCount { get; set; }

    [JsonPropertyName("mrp_product_qty")]
    public double MrpProductQuantity { get; set; }

    [JsonPropertyName("is_kits")]
    public bool IsKits { get; set; }

    [JsonPropertyName("product_catalog_product_is_in_bom")]
    public bool ProductCatalogProductIsInBom { get; set; }

    [JsonPropertyName("product_catalog_product_is_in_mo")]
    public bool ProductCatalogProductIsInMo { get; set; }

    [JsonPropertyName("date_from")]
    [JsonConverter(typeof(StringOrFalseJsonConverter))]
    public string DateFrom { get; set; }

    [JsonPropertyName("date_to")]
    [JsonConverter(typeof(StringOrFalseJsonConverter))]
    public string DateTo { get; set; }

    [JsonPropertyName("invoice_state")]
    [JsonConverter(typeof(StringOrFalseJsonConverter))]
    public string InvoiceState { get; set; }

    [JsonPropertyName("sale_avg_price")]
    public double SaleAveragePrice { get; set; }

    [JsonPropertyName("purchase_avg_price")]
    public double PurchaseAveragePrice { get; set; }

    [JsonPropertyName("sale_num_invoiced")]
    public double SaleNumberInvoiced { get; set; }

    [JsonPropertyName("purchase_num_invoiced")]
    public double PurchaseNumberInvoiced { get; set; }

    [JsonPropertyName("sales_gap")]
    public double SalesGap { get; set; }

    [JsonPropertyName("purchase_gap")]
    public double PurchaseGap { get; set; }

    [JsonPropertyName("turnover")]
    public double Turnover { get; set; }

    [JsonPropertyName("total_cost")]
    public double TotalCost { get; set; }

    [JsonPropertyName("sale_expected")]
    public double SaleExpected { get; set; }

    [JsonPropertyName("normal_cost")]
    public double NormalCost { get; set; }

    [JsonPropertyName("total_margin")]
    public double TotalMargin { get; set; }

    [JsonPropertyName("expected_margin")]
    public double ExpectedMargin { get; set; }

    [JsonPropertyName("total_margin_rate")]
    public double TotalMarginRate { get; set; }

    [JsonPropertyName("expected_margin_rate")]
    public double ExpectedMarginRate { get; set; }

    [JsonPropertyName("purchased_product_qty")]
    public double PurchasedProductQuantity { get; set; }

    [JsonPropertyName("is_in_purchase_order")]
    public bool IsInPurchaseOrder { get; set; }

    [JsonPropertyName("avg_cost")]
    public double AverageCost { get; set; }

    [JsonPropertyName("total_value")]
    public double TotalValue { get; set; }

    [JsonPropertyName("company_currency_id")]
    public Many2One CompanyCurrency { get; set; }

    [JsonPropertyName("has_image")]
    public bool HasImage { get; set; }

    [JsonPropertyName("purchase_order_line_ids")]
    public List<int> PurchaseOrderLineIds { get; set; }

    [JsonPropertyName("monthly_demand")]
    public double MonthlyDemand { get; set; }

    [JsonPropertyName("suggested_qty")]
    public double SuggestedQuantity { get; set; }

    [JsonPropertyName("suggest_estimated_price")]
    public double SuggestEstimatedPrice { get; set; }

    [JsonPropertyName("sales_count")]
    [JsonConverter(typeof(DoubleOrFalseJsonConverter))]
    public double? SalesCount { get; set; }

    [JsonPropertyName("product_catalog_product_is_in_sale_order")]
    public bool ProductCatalogProductIsInSaleOrder { get; set; }

    [JsonPropertyName("schedule_count")]
    public int ScheduleCount { get; set; }

    [JsonPropertyName("variant_ribbon_id")]
    public Many2One VariantRibbonId { get; set; }

    [JsonPropertyName("website_id")]
    public Many2One WebsiteId { get; set; }

    [JsonPropertyName("product_variant_image_ids")]
    public List<int> ProductVariantImageIds { get; set; }

    [JsonPropertyName("base_unit_count")]
    public double BaseUnitCount { get; set; }

    [JsonPropertyName("base_unit_id")]
    [JsonConverter(typeof(StringOrFalseJsonConverter))]
    public string BaseUnitId { get; set; }

    [JsonPropertyName("base_unit_price")]
    public double BaseUnitPrice { get; set; }

    [JsonPropertyName("base_unit_name")]
    public string BaseUnitName { get; set; }

    [JsonPropertyName("website_url")]
    public string WebsiteUrl { get; set; }

    [JsonPropertyName("stock_notification_partner_ids")]
    public List<int> StockNotificationPartnerIds { get; set; }

    [JsonPropertyName("website_published")]
    public bool WebsitePublished { get; set; }

    [JsonPropertyName("is_published")]
    public bool IsPublished { get; set; }

    [JsonPropertyName("can_publish")]
    public bool CanPublish { get; set; }

    [JsonPropertyName("website_absolute_url")]
    public string WebsiteAbsoluteUrl { get; set; }

    [JsonPropertyName("is_seo_optimized")]
    public bool IsSeoOptimized { get; set; }

    [JsonPropertyName("website_meta_title")]
    [JsonConverter(typeof(StringOrFalseJsonConverter))]
    public string WebsiteMetaTitle { get; set; }

    [JsonPropertyName("website_meta_description")]
    [JsonConverter(typeof(StringOrFalseJsonConverter))]
    public string WebsiteMetaDescription { get; set; }

    [JsonPropertyName("website_meta_keywords")]
    [JsonConverter(typeof(StringOrFalseJsonConverter))]
    public string WebsiteMetaKeywords { get; set; }

    [JsonPropertyName("website_meta_og_img")]
    [JsonConverter(typeof(StringOrFalseJsonConverter))]
    public string WebsiteMetaOgImg { get; set; }

    [JsonPropertyName("seo_name")]
    [JsonConverter(typeof(StringOrFalseJsonConverter))]
    public string SeoName { get; set; }

    [JsonPropertyName("name")]
    [JsonConverter(typeof(StringOrFalseJsonConverter))]
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
    public Many2One Category { get; set; }

    [JsonPropertyName("currency_id")]
    public Many2One Currency { get; set; }

    [JsonPropertyName("cost_currency_id")]
    public Many2One CostCurrency { get; set; }

    [JsonPropertyName("list_price")]
    public double ListPrice { get; set; }

    [JsonPropertyName("volume_uom_name")]
    public string VolumeUomName { get; set; }

    [JsonPropertyName("weight_uom_name")]
    public string WeightUomName { get; set; }

    [JsonPropertyName("sale_ok")]
    public bool SaleOk { get; set; }

    [JsonPropertyName("purchase_ok")]
    public bool PurchaseOk { get; set; }

    [JsonPropertyName("uom_id")]
    public Many2One UnitOfMeasure { get; set; }

    [JsonPropertyName("uom_ids")]
    public List<int> UomIds { get; set; }

    [JsonPropertyName("uom_name")]
    public string UomName { get; set; }

    [JsonPropertyName("company_id")]
    public Many2One CompanyId { get; set; }

    [JsonPropertyName("seller_ids")]
    public List<int> SellerIds { get; set; }

    [JsonPropertyName("variant_seller_ids")]
    public List<int> VariantSellerIds { get; set; }

    [JsonPropertyName("color")]
    public int Color { get; set; }

    [JsonPropertyName("attribute_line_ids")]
    public List<int> AttributeLineIds { get; set; }

    [JsonPropertyName("valid_product_template_attribute_line_ids")]
    public List<int> ValidProductTemplateAttributeLineIds { get; set; }

    [JsonPropertyName("product_variant_ids")]
    public List<int> ProductVariantIds { get; set; }

    [JsonPropertyName("product_variant_id")]
    public Many2One ProductVariant { get; set; }

    [JsonPropertyName("product_variant_count")]
    public int ProductVariantCount { get; set; }

    [JsonPropertyName("has_configurable_attributes")]
    public bool HasConfigurableAttributes { get; set; }

    [JsonPropertyName("is_dynamically_created")]
    public bool IsDynamicallyCreated { get; set; }

    [JsonPropertyName("product_tooltip")]
    public string ProductTooltip { get; set; }

    [JsonPropertyName("product_tag_ids")]
    public List<int> ProductTagIds { get; set; }

    [JsonPropertyName("product_properties")]
    public List<int> ProductProperties { get; set; }

    [JsonPropertyName("taxes_id")]
    public List<int> TaxesId { get; set; }

    [JsonPropertyName("supplier_taxes_id")]
    public List<int> SupplierTaxesId { get; set; }

    [JsonPropertyName("property_account_income_id")]
    public Many2One PropertyAccountIncomeId { get; set; }

    [JsonPropertyName("property_account_expense_id")]
    public Many2One PropertyAccountExpenseId { get; set; }

    [JsonPropertyName("account_tag_ids")]
    public List<int> AccountTagIds { get; set; }

    [JsonPropertyName("fiscal_country_codes")]
    public string FiscalCountryCodes { get; set; }

    [JsonPropertyName("is_storable")]
    public bool IsStorable { get; set; }

    [JsonPropertyName("responsible_id")]
    public Many2One ResponsibleUser { get; set; }

    [JsonPropertyName("property_stock_production")]
    public Many2One PropertyStockProduction { get; set; }

    [JsonPropertyName("property_stock_inventory")]
    public Many2One PropertyStockInventory { get; set; }

    [JsonPropertyName("sale_delay")]
    public int SaleDelay { get; set; }

    [JsonPropertyName("tracking")]
    public string Tracking { get; set; }

    [JsonPropertyName("lot_sequence_id")]
    public Many2One LotSequence { get; set; }

    [JsonPropertyName("serial_prefix_format")]
    public string SerialPrefixFormat { get; set; }

    [JsonPropertyName("next_serial")]
    public string NextSerial { get; set; }

    [JsonPropertyName("description_picking")]
    [JsonConverter(typeof(StringOrFalseJsonConverter))]
    public string DescriptionPicking { get; set; }

    [JsonPropertyName("description_pickingout")]
    [JsonConverter(typeof(StringOrFalseJsonConverter))]
    public string DescriptionPickingOut { get; set; }

    [JsonPropertyName("description_pickingin")]
    [JsonConverter(typeof(StringOrFalseJsonConverter))]
    public string DescriptionPickingIn { get; set; }

    [JsonPropertyName("location_id")]
    [JsonConverter(typeof(StringOrFalseJsonConverter))]
    public string LocationId { get; set; }

    [JsonPropertyName("warehouse_id")]
    [JsonConverter(typeof(StringOrFalseJsonConverter))]
    public string WarehouseId { get; set; }

    [JsonPropertyName("has_available_route_ids")]
    public bool HasAvailableRouteIds { get; set; }

    [JsonPropertyName("route_ids")]
    public List<int> RouteIds { get; set; }

    [JsonPropertyName("route_from_categ_ids")]
    public List<int> RouteFromCategIds { get; set; }

    [JsonPropertyName("bom_ids")]
    public List<int> BomIds { get; set; }

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
    [JsonConverter(typeof(StringOrFalseJsonConverter))]
    public string PropertyPriceDifferenceAccountId { get; set; }

    [JsonPropertyName("service_type")]
    public string ServiceType { get; set; }

    [JsonPropertyName("sale_line_warn_msg")]
    [JsonConverter(typeof(StringOrFalseJsonConverter))]
    public string SaleLineWarnMsg { get; set; }

    [JsonPropertyName("expense_policy")]
    public string ExpensePolicy { get; set; }

    [JsonPropertyName("visible_expense_policy")]
    public bool VisibleExpensePolicy { get; set; }

    [JsonPropertyName("invoice_policy")]
    public string InvoicePolicy { get; set; }

    [JsonPropertyName("optional_product_ids")]
    public List<int> OptionalProductIds { get; set; }

    [JsonPropertyName("product_add_mode")]
    public string ProductAddMode { get; set; }

    [JsonPropertyName("service_to_purchase")]
    public bool ServiceToPurchase { get; set; }

    [JsonPropertyName("landed_cost_ok")]
    public bool LandedCostOk { get; set; }

    [JsonPropertyName("split_method_landed_cost")]
    [JsonConverter(typeof(StringOrFalseJsonConverter))]
    public string SplitMethodLandedCost { get; set; }

    [JsonPropertyName("hs_code")]
    [JsonConverter(typeof(StringOrFalseJsonConverter))]
    public string HsCode { get; set; }

    [JsonPropertyName("country_of_origin")]
    [JsonConverter(typeof(StringOrFalseJsonConverter))]
    public string CountryOfOrigin { get; set; }

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
    public double RatingAverage { get; set; }

    [JsonPropertyName("rating_avg_text")]
    public string RatingAverageText { get; set; }

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

    [JsonPropertyName("compare_list_price")]
    public double CompareListPrice { get; set; }

    [JsonPropertyName("variants_default_code")]
    public string VariantsDefaultCode { get; set; }

    [JsonPropertyName("project_id")]
    public JsonElement ProjectId { get; set; }

    [JsonPropertyName("project_template_id")]
    [JsonConverter(typeof(StringOrFalseJsonConverter))]
    public string ProjectTemplateId { get; set; }

    [JsonPropertyName("task_template_id")]
    [JsonConverter(typeof(StringOrFalseJsonConverter))]
    public string TaskTemplateId { get; set; }

    [JsonPropertyName("service_policy")]
    [JsonConverter(typeof(StringOrFalseJsonConverter))]
    public string ServicePolicy { get; set; }

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
    [JsonConverter(typeof(StringOrFalseJsonConverter))]
    public string ServiceUpsellThresholdRatio { get; set; }
}


