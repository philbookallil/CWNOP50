using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Nop.Plugin.CW.Core.Areas.Admin.Factories;
using Nop.Plugin.CW.Core.Areas.Admin.Models.Exchanges;
using Nop.Web.Areas.Admin.Controllers;
using Nop.Web.Areas.Admin.Models.Orders;

namespace Nop.Plugin.CW.Core.Filters
{
    public class ReturnRequestAdminFilter : CoreActionFilter, IAsyncActionFilter
    {
        #region Fields

        protected readonly IReturnRequestExchangeModelFactory _returnRequestExchangeModelFactory;

        #endregion

        #region Ctor

        public ReturnRequestAdminFilter(IReturnRequestExchangeModelFactory returnRequestExchangeModelFactory)
        {
            _returnRequestExchangeModelFactory = returnRequestExchangeModelFactory;
        }

        #endregion

        #region Utilities

        private bool IsReturnRequestAdminEditPage(HttpContext httpContext, object controller, ControllerActionDescriptor actionDescriptor)
        {
            return IsGet(httpContext) && IsActionName("Edit", actionDescriptor) &&
                    IsController<ReturnRequestController>(controller);
        }

        private async Task<CWReturnRequestModel> BuildReturnExchangeModelAsync(ReturnRequestModel requestData)
        {
            CWReturnRequestModel model = new(requestData);
            await _returnRequestExchangeModelFactory.PrepareExchangeRequestModelAsync(model, requestData);
            return model;
        }

        #endregion

        #region Methods

        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var actionDescriptor = context.ActionDescriptor;

            var resultContext = await next();

            if (actionDescriptor is ControllerActionDescriptor)
            {
                var controllerActionDescriptor = actionDescriptor as ControllerActionDescriptor;
                var controller = context.Controller;
                var httpContext = context.HttpContext;

                if (IsReturnRequestAdminEditPage(httpContext, controller, controllerActionDescriptor))
                {
                    if (controller is Controller)
                    {
                        var mvcController = controller as Controller;
                        // If the controller's model is the NOP ReturnRequest class
                        if (mvcController.ViewData.Model is ReturnRequestModel model)
                        {
                            // We create an instance of our custom model and replace the current one with ours
                            mvcController.ViewData.Model = await BuildReturnExchangeModelAsync(model);
                        }
                    }
                }
            }
        }

        #endregion
    }
}