using Nop.Core.Domain.Customers;
using Nop.Services.Security;

namespace Nop.Plugin.CW.ThemeCore.Services.Security;

/// <summary>
/// Declares the CW Theme Core admin permission. Discovered automatically by nop's
/// permission service (no explicit DI registration required).
/// </summary>
public class CwThemeCorePermissionConfigManager : IPermissionConfigManager
{
    /// <summary>
    /// Permission to manage CW theme settings (accent colours, logo).
    /// </summary>
    public const string MANAGE = "CW.ThemeCore.Manage";

    public IList<PermissionConfig> AllConfigs =>
        new List<PermissionConfig>
        {
            new("Admin area. CW Plugin. Theme settings.", MANAGE, nameof(StandardPermission.Configuration), NopCustomerDefaults.AdministratorsRoleName)
        };
}
