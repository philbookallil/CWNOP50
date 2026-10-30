using Nop.Core.Domain.Customers;
using Nop.Services.Security;

namespace Nop.Plugin.Payments.NABGateway.Services.Security;

/// <summary>
/// Registers the permission gating the NAB XML payment reports.
/// Discovered automatically by the framework's permission system.
/// </summary>
public class NABXMLPaymentPermissionConfigManager : IPermissionConfigManager
{
    /// <summary>
    /// Permission required to view the NAB XML payment / failed-transaction reports.
    /// </summary>
    public const string ACCESS_REPORTS = "Payments.NABGateway.Admin.AccessReports";

    public IList<PermissionConfig> AllConfigs =>
        new List<PermissionConfig>
        {
            new("Admin area. NAB Payment Gateway. Access payment reports",
                ACCESS_REPORTS,
                nameof(StandardPermission.Orders),
                NopCustomerDefaults.AdministratorsRoleName)
        };
}
