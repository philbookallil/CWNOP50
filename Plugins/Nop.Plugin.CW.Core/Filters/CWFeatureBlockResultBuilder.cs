using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Nop.Core.Http;
using Nop.Services.Helpers;
using Nop.Services.Localization;
using Nop.Services.Messages;
using Nop.Web.Framework;

namespace Nop.Plugin.CW.Core.Filters;

/// <summary>
/// Builds the action result returned when a CW feature is disabled
/// </summary>
public class CWFeatureBlockResultBuilder : ICWFeatureBlockResultBuilder
{
    #region Fields

    protected readonly ILocalizationService _localizationService;
    protected readonly INotificationService _notificationService;
    protected readonly IWebHelper _webHelper;

    #endregion

    #region Ctor

    public CWFeatureBlockResultBuilder(ILocalizationService localizationService,
        INotificationService notificationService,
        IWebHelper webHelper)
    {
        _localizationService = localizationService;
        _notificationService = notificationService;
        _webHelper = webHelper;
    }

    #endregion

    #region Methods

    public virtual async Task<IActionResult> CreateAsync(HttpRequest request, string redirectRouteName = null)
    {
        ArgumentNullException.ThrowIfNull(request);

        var message = await _localizationService.GetResourceAsync("Plugins.CW.Core.Features.Disabled");

        var isAdmin = request.RouteValues.TryGetValue("area", out var area)
            && string.Equals(area?.ToString(), AreaNames.ADMIN, StringComparison.OrdinalIgnoreCase);

        var isAjax = _webHelper.IsAjaxRequest(request);
        var isPost = HttpMethods.IsPost(request.Method);

        //AJAX GET: the caller injects the response body straight into a panel
        //(see admin.store.navigation.js loadPartial). Anything other than an empty
        //body would paint an error page or the whole dashboard inside a tab.
        if (isAjax && !isPost)
            return new EmptyResult();

        //DataTables grid read: return a well-formed empty page rather than an error shape,
        //so the grid renders empty instead of raising a JS alert
        if (isAjax && isPost && request.HasFormContentType && request.Form.ContainsKey("draw"))
            return new JsonResult(new { draw = request.Form["draw"].ToString(), recordsTotal = 0, recordsFiltered = 0, data = Array.Empty<object>() });

        //AJAX POST: mirror the shape every CW admin POST already returns
        if (isAjax && isPost)
            return new JsonResult(new { success = false, error = message });

        if (isAdmin)
        {
            //full page load or non-AJAX POST in the admin area
            _notificationService.ErrorNotification(message);

            return new RedirectToActionResult("Index", "Home", new { area = AreaNames.ADMIN });
        }

        //public store
        return string.IsNullOrEmpty(redirectRouteName)
            ? new RedirectToRouteResult(NopRouteNames.General.HOMEPAGE, null)
            : new RedirectToRouteResult(redirectRouteName, null);
    }

    #endregion
}