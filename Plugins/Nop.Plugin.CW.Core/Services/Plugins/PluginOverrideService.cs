using Microsoft.AspNetCore.Http;
using Nop.Core.Domain.Catalog;
using Nop.Core.Domain.Customers;
using Nop.Core.Domain.Media;
using Nop.Core.Infrastructure;
using Nop.Data.Migrations;
using Nop.Plugin.CW.Core.Services.Customers;
using Nop.Services.Configuration;
using Nop.Services.Customers;
using Nop.Services.Helpers;
using Nop.Services.Logging;
using Nop.Services.Plugins;

namespace Nop.Plugin.CW.Core.Services.Plugins;

/// <summary>
/// Plugin service with plugins limited to customer roles following the customer signed in.
/// </summary>
public class PluginOverrideService : PluginService
{
    #region Fields

    protected readonly ICustomerOverrideService _customerOverrideService;

    #endregion

    #region Ctor

    public PluginOverrideService(CatalogSettings catalogSettings,
        ICustomerService customerService,
        IHttpContextAccessor httpContextAccessor,
        Lazy<IMigrationManager> migrationManager,
        ILogger logger,
        INopFileProvider fileProvider,
        ISettingService settingService,
        IWebHelper webHelper,
        MediaSettings mediaSettings,
        ICustomerOverrideService customerOverrideService) : base(catalogSettings,
                                                            customerService,
                                                            httpContextAccessor,
                                                            migrationManager,
                                                            logger,
                                                            fileProvider,
                                                            settingService,
                                                            webHelper,
                                                            mediaSettings)
    {
        _customerOverrideService = customerOverrideService;
    }

    #endregion

    #region Utilities

    /// <summary>
    /// Check whether to load the plugin based on the customer passed
    /// </summary>
    /// <remarks>
    /// A payment, shipping, tax or widget plugin limited to a role is about what the customer may use and pay
    /// with, so it follows the signed-in customer's roles even while the catalogue follows a packname's. nop
    /// reads GetCustomerRoleIdsAsync, which in the packname context answers with the packname's roles.
    /// </remarks>
    /// <param name="pluginDescriptor">Plugin descriptor to check</param>
    /// <param name="customer">Customer</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the result of check
    /// </returns>
    protected override async Task<bool> FilterByCustomerAsync(PluginDescriptor pluginDescriptor, Customer customer)
    {
        ArgumentNullException.ThrowIfNull(pluginDescriptor);

        if (customer == null || !pluginDescriptor.LimitedToCustomerRoles.Any())
            return true;

        if (_catalogSettings.IgnoreAcl)
            return true;

        return pluginDescriptor.LimitedToCustomerRoles.Intersect(await _customerOverrideService.GetSignedInCustomerRoleIdsAsync(customer)).Any();
    }

    #endregion
}
