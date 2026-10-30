using Microsoft.AspNetCore.Mvc;
using Nop.Plugin.CW.Core.Areas.Admin.Models.Exchanges;
using Nop.Plugin.CW.Core.Domain.Exchanges;
using Nop.Web.Framework.Components;

namespace Nop.Plugin.CW.Core.Areas.Admin.Components
{
    [ViewComponent(Name = "FeeConcept")]
    public class FeeConceptViewComponent : NopViewComponent
    {
        public IViewComponentResult Invoke(CWReturnRequestModel exchangeModel, FeeType feeType)
        {
            var viewName = string.Empty;
            switch (feeType)
            {
                case FeeType.RepackagingFee:
                    viewName = "RePackagingFee";
                    break;

                case FeeType.ReShippingFee:
                    viewName = "ReShippingFee";
                    break;

                case FeeType.AdditionalFee:
                    viewName = "AdditionalFee";
                    break;
            }
            if (!string.IsNullOrEmpty(viewName))
            {
                return View(viewName, exchangeModel);
            }
            throw new InvalidOperationException("Cannot determine view for given fee type");
        }
    }
}
