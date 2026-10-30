using Nop.Core.Domain.Customers;
using Nop.Services.Security;

namespace Nop.Plugin.CW.Core.Services.Security
{
    /// <summary>
    /// Permission Provider for the Credit Notes Plugin
    /// </summary>
    public class CreditNotesPermissionConfigManager : IPermissionConfigManager
    {
        public const string ALLOW_MANUAL_CREDITNOTES = "CW.Plugin.AllowManualCreditNotes";

        /// <summary>
        /// Gets all permission configurations
        /// </summary>
        public IList<PermissionConfig> AllConfigs =>
            new List<PermissionConfig>
            {
                new("CW Plugin. Allow Manual Credit Notes creation", ALLOW_MANUAL_CREDITNOTES, nameof(StandardPermission.Orders), NopCustomerDefaults.AdministratorsRoleName),
            };
    }
}