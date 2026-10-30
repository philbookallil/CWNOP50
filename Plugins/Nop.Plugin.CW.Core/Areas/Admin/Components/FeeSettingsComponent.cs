using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Nop.Plugin.CW.Core.Areas.Admin.Models.Exchanges.FeeRule;
using Nop.Plugin.CW.Core.Domain.Exchanges;
using Nop.Plugin.CW.Core.Domain.Settings;
using Nop.Plugin.CW.Core.Helpers;
using Nop.Plugin.CW.Core.Services.Settings;
using Nop.Web.Framework.Components;

namespace Nop.Plugin.CW.Core.Areas.Admin.Components
{
    [ViewComponent(Name = "FeeSettings")]
    public class FeeSettingsComponent : NopViewComponent
    {
        #region Fields

        protected readonly ICWFeatureService _cwFeatureService;

        #endregion

        #region Ctor

        public FeeSettingsComponent(ICWFeatureService cwFeatureService)
        {
            _cwFeatureService = cwFeatureService;
        }

        #endregion

        #region Utilities

        private IList<SelectListItem> PrepareFileTypesAsync()
        {
            IList<SelectListItem> items = new List<SelectListItem>();

            var excludeFeeTypes = new List<FeeType> { FeeType.AdditionalFee };
            var feeTypes = Enum.GetValues(typeof(FeeType))
                            .Cast<FeeType>()
                            .Where(x => !excludeFeeTypes.Contains(x))
                            .ToArray();

            //if (items == null)
            //    throw new ArgumentNullException(nameof(items));

            //prepare available order statuses
            //var availableStatusItems = await FeeType.AdditionalFee.ToSelectListAsync(false);
            foreach (var fee in feeTypes)
            {
                items.Add(new SelectListItem
                {
                    Text = ExchangesHelper.DetermineFeeTypeName(fee),
                    Value = Convert.ToInt32(fee).ToString()
                });
            }
            return items;

        }

        private IList<SelectListItem> PrepareOperatorTypesAsync()
        {
            IList<SelectListItem> items = new List<SelectListItem>();

            var feeTypes = Enum.GetValues(typeof(FeeRuleOperator))
                            .Cast<FeeRuleOperator>()
                            .ToArray();

            //if (items == null)
            //    throw new ArgumentNullException(nameof(items));

            //prepare available order statuses
            //var availableStatusItems = await FeeType.AdditionalFee.ToSelectListAsync(false);
            foreach (var fee in feeTypes)
            {
                items.Add(new SelectListItem
                {
                    Text = ExchangesHelper.DetermineFeeRuleOperatorName(fee),
                    Value = Convert.ToInt32(fee).ToString()
                });
            }
            return items;

        }

        #endregion

        #region Methods

        public async Task<IViewComponentResult> InvokeAsync(int storeId)
        {
            if (!await _cwFeatureService.IsEnabledAsync(CWFeature.Exchanges, storeId))
                return Content(string.Empty);

            var feeSettingsSearchModel = new FeeRuleSearchModel
            {
                AvailableFeeTypes = PrepareFileTypesAsync(),
                AvailableOperatorsTypes = PrepareOperatorTypesAsync(),
                StoreId = storeId
            };

            feeSettingsSearchModel.SetGridPageSize();

            return View("~/Plugins/CW.Core/Areas/Admin/Views/Exchanges/_FeeRules.cshtml", feeSettingsSearchModel);
        }

        #endregion
    }
}
