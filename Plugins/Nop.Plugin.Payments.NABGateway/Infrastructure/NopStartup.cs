using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nop.Core.Infrastructure;
using Nop.Plugin.Payments.NABGateway.Areas.Admin.Factories;
using Nop.Plugin.Payments.NABGateway.Services;

namespace Nop.Plugin.Payments.NABGateway.Infrastructure;

/// <summary>
/// Plugin DI registration: gateway transport, named HttpClient and view location expander.
/// </summary>
public class NopStartup : INopStartup
{
    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        //register the gateway transport
        services.AddScoped<NABXMLGatewayClient>();

        //card kept in memory between the payment-info and confirm steps (singleton: it outlives the request)
        services.AddSingleton<INABXMLCardDataStore, NABXMLCardDataStore>();

        //transaction persistence + reporting
        services.AddScoped<INABXMLTransactionService, NABXMLTransactionService>();
        services.AddScoped<INABXMLTransactionModelFactory, NABXMLTransactionModelFactory>();

        //named HttpClient for the NAB Transact XML API
        services.AddHttpClient(NABXMLPaymentDefaults.HttpClientName, client =>
        {
            client.Timeout = TimeSpan.FromSeconds(NABXMLPaymentDefaults.GatewayTimeoutSeconds);
        })
        .ConfigurePrimaryHttpMessageHandler(serviceProvider =>
        {
            var handler = new HttpClientHandler();

            //honour the "Ignore SSL errors" setting (default store scope) — intended for test/sandbox only.
            //the handler is pooled, so toggling the setting takes effect once the pooled handler recycles (~2 min).
            var settings = serviceProvider.GetRequiredService<NABXMLPaymentSettings>();
            if (settings.IgnoreSslErrors)
                handler.ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;

            return handler;
        });

        //plugin view locations
        services.Configure<RazorViewEngineOptions>(options =>
        {
            options.ViewLocationExpanders.Add(new ViewLocationExpander());
        });
    }

    public void Configure(IApplicationBuilder application)
    {
    }

    /// <summary>
    /// Run after the framework registers its defaults so the named HttpClient and view expander wire in correctly.
    /// </summary>
    public int Order => 3000;
}
