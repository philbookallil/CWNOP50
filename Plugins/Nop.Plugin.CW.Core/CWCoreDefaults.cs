namespace Nop.Plugin.CW.Core;

public class CWCoreDefaults
{
    /// <summary>
    /// Gets a plugin system name
    /// </summary>
    public static string SystemName => "CW.Core";

    /// <summary>
    /// Gets the branches administration menu system name
    /// </summary>
    public static string BranchAdminMenuSystemName => "CW.Core.BranchesAdminMenu";

    /// <summary>
    /// Gets the multiStores administration menu system name
    /// </summary>
    public static string MultiStoreAdminMenuSystemName => "CW.Core.MultiStoresAdminMenu";

    /// <summary>
    /// Gets the exchanges administration menu system name
    /// </summary>
    public static string ExchangesAdminMenuSystemName => "CW.Core.ExchangesAdminMenu";

    /// <summary>
    /// Gets the credit notes report administration menu system name
    /// </summary>
    public static string CreditNotesReportAdminMenuSystemName => "CW.CreditNotes.Report";

    /// <summary>
    /// Gets the global fee settings administration menu system name
    /// </summary>
    public static string FeeSettingsAdminMenuSystemName => "CW.Exchanges.FeeSettings";

    /// <summary>
    /// Gets the default store settings administration menu system name
    /// </summary>
    public static string DefaultStoreSettingsAdminMenuSystemName => "CW.Core.DefaultStoreSettingsAdminMenu";

    /// <summary>
    /// Gets the name of the stored procedure to get the packNameList ids by manager
    /// </summary>
    public static string GetPackNameListIdsByManagerSPName => "packname_get_listids_by_manager";

    /// <summary>
    /// Products per page in the replacement picker. Twelve fills a three-column grid evenly.
    /// </summary>
    public const int ReplacementPickerPageSize = 12;

    /// <summary>
    /// Thumbnail size for product pictures in the return wizard table
    /// </summary>
    public const int ReturnItemThumbSize = 60;

    /// <summary>
    /// The most return requests one confirmation (the panel after the wizard, its receipts) shows
    /// </summary>
    public const int ReturnRequestsConfirmationMaxCount = 50;

    /// <summary>
    /// The most lines with a quantity one bulk ordering submit adds (a whole page can hold more boxes; more than this
    /// many filled at once are added card by card)
    /// </summary>
    public const int BulkOrderMaxLinesPerAdd = 1000;

    /// <summary>
    /// Picture size for the replacement picker's product preview
    /// </summary>
    public const int ReplacementPreviewSize = 240;

    /// <summary>
    /// Key under which a shipment's delivery destination label is carried on
    /// <c>ShipmentBriefModel.CustomProperties</c>, for the customer's order page.
    /// </summary>
    /// <remarks>
    /// nop's ShipmentBriefModel has four properties and no room for ours, and it is a nop record we must
    /// not modify. CustomProperties is the extension point nop provides for exactly this. A const rather
    /// than a literal so the writer and the reader are greppable from each other.
    /// </remarks>
    public const string SHIPMENT_DESTINATION_PROPERTY = "CW.ShipmentDestination";

    /// <summary>
    /// Per-product override for the bulk grid on the product details page, stored as a
    /// <c>GenericAttribute</c> on <c>Product</c>. Tri-state: "1" forces it on, "0" forces it off, absent or
    /// empty follows the store's <c>BulkOrderEnableOnProductPage</c>.
    /// </summary>
    /// <remarks>
    /// One key phrased positively, rather than 4.6's <c>LockBulkOrder</c> + <c>LockBulkOrderOverride</c> pair
    /// — a nullable bool encoded as two columns and named as a negative, so its own expression read
    /// <c>LockBulkOrderOverride ? LockBulkOrder : !EnableStandardProductBulkOrder</c>.
    /// GenericAttribute rather than a CW table because the data is sparse, needs no migration, and carries a
    /// StoreId — which matches the store setting it overrides being per-store too.
    /// </remarks>
    public const string BULKORDER_SHOW_ON_PRODUCT_PAGE_PROPERTY = "CW.BulkOrder.ShowOnProductPage";

    /// <summary>
    /// Gets the name of the stored procedure to get the subsidy history
    /// </summary>
    public static string GetSubsidyHistorySPName => "subsidy_get_history";

    public static string FreightMethodSystemName => "Freight";

    public static string FreightPluginSystemName => "CW.Freight";

    /// <summary>Prefix shared by every CW cache key, for wholesale CW cache invalidation</summary>
    public static string CachePrefix => "CW.";

    #region route-names

    /// <summary>
    /// Gets the customer list route name
    /// </summary>
    public const string CUSTOMER_LIST = "CW.Plugin.Admin.Customer.List";

    /// <summary>
    /// Gets the customer create route name
    /// </summary>
    public const string CUSTOMER_CREATE = "CW.Plugin.Admin.Customer.Create";

    /// <summary>
    /// Gets the customer edit route name
    /// </summary>
    public const string CUSTOMER_EDIT = "CW.Plugin.Admin.Customer.Edit";

    /// <summary>
    /// Gets the multiStore edit route name
    /// </summary>
    public const string MULTISTORE_EDIT = "CW.Plugin.Admin.MultiStore.Edit";

    public const string BULKORDER_INDEX = "Plugin.CW.Core.BulkOrdering";

    public const string BULKORDER_ITEMS = "Plugin.CW.Core.BulkOrdering.Items";

    public const string BULKORDER_GRID = "Plugin.CW.Core.BulkOrdering.Grid";

    public const string BULKORDER_ADDTOCART = "Plugin.CW.Core.BulkOrdering.AddToCart";

    #endregion

    #region Customer Navigation Tabs

    /// <summary>
    /// Gets the Subsidies route name
    /// </summary>
    public static string CustomerCreditsForMeRouteName => "Plugin.CW.Core.CreditsForMe";

    /// <summary>
    /// Gets the class name of the Subsidies menu item
    /// </summary>
    public static string CustomerCreditsForMeMenuClassName => "credits-forme";

    /// <summary>
    /// Gets the tab id of the Subsidies menu item
    /// </summary>
    public static int CustomerCreditsForMeMenuTab => 21;

    /// <summary>
    /// Gets the Subsidies route name
    /// </summary>
    public static string CustomerSubsidiesRouteName => "Plugin.CW.Core.CustomerSubsidies";

    /// <summary>
    /// Gets the class name of the Subsidies menu item
    /// </summary>
    public static string CustomerSubsidiesMenuClassName => "customer-subsidies";

    /// <summary>
    /// Gets the tab id of the Subsidies menu item
    /// </summary>
    public static int CustomerSubsidiesMenuTab => 22;

    /// <summary>
    /// Gets the route name of the subsidy catalogue
    /// </summary>
    public static string SubsidyCatalogRouteName => "Plugin.CW.Core.SubsidyCatalog";

    /// <summary>
    /// Gets the route name of the packname order-completed page
    /// </summary>
    public static string PackNameCheckoutCompletedRouteName => "PackNameCheckoutCompleted";

    #endregion
}
