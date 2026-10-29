using Nop.Core.Domain.Customers;
using Nop.Services.Security;

namespace Nop.Plugin.ExternalAuth.SSO.Security;

/// <summary>
/// Declares the plugin's permissions in the nopCommerce 5.00 permission model.
/// </summary>
/// <remarks>
/// Auto-discovered by <c>PermissionService.InsertPermissionsAsync</c> via
/// <c>ITypeFinder.FindClassesOfType&lt;IPermissionConfigManager&gt;</c>; no DI registration required.
/// Default-granted only to the <c>Administrators</c> role. See References/SSO_Plugin_Reference.md §7.3.
/// Plugin install/uninstall calls <c>InsertPermissionsAsync</c> / removes these system names
/// from the <c>PermissionRecord</c> table.
/// </remarks>
public partial class ExternalAuthSSOPermissionConfigManager : IPermissionConfigManager
{
    /// <summary>
    /// Category column on the admin "Access control list" screen, shown as "Single Sign On".
    /// </summary>
    /// <remarks>
    /// No dots: nop's ACL page uses the category as part of an element id and selects it with jQuery, so a dot
    /// is read as a class selector and the category's permission grid never loads. It was
    /// <see cref="RetiredCategory"/> until 2026-10-22; the plugin update moves existing records.
    /// </remarks>
    public const string Category = "SingleSignOn";

    /// <summary>
    /// The category used until 2026-10-22, kept so the plugin update can move records out of it.
    /// </summary>
    public const string RetiredCategory = "ExternalAuth.SSO";

    /// <inheritdoc />
    public IList<PermissionConfig> AllConfigs => new List<PermissionConfig>
    {
        new (
            name: "Plugins. ExternalAuth.SSO. Configure",
            systemName: ExternalAuthSSOPermissions.ACCESS_SSO_SETTINGS,
            category: Category,
            NopCustomerDefaults.AdministratorsRoleName),
        new (
            name: "Plugins. ExternalAuth.SSO. Manage login bypass and audit log",
            systemName: ExternalAuthSSOPermissions.MANAGE_SSO_SECURITY,
            category: Category,
            NopCustomerDefaults.AdministratorsRoleName)
    };
}
