using Microsoft.AspNetCore.Mvc;
using Nop.Plugin.CW.Core.Areas.Admin.Models.Exchanges;
using Nop.Plugin.CW.Core.Domain.Settings;
using Nop.Plugin.CW.Core.Services.Settings;
using Nop.Web.Framework.Components;

namespace Nop.Plugin.CW.Core.Areas.Admin.Components
{
    [ViewComponent(Name = "ExchangeSettings")]
    public class ExchangeSettingsComponent : NopViewComponent
    {
        #region Fields

        protected readonly ICWFeatureService _cwFeatureService;

        #endregion

        #region Ctor

        public ExchangeSettingsComponent(ICWFeatureService cwFeatureService)
        {
            _cwFeatureService = cwFeatureService;
        }

        #endregion

        #region Methods

        public async Task<IViewComponentResult> InvokeAsync(int storeId)
        {
            if (!await _cwFeatureService.IsEnabledAsync(CWFeature.Exchanges, storeId))
                return Content(string.Empty);

            var model = new ExchangeSettingsSearchModel
            {
                StoreId = storeId
            };

            model.SetGridPageSize();

            return View(model);
        }

        #endregion
    }
}