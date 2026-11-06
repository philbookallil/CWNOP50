using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Filters;
using Nop.Core;
using Nop.Plugin.CW.ThemeCore.Services.Layout;
using Nop.Web.Framework;
using Nop.Services.Themes;
using Nop.Web.Framework.UI;

namespace Nop.Plugin.CW.ThemeCore.Filters;

/// <summary>
/// Adds the active theme's per-store layout CSS classes to the &lt;html&gt; tag (via
/// <see cref="INopHtmlHelper.AddPageCssClassParts"/>) for storefront GET requests. Runs before the
/// view renders so the classes appear in <c>NopHtml.GeneratePageCssClasses()</c>. Admin, non-GET
/// requests, and themes without a manifest are skipped.
/// </summary>
public class ThemeLayoutPageClassFilter : IAsyncActionFilter
{
    #region Fields

    protected readonly IThemeContext _themeContext;
    protected readonly IStoreContext _storeContext;
    protected readonly IThemeLayoutService _layoutService;
    protected readonly INopHtmlHelper _nopHtmlHelper;

    #endregion

    #region Ctor

    public ThemeLayoutPageClassFilter(
        IThemeContext themeContext,
        IStoreContext storeContext,
        IThemeLayoutService layoutService,
        INopHtmlHelper nopHtmlHelper)
    {
        _themeContext = themeContext;
        _storeContext = storeContext;
        _layoutService = layoutService;
        _nopHtmlHelper = nopHtmlHelper;
    }

    #endregion

    #region Methods

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (ShouldApply(context))
        {
            var theme = await _themeContext.GetWorkingThemeNameAsync();
            if (!string.IsNullOrEmpty(theme))
            {
                var store = await _storeContext.GetCurrentStoreAsync();
                foreach (var cssClass in await _layoutService.GetActivePageCssClassesAsync(theme, store.Id))
                    _nopHtmlHelper.AddPageCssClassParts(cssClass);
            }
        }

        await next();
    }

    #endregion

    #region Utilities

    private static bool ShouldApply(ActionExecutingContext context)
    {
        //storefront only (admin is never themed) and GET only (layout classes affect rendered pages)
        var area = context.RouteData.Values.TryGetValue("area", out var value) ? value?.ToString() : null;
        if (string.Equals(area, AreaNames.ADMIN, StringComparison.OrdinalIgnoreCase))
            return false;

        return HttpMethods.IsGet(context.HttpContext.Request.Method);
    }

    #endregion
}
