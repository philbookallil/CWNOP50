using Nop.Core.Domain.Customers;
using Nop.Services.Security;

namespace Nop.Plugin.Integration.Odoo.Infrastructure;

/// <summary>
/// The plugin's own permissions. nop adds a permission it does not have yet when the application starts.
/// </summary>
public class OdooPermissionConfigManager : IPermissionConfigManager
{
    /// <summary>
    /// Open the Orders page, send orders to Odoo and cancel them there. The plugin's settings and sync log stay
    /// under Manage plugins.
    /// </summary>
    public const string SEND_ORDERS = "Integration.Odoo.SendOrders";

    /// <summary>
    /// Gets all permission configurations
    /// </summary>
    public IList<PermissionConfig> AllConfigs => new List<PermissionConfig>
    {
        new("Admin area. Odoo. Send orders to Odoo", SEND_ORDERS, nameof(StandardPermission.Orders), NopCustomerDefaults.AdministratorsRoleName)
    };
}
