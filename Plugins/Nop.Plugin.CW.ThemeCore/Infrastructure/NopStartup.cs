using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nop.Core.Infrastructure;
using Nop.Web.Framework.Infrastructure.Extensions;
using Nop.Plugin.CW.ThemeCore.Filters;
using Nop.Plugin.CW.ThemeCore.Services;
using Nop.Plugin.CW.ThemeCore.Services.BrandDesigner;
using Nop.Plugin.CW.ThemeCore.Services.BrandDesigner.Ai;
using Nop.Plugin.CW.ThemeCore.Services.Layout;
using Nop.Plugin.CW.ThemeCore.Services.Preview;

namespace Nop.Plugin.CW.ThemeCore.Infrastructure;

public class NopStartup : INopStartup
{
    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<ICwThemeCssService, CwThemeCssService>();
        services.AddScoped<ICwCssSanitizer, CwCssSanitizer>();
        services.AddScoped<ICwThemeCssPathResolver, CwThemeCssPathResolver>();
        services.AddScoped<ICwThemeCssFileService, CwThemeCssFileService>();

        //layout system (per-store, per-section layout selection)
        services.AddScoped<IThemeLayoutManifestService, ThemeLayoutManifestService>();
        services.AddScoped<IThemeLayoutService, ThemeLayoutService>();
        services.AddScoped<ThemeLayoutPageClassFilter>();
        services.Configure<MvcOptions>(options => options.Filters.AddService<ThemeLayoutPageClassFilter>());

        //Guided Brand Designer
        services.AddScoped<ICwBrandDesignerService, CwBrandDesignerService>();
        services.AddScoped<ICwBrandAiSettingsService, CwBrandAiSettingsService>();
        services.AddScoped<ICwBrandAiLimiter, CwBrandAiLimiter>();
        services.AddScoped<ICwBrandAiClientResolver, CwBrandAiClientResolver>();
        AddBrandAiClient<CwClaudeBrandAiClient>(services);
        AddBrandAiClient<CwOpenAiCompatibleBrandAiClient>(services);
        services.AddScoped<IThemeBrandPreviewService, ThemeBrandPreviewService>();

        //live preview: signed cross-origin token that authorises "preview mode" on a store's own domain
        services.AddScoped<ICwPreviewTokenService, CwPreviewTokenService>();
        //live preview Phase 3: per-store hidden "preview customer" (dummy cart) for live Cart/Checkout
        services.AddScoped<ICwPreviewCustomerService, CwPreviewCustomerService>();
        //live preview Phase 3: render one-page checkout in the preview whatever the store's checkout settings
        //(checkout disabled, one-page checkout off, anonymous checkout off)
        services.AddScoped<CwPreviewCheckoutFilter>();
        services.Configure<MvcOptions>(options => options.Filters.AddService<CwPreviewCheckoutFilter>());

        services.Configure<RazorViewEngineOptions>(options =>
        {
            options.ViewLocationExpanders.Add(new ViewLocationExpander());
        });
    }

    /// <summary>
    /// A typed client for an AI provider. Redirects are not followed: the request carries the API key, and an address
    /// an admin typed must not be able to send it on to another host.
    /// </summary>
    private static void AddBrandAiClient<TClient>(IServiceCollection services) where TClient : class
    {
        var builder = services.AddHttpClient<TClient>();
        builder.WithProxy();
        builder.ConfigureHttpMessageHandlerBuilder(handlerBuilder =>
        {
            if (handlerBuilder.PrimaryHandler is HttpClientHandler handler)
                handler.AllowAutoRedirect = false;
        });
    }

    public void Configure(IApplicationBuilder application)
    {
        //The live-preview framing middleware is NOT registered here: this startup's Order (3000) runs after
        //nop's terminal endpoint middleware (NopEndpoints, Order 900 → UseEndpoints), where any middleware
        //would never be reached. It is registered by PreviewFramingStartup, ordered before the endpoints.
    }

    //load late for service registration: theme infrastructure other CW plugins/themes depend on
    public int Order => 3000;
}
