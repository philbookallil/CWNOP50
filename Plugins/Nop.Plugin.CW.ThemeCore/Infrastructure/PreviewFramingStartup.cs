using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nop.Core.Infrastructure;

namespace Nop.Plugin.CW.ThemeCore.Infrastructure;

/// <summary>
/// Registers the live-preview framing middleware (<see cref="CwPreviewFramingMiddleware"/>) in the request
/// pipeline. Its <see cref="Order"/> MUST sit between nop's routing (NopRoutingStartup, Order 400 →
/// <c>UseRouting</c>) and endpoints (NopEndpoints, Order 900 → <c>UseEndpoints</c>): nop builds one pipeline
/// by running every <see cref="INopStartup.Configure"/> in ascending Order, and a middleware added after the
/// terminal endpoint middleware is never reached. Kept separate from <see cref="NopStartup"/> (Order 3000,
/// used for this plugin's service registration) so the pipeline position is independent of DI ordering.
/// </summary>
public class PreviewFramingStartup : INopStartup
{
    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
    }

    public void Configure(IApplicationBuilder application)
    {
        //relax frame-ancestors to the authorised admin origin, only for a valid preview request, and flag the
        //request so the theme head component injects the preview bridge (see CwThemeHeadViewComponent)
        application.UseMiddleware<CwPreviewFramingMiddleware>();
    }

    //between UseRouting (400) and UseEndpoints (900): the middleware must run before MVC executes the view
    public int Order => 550;
}
