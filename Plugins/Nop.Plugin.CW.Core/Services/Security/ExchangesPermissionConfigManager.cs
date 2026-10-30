using Nop.Core.Domain.Customers;
using Nop.Services.Security;

namespace Nop.Plugin.CW.Core.Services.Security
{
    public class ExchangesPermissionConfigManager : IPermissionConfigManager
    {
        public const string ACCESS_EXCHANGES = "CW.Plugin.AccessExchanges";

        //public static readonly PermissionRecord AccessExchanges = new PermissionRecord { Name = "CW Plugin. Access Exchanges", SystemName = "AccessExchanges", Category = "Plugin" };

        /// <summary>
        /// Gets all permission configurations
        /// </summary>
        public IList<PermissionConfig> AllConfigs =>
            new List<PermissionConfig>
            {
                new("CW Plugin. Access Exchanges", ACCESS_EXCHANGES, nameof(StandardPermission.Orders), NopCustomerDefaults.AdministratorsRoleName),
            };
    }
}

