using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nop.Core.Infrastructure;
using Nop.Plugin.Integration.Odoo.Services;
using System.Threading.Channels;

namespace Nop.Plugin.Integration.Odoo.Infrastructure;

/// <summary>
/// Registers the Odoo Integration plugin's services with the ASP.NET Core DI container.
/// NopCommerce discovers this class automatically via the INopStartup interface.
/// </summary>
public class NopStartup : INopStartup
{
    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {

        // Register the API service as scoped (per-request lifetime)
        services.AddScoped<IOdooApiService, OdooApiService>();

        // Register ProductSyncService with required NopCommerce services
        services.AddScoped<IProductSyncService, ProductSyncService>();

        // Register OdooLogService for sync tracing
        services.AddScoped<IOdooLogService, OdooLogService>();

        // Register branch sync infrastructure (no dependency on Nop.Plugin.Integration.Odoo)
        services.AddScoped<IBranchSyncService, BranchSyncService>();

        // Register store sync infrastructure and the Odoo↔nop store id mapping
        services.AddScoped<IOdooStoreMapService, OdooStoreMapService>();
        services.AddScoped<IStoreSyncService, StoreSyncService>();

        // Register department sync (reuses CW.Core department/role provisioning)
        services.AddScoped<IDepartmentSyncService, DepartmentSyncService>();

        // --- Background sync infrastructure (shared by product & branch sync) ---
        // Bounded channel prevents unbounded memory growth if requests pile up.
        var channel = Channel.CreateBounded<OdooSyncRequest>(new BoundedChannelOptions(10)
        {
            FullMode = BoundedChannelFullMode.Wait
        });
        services.AddSingleton(channel);
        services.AddSingleton<IOdooSyncQueue, OdooSyncQueue>();
        services.AddHostedService<OdooSyncBackgroundService>();

        // Register the named HttpClient used by OdooApiService.
        // A 30-second timeout is appropriate for API calls over SaaS internet connectivity.
        services.AddHttpClient(OdooConstants.HttpClientName, client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept
                  .Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
        });

    }

    public void Configure(IApplicationBuilder application)
    {
        // No middleware required for this plugin
    }

    /// <summary>
    /// Load order – run after the core NopCommerce services are registered.
    /// </summary>
    public int Order => 3000;
}
