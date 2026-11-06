using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Nop.Core;
using Nop.Plugin.CW.ThemeCore.Infrastructure;
using Nop.Plugin.CW.ThemeCore.Services;
using Nop.Plugin.CW.ThemeCore.Services.Layout;
using Nop.Services.Themes;
using Nop.Web.Framework.Components;

namespace Nop.Plugin.CW.ThemeCore.Components;

/// <summary>
/// Renders, into the page head: (1) the inline per-store accent-colour <c>:root{}</c> block, then
/// (2) a link to the store's generated Custom CSS file (Capability 2) — so custom CSS can use/override the tokens.
/// Registered against the core <c>PublicWidgetZones.HeadHtmlTag</c> zone, so it works on any active theme.
/// </summary>
public class CwThemeHeadViewComponent : NopViewComponent
{
    #region Fields

    protected readonly ICwThemeCssService _themeCssService;
    protected readonly ICwThemeCssFileService _themeCssFileService;
    protected readonly IStoreContext _storeContext;
    protected readonly IThemeContext _themeContext;
    protected readonly IThemeLayoutManifestService _manifestService;

    private static readonly JsonSerializerOptions _regionMapJson = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    #endregion

    #region Ctor

    public CwThemeHeadViewComponent(
        ICwThemeCssService themeCssService,
        ICwThemeCssFileService themeCssFileService,
        IStoreContext storeContext,
        IThemeContext themeContext,
        IThemeLayoutManifestService manifestService)
    {
        _themeCssService = themeCssService;
        _themeCssFileService = themeCssFileService;
        _storeContext = storeContext;
        _themeContext = themeContext;
        _manifestService = manifestService;
    }

    #endregion

    #region Methods

    public async Task<IViewComponentResult> InvokeAsync(string widgetZone, object additionalData)
    {
        var store = await _storeContext.GetCurrentStoreAsync();

        var model = new CwThemeHeadModel
        {
            RootCss = await _themeCssService.GenerateRootCssAsync(store.Id)
        };

        var cssRef = await _themeCssFileService.GetRenderRefAsync(store.Id);
        if (cssRef != null)
        {
            model.CustomCssVirtualPath = cssRef.VirtualPath;
            model.CustomCssVersion = cssRef.Version;
        }

        //live-preview: the framing middleware validated a preview token for this request and left the admin
        //origin here → inject the bridge so the Brand-Designer iframe can drive this page (§ live-preview Phase 1)
        model.PreviewAdminOrigin = HttpContext?.Items[CwPreviewFramingMiddleware.AdminOriginItemKey] as string;

        //hand the bridge the active theme's click-to-edit region map (Phase 2) — only in preview mode
        if (!string.IsNullOrEmpty(model.PreviewAdminOrigin))
        {
            var manifest = await _manifestService.GetManifestAsync(await _themeContext.GetWorkingThemeNameAsync());
            model.PreviewRegionMapJson = JsonSerializer.Serialize(manifest.RegionMap, _regionMapJson);
            model.PreviewBridgeVersion = GetBridgeVersion();
        }

        return View("~/Plugins/CW.ThemeCore/Views/Components/CwThemeHead/Default.cshtml", model);
    }

    #endregion

    #region Utilities

    //cache-buster for the preview bridge: the file's last-write ticks, so a changed bridge always reloads
    //(a parent hard-refresh doesn't reliably re-fetch an iframe's <script> subresource)
    private static long GetBridgeVersion()
    {
        try
        {
            var path = CommonHelper.DefaultFileProvider.MapPath("~/Plugins/CW.ThemeCore/Content/BrandPreview/cw-preview-bridge.js");
            return System.IO.File.Exists(path) ? System.IO.File.GetLastWriteTimeUtc(path).Ticks : 0L;
        }
        catch { return 0L; }
    }

    #endregion
}
