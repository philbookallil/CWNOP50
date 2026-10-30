using Microsoft.AspNetCore.Mvc;
using Nop.Plugin.CW.Core.Factories;
using Nop.Web.Framework.Components;

namespace Nop.Plugin.CW.Core.Components
{
    [ViewComponent(Name = "ReturnRequestsConfirmations")]
    public class ReturnRequestsConfirmationsViewComponent : NopViewComponent
    {
        #region Fields

        protected readonly IReturnRequestItemModelFactory _returnRequestItemModelFactory;

        #endregion

        #region Ctor

        public ReturnRequestsConfirmationsViewComponent(IReturnRequestItemModelFactory returnRequestItemModelFactory)
        {
            _returnRequestItemModelFactory = returnRequestItemModelFactory;
        }

        #endregion

        #region Methods

        public async Task<IViewComponentResult> InvokeAsync(int[] returnRequestsIds)
        {
            var model = await _returnRequestItemModelFactory.PrepareReturnExchangesConfirmationModelAsync(returnRequestsIds);

            return await ViewAsync(model);
        }

        #endregion
    }
}
