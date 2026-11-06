using Microsoft.AspNetCore.Mvc;
using Nop.Core;
using Nop.Plugin.CW.ThemeCore.Settings;
using Nop.Services.Configuration;
using Nop.Services.Media;
using Nop.Web.Framework.Components;

namespace Nop.Plugin.CW.ThemeCore.Components;

/// <summary>
/// Renders the optional per-store second (co-brand) header logo. A CW theme invokes it where it wants
/// the logo (e.g. the right of the header): <c>@await Component.InvokeAsync("CwSecondLogo")</c>.
/// Renders nothing when no second logo is configured for the current store.
/// </summary>
public class CwSecondLogoViewComponent : NopViewComponent
{
    #region Fields

    protected readonly ISettingService _settingService;
    protected readonly IStoreContext _storeContext;
    protected readonly IPictureService _pictureService;

    #endregion

    #region Ctor

    public CwSecondLogoViewComponent(
        ISettingService settingService,
        IStoreContext storeContext,
        IPictureService pictureService)
    {
        _settingService = settingService;
        _storeContext = storeContext;
        _pictureService = pictureService;
    }

    #endregion

    #region Methods

    public async Task<IViewComponentResult> InvokeAsync()
    {
        var store = await _storeContext.GetCurrentStoreAsync();
        var settings = await _settingService.LoadSettingAsync<CwThemeSettings>(store.Id);

        if (settings.SecondLogoPictureId <= 0)
            return Content(string.Empty);

        var imageUrl = await _pictureService.GetPictureUrlAsync(settings.SecondLogoPictureId, showDefaultPicture: false);
        if (string.IsNullOrEmpty(imageUrl))
            return Content(string.Empty);

        var model = new CwSecondLogoModel
        {
            ImageUrl = imageUrl,
            LinkUrl = settings.SecondLogoLinkUrl,
            AltText = store.Name
        };

        return View("~/Plugins/CW.ThemeCore/Views/Components/CwSecondLogo/Default.cshtml", model);
    }

    #endregion
}
