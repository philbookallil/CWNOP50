using Nop.Core.Domain.Customers;
using Nop.Services.Security;

namespace Nop.Plugin.CW.Core.Services.Security;

public class CWPermissionConfigManager : IPermissionConfigManager
{
    public const string ORDERS_ACCESS_PAYMENTS = $"{nameof(StandardPermission.Orders)}.AccessPayments";

    public const string ADMIN_ACCESS_BRANCH = "CW.CORE.Admin.AccessBranch";
    public const string ADMIN_ACCESS_MULTISTORE = "CW.CORE.Admin.AccessMultiStore";
    public const string ADMIN_ACCESS_MULTISTORE_SETTINGS = "CW.CORE.Admin.AccessMultiStoreSettings";
    public const string ADMIN_ACCESS_DEFAULT_STORE_SETTINGS = "CW.CORE.Admin.AccessDefaultStoreSettings";
    public const string ADMIN_ACCESS_CREDITNOTES = "CW.CORE.Admin.AccessCreditNotes";
    public const string ADMIN_ACCESS_EXCHANGES = "CW.CORE.Admin.AccessExchanges";
    public const string PUBLIC_ACCESS_BULKORDERING = "CW.CORE.PublicStore.AccessBulkOrdering";

    /// <summary>
    /// Gets all permission configurations
    /// </summary>
    public IList<PermissionConfig> AllConfigs =>
        new List<PermissionConfig>
        {
            new("CW Access Order Payments", ORDERS_ACCESS_PAYMENTS, nameof(StandardPermission.Orders), NopCustomerDefaults.AdministratorsRoleName),

            new("Admin area. CW Plugin. Branches. Create, edit, delete", ADMIN_ACCESS_BRANCH , nameof(StandardPermission.Customers), NopCustomerDefaults.AdministratorsRoleName),
            new("Admin area. CW Plugin. MultiStores.", ADMIN_ACCESS_MULTISTORE , nameof(StandardPermission.Customers), NopCustomerDefaults.AdministratorsRoleName),
            new("Admin area. CW Plugin. MultiStore Settings.", ADMIN_ACCESS_MULTISTORE_SETTINGS , nameof(StandardPermission.Customers), NopCustomerDefaults.AdministratorsRoleName),
            new("Admin area. CW Plugin. Default Store Settings.", ADMIN_ACCESS_DEFAULT_STORE_SETTINGS , nameof(StandardPermission.Customers), NopCustomerDefaults.AdministratorsRoleName),
            new("Admin area. CW Plugin. Exchanges and Returns.", ADMIN_ACCESS_EXCHANGES , nameof(StandardPermission.Orders), NopCustomerDefaults.AdministratorsRoleName),
            new("Admin area. CW Plugin. Credit Notes.", ADMIN_ACCESS_CREDITNOTES , nameof(StandardPermission.Customers), NopCustomerDefaults.AdministratorsRoleName),
            new("Public store. CW Plugin. Access bulk ordering.", PUBLIC_ACCESS_BULKORDERING, nameof(StandardPermission.PublicStore), NopCustomerDefaults.RegisteredRoleName, NopCustomerDefaults.AdministratorsRoleName),
        };
}
