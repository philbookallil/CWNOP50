using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Nop.Core;
using Nop.Core.Domain.Customers;
using Nop.Core.Domain.Stores;
using Nop.Core.Infrastructure;
using CW.Common.Helpers;
using Nop.Services.Authentication;
using Nop.Web.Framework.UI;

namespace Nop.Plugin.CW.Core.Filters
{
    public class CoreActionFilter : IActionFilter
    {
        private IList<Type> _disallowedControllerTypes = new List<Type>();

        protected virtual void AddDisallowedControllerType(Type controllerType)
        {
            _disallowedControllerTypes.Add(controllerType);
        }

        protected virtual void RemoveDisallowedControllerType(Type controllerType)
        {
            if (IsControllerTypeDisallowed(controllerType))
            {
                _disallowedControllerTypes.Remove(controllerType);
            }
        }

        protected virtual bool IsControllerTypeDisallowed(Type controllerType)
        {
            return _disallowedControllerTypes.Contains(controllerType);
        }

        protected virtual bool AllowAjaxRequests(ControllerContext controllerContext, ActionDescriptor actionDescriptor)
        {
            return true;
        }

        /// <summary>
        /// Adds the provided list of css references to the current Page context
        /// </summary>
        /// <param name="cssParts">A list of virtual or absolute URL paths to the css files references</param>
        protected void AddCSSToContext(string[] cssParts)
        {
            var pageBuilder = EngineContext.Current.Resolve<INopHtmlHelper>();
            foreach (string cssPart in cssParts)
            {
                pageBuilder.AddCssFileParts(cssPart, string.Empty, true);
            }
        }

        /// <summary>
        /// Adds the provided css file reference to the current Page context
        /// </summary>
        /// <param name="cssPart">A virtual or absolute URL path to the css that will be included</param>
        protected void AddCSSToContext(string cssPart)
        {
            AddCSSToContext(new string[] { cssPart });
        }





        /// <summary>
        /// Determines if the given controller is an instance that inherits from or is of a given Type
        /// </summary>
        /// <typeparam name="T">The type specifier on which the type check will be made</typeparam>
        /// <param name="controller">The controller instance that will be checked</param>
        /// <returns>Returns true if the controller matches the type, false otherwise</returns>
        protected bool IsController<T>(object controller)
        {
            return (controller is T);
        }

        /// <summary>
        /// Determines if the given controller's Type has the specified name or full name
        /// </summary>
        /// <typeparam name="T">The type specifier on which the type check will be made</typeparam>
        /// <param name="controller">The controller instance that will be checked</param>
        /// <param name="typeName">The Type name that will be used as compare value</param>
        /// <param name="useFullName">Wether to use the short or full name of the controller's type</param>
        /// <returns>Returns true if the controller matches the type, false otherwise</returns>
        protected bool IsController(object controller, string typeName, bool useFullName = false)
        {
            var controllerType = controller.GetType();
            return useFullName ? (controllerType.FullName == typeName) : (controllerType.Name == typeName);
        }

        /// <summary>
        /// Checks if the given action descriptor has the given action name
        /// </summary>
        /// <param name="actionName">The action name to check</param>
        /// <param name="actionDescriptor">The action descrptor that will be compared</param>
        /// <returns>True if the action descriptor represents the given action name, False otherwise</returns>
        protected bool IsActionName(string actionName, ControllerActionDescriptor actionDescriptor)
        {
            return (actionDescriptor.ActionName == actionName);
        }

        /// <summary>
        /// Checks if the given action descriptor has any of the given action names
        /// </summary>
        /// <param name="actionNames">The action names to check for</param>
        /// <param name="actionDescriptor">The action descrptor that will be compared</param>
        /// <returns>True if the action descriptor represents any of the given action names, False otherwise</returns>
        protected bool IsActionName(string[] actionNames, ControllerActionDescriptor actionDescriptor)
        {
            return (actionNames.Contains(actionDescriptor.ActionName));
        }

        /// <summary>
        /// Checks if the current HTTP Request has a querystring parameter with the given name
        /// </summary>
        /// <param name="paramName">The parameter name</param>
        /// <param name="httpContext">The current HttpContext</param>
        /// <returns>True if the parameter exists in the querystring, false otherwise</returns>
        protected bool HasQuerystringParam(string paramName, HttpContext httpContext)
        {
            return HttpContextHelper.HasQuerystringParam(paramName, httpContext);
        }

        /// <summary>
        /// Checks if the current HTTP Request has a querystring parameter with the given name and value
        /// </summary>
        /// <param name="paramName">The parameter name</param>
        /// <param name="paramValue">The parameter value</param>
        /// <param name="httpContext">The current HttpContext</param>
        /// <returns>True if the parameter exists in the querystring and also has the given value, false otherwise</returns>
        protected bool HasQuerystringParam(string paramName, string paramValue, HttpContext httpContext)
        {
            return HttpContextHelper.HasQuerystringParam(paramName, paramValue, httpContext);
        }

        /// <summary>
        /// Gets a parameter from the current querystring as a string value
        /// </summary>
        /// <param name="paramName">The parameter name</param>
        /// <param name="httpContext">The current HttpContext</param>
        /// <returns>Returns the given parameter as a string value or null if not found</returns>
        protected string GetStringQuerystringParam(string paramName, HttpContext httpContext)
        {
            return HttpContextHelper.GetStringQuerystringParam(paramName, httpContext);
        }

        /// <summary>
        /// Gets a parameter from the current querystring as an integer value
        /// </summary>
        /// <param name="paramName">The parameter name</param>
        /// <param name="httpContext">The current HttpContext</param>
        /// <returns>
        /// Returns the given parameter as a nullable integer value or null if not found or
        /// if the value cannot be properly converted to an integer
        /// </returns>
        protected int? GetIntegerQuerystringParam(string paramName, HttpContext httpContext)
        {
            return HttpContextHelper.GetIntegerQuerystringParam(paramName, httpContext);
        }

        /// <summary>
        /// Gets a given parameter from the current HTTP context request, that is values that are coming as part
        /// of the request.
        /// </summary>
        /// <param name="paramName">The parameter name</param>
        /// <param name="httpContext">The current HTTP context</param>
        /// <returns>The parameter value as a string or null if not found</returns>
        protected string GetRequestParam(string paramName, HttpContext httpContext)
        {
            return HttpContextHelper.GetRequestParam(paramName, httpContext);
        }

        /// <summary>
        /// Determines if the current HTTP request is a POST
        /// </summary>
        /// <param name="httpContext">The current HTTP context</param>
        /// <returns>True if the request is a POST, false otherwise</returns>
        protected bool IsPost(HttpContext httpContext)
        {
            return HttpContextHelper.IsPost(httpContext);
        }

        /// <summary>
        /// Determines if the current HTTP request is a GET
        /// </summary>
        /// <param name="httpContext">The current HTTP context</param>
        /// <returns>True if the request is a GET, false otherwise</returns>
        protected bool IsGet(HttpContext httpContext)
        {
            return HttpContextHelper.IsGet(httpContext);
        }

        /// <summary>
        /// Adds a given value to the current HTTP session
        /// </summary>
        /// <param name="key">The value key on the session</param>
        /// <param name="value">The value to add</param>
        /// <param name="httpContext">The current HTTP context</param>
        protected void AddSessionValue(string key, string value, HttpContext httpContext)
        {
            HttpContextHelper.AddSessionValue(key, value, httpContext);
        }

        /// <summary>
        /// Removes a given session value from the current session
        /// </summary>
        /// <param name="key">The session key that identifies the value</param>
        /// <param name="httpContext">The current HTTP context</param>
        protected void ClearSessionValue(string key, HttpContext httpContext)
        {
            HttpContextHelper.ClearSessionValue(key, httpContext);
        }

        /// <summary>
        /// Obtains a session value from the current HTTP session
        /// </summary>
        /// <param name="key">The key that identifies the session value</param>
        /// <param name="httpContext">The HTTP context</param>
        /// <returns>The session value or null if not found</returns>
        protected string GetSessionStringValue(string key, HttpContext httpContext)
        {
            return HttpContextHelper.GetSessionStringValue(key, httpContext);
        }

        /// <summary>
        /// Determines if the current request is Ajax
        /// </summary>
        /// <param name="httpContext">The current HTTP context</param>
        /// <returns><code>true</code> if the request is ajax or <code>false</code> otherwise</returns>
        protected bool IsAjaxRequest(HttpContext httpContext)
        {
            return HttpContextHelper.IsAjaxRequest(httpContext);
        }


        /// <summary>
        /// Creates a Redirect Route using the provided data
        /// </summary>
        /// <param name="action">The name of the controller action</param>
        /// <param name="controller">The controller name</param>
        /// <param name="routeValues">A set of additional route values to assign to the redirect</param>
        /// <param name="area">An MVC Area name</param>
        /// <returns>
        ///     A <code>RedirectToRouteResult</code> instance that will instruct the MVC framework to perform the 
        ///     desired redirect on the response
        /// </returns>
        protected RedirectToRouteResult CreateRedirect(string action, string controller, RouteValueDictionary routeValues = null, string area = "")
        {
            RouteValueDictionary redirectValues = new RouteValueDictionary();
            redirectValues.Add("Controller", controller);
            redirectValues.Add("Action", action);
            if (area != null)
            {
                redirectValues.Add("area", area);
            }
            if (routeValues != null)
            {
                foreach (string routeValueKey in routeValues.Keys)
                {
                    if (redirectValues.ContainsKey(routeValueKey))
                    {
                        redirectValues[routeValueKey] = routeValues[routeValueKey]; // overwrite the value
                    }
                    else
                    {
                        redirectValues.Add(routeValueKey, routeValues[routeValueKey]);
                    }
                }
            }
            return new RedirectToRouteResult(redirectValues);
        }

        /// <summary>
        ///  Creates a Redirect Route using the provided data
        /// </summary>
        /// <param name="actionDescriptor">The <code>ActionDescriptor</code> for the current action context</param>
        /// <param name="routeValues">A set of additional route values to assign to the redirect</param>
        /// <param name="area">An MVC Area name</param>
        /// <returns>
        ///     A <code>RedirectToRouteResult</code> instance that will instruct the MVC framework to perform the 
        ///     desired redirect on the response
        /// </returns>
        protected RedirectToRouteResult CreateRedirect(ControllerActionDescriptor actionDescriptor, RouteValueDictionary routeValues = null, string area = "")
        {
            string actionName = actionDescriptor.ActionName;
            string controllerName = actionDescriptor.ControllerName;
            return CreateRedirect(actionName, controllerName, routeValues, area);
        }

        /// <summary>
        /// Obtains the current MVC Area name
        /// </summary>
        /// <param name="routeData">The current route's data</param>
        /// <returns>The MVC area name or null if none can be found</returns>
        protected string CurrentMVCArea(RouteData routeData)
        {
            return HttpContextHelper.GetMVCArea(routeData);
        }

        /// <summary>
        /// Determines if the current controller context is in the NOP admin area
        /// </summary>
        /// <param name="routeData"></param>
        /// <returns></returns>
        protected bool IsNOPAdminArea(RouteData routeData)
        {
            return HttpContextHelper.IsNOPAdminArea(routeData);
        }

        /// <summary>
        /// Determines if the current controller context is in the Admin Area
        /// </summary>
        /// <param name="controllerContext"></param>
        /// <returns></returns>
        protected bool IsAdminArea(HttpContext httpContext)
        {
            return HttpContextHelper.IsAdminArea(httpContext);
        }

        /// <summary>
        /// Determines if the current controller context belongs to the NOP admin area
        /// </summary>
        /// <param name="httpContext">The current HTTP context</param>
        /// <returns>True of the current controller context is the NOP admin area, false otherwise</returns>
        protected bool IsAdminAreaContext(HttpContext httpContext, RouteData routeData)
        {
            return HttpContextHelper.IsAdminAreaContext(httpContext, routeData);
        }


        /// <summary>
        /// Checks if the current HTTP Request has a form value with the given name
        /// </summary>
        /// <param name="formValueName">The form value name</param>
        /// <param name="httpContext">The current HttpContext</param>
        /// <returns>True if the form value exists in the request, false otherwise</returns>
        protected bool HasFormValue(string formValueName, HttpContext httpContext)
        {
            return HttpContextHelper.HasFormValue(formValueName, httpContext);
        }


        /// <summary>
        /// Must be implemented by child classes to specify if this action filter is going to be
        /// included on the filter pipeline for the current request. Child classes just need to return a "true"
        /// value if they want them to be included on the filter pipeline
        /// </summary>
        /// <param name="controllerContext">The controller context related to the current request</param>
        /// <param name="actionDescriptor">The action descriptor for the current request</param>
        /// <returns>
        ///     A "true" value if the implementing class wants to be included/participate on the current 
        ///     request filter pipeline
        /// </returns>
        protected virtual bool IsFilterActive(ControllerContext controllerContext, ControllerActionDescriptor actionDescriptor)
        {
            if (IsAjaxRequest(controllerContext.HttpContext) && !AllowAjaxRequests(controllerContext, actionDescriptor))
                return false;
            return !IsControllerTypeDisallowed(actionDescriptor.ControllerTypeInfo.AsType());
        }

        /// <summary>
        /// Gets a reference to the current logged in user
        /// </summary>
        /// <returns>The current user/customer</returns>
        protected virtual async Task<Customer> GetAuthenticatedCustomer()
        {
            var authService = EngineContext.Current.Resolve<IAuthenticationService>();
            return await authService.GetAuthenticatedCustomerAsync();
        }

        /// <summary>
        /// Gets the current MVC Controller
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        protected Controller GetController(ActionExecutingContext context)
        {
            return context.Controller as Controller;
        }

        /// <summary>
        /// Gets the current MVC Controller
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        protected Controller GetController(ActionExecutedContext context)
        {
            return context.Controller as Controller;
        }

        public virtual void OnActionExecuting(ActionExecutingContext context)
        {
            // do nothing by default, child classes can override to implement custom logic
        }

        public virtual void OnActionExecuted(ActionExecutedContext context)
        {
            // do nothing by default, child classes can override to implement custom logic
        }
    }
}