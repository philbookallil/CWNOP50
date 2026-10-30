using Nop.Core;
using Nop.Core.Domain.Customers;
using Nop.Core.Domain.Stores;
using Nop.Plugin.CW.Core.Domain.Customers;
using Nop.Plugin.CW.Core.Extensions;
using Nop.Services.Common;
using Nop.Services.Customers;
using Nop.Services.Localization;
using Nop.Services.Logging;
using Nop.Services.Security;
using Nop.Web.Framework.Mvc.Filters;

namespace Nop.Plugin.CW.Core.BusinessLogic;

public class CustomersLogic : ICustomersLogic
{
    #region Fields

    private readonly IWorkContext _workContext;
    private readonly ICustomerActivityService _customerActivityService;
    private readonly ICustomerService _customerService;
    private readonly IGenericAttributeService _genericAttributeService;
    private readonly ILocalizationService _localizationService;
    private readonly IStoreContext _storeContext;
    private readonly ILogger _logger;

    #endregion

    #region Ctor

    public CustomersLogic(IWorkContext workContext,
        ICustomerActivityService customerActivityService,
        ICustomerService customerService,
        IGenericAttributeService genericAttributeService,
        ILocalizationService localizationService,
        ILogger logger,
        IStoreContext storeContext)
    {
        _workContext = workContext;
        _customerActivityService = customerActivityService;
        _customerService = customerService;
        _genericAttributeService = genericAttributeService;
        _localizationService = localizationService;
        _logger = logger;
        _storeContext = storeContext;
    }

    #endregion

    #region Utilities

    /// <summary>
    /// Starts an Impersonation session
    /// </summary>
    /// <param name="impersonatingUser">The user (customer) that is impersonating (owner of the session)</param>
    /// <param name="impersonatedUser">The user (customer) that is being impersonated</param>
    private async Task StartImpersonationAsync(Customer impersonatingUser, Customer impersonatedUser)
    {
        //ensure that a non-admin user cannot impersonate as an administrator
        //otherwise, that user can simply impersonate as an administrator and gain additional administrative privileges
        var impersonatingUserIsAdmin = await _customerService.IsAdminAsync(impersonatingUser);
        var impersonatedUserIsAdmin = await _customerService.IsAdminAsync(impersonatedUser);

        if (!impersonatingUserIsAdmin && impersonatedUserIsAdmin)
        {
            throw new NopException(await _localizationService.GetResourceAsync("Admin.Customers.Customers.NonAdminNotImpersonateAsAdminError"));
        }

        //activity log
        await _customerActivityService.InsertActivityAsync("Impersonation.Started",
            string.Format(await _localizationService.GetResourceAsync("ActivityLog.Impersonation.Started.StoreOwner"), impersonatedUser.Email, impersonatedUser.Id), impersonatedUser);
        await _customerActivityService.InsertActivityAsync(impersonatedUser, "Impersonation.Started",
            string.Format(await _localizationService.GetResourceAsync("ActivityLog.Impersonation.Started.Customer"), impersonatingUser.Email, impersonatingUser.Id), impersonatingUser);

        //ensure login is not required
        impersonatedUser.RequireReLogin = false;
        await _customerService.UpdateCustomerAsync(impersonatedUser);

        //Link the impersonation session to the owner
        await _genericAttributeService.SaveAttributeAsync<int?>(impersonatingUser, NopCustomerDefaults.ImpersonatedCustomerIdAttribute, impersonatedUser.Id);
    }

    #endregion

    #region Methods

    /// <summary>
    /// Requests an impersonation session on the given store
    /// </summary>
    /// <param name="targetStore">The Store on which the impersonation session is requested</param>
    /// <param name="requestingUser">The user (customer) that is going to perform the impersonation action</param>
    /// <param name="impersonatedUser">The user (customer) that is going to be impersonated</param>
    /// <param name="redirectTo">A relative URL to which the user should be redirected on the target Store</param>
    /// <returns>If the impersonation succeeds returns the URL to which the current request should be redirected, if not an exception is thrown</returns>
    public async Task<string> RequestImpersonation(Store targetStore, Customer requestingUser, int impersonatedUserId, string redirectTo = null)
    {
        // We need to ask the other Store for an impersonation token to be able to login there and impersonate
        var impersonationToken = await RequestImpersonationToken(targetStore, requestingUser.Id, impersonatedUserId);
        var redirectBaseUrl = targetStore.GetUrl();
        // Build an URL to the token based Impersonation feature on the other Store and redirect there
        var redirectToParam = string.IsNullOrEmpty(redirectTo) ? string.Empty : string.Concat("&redirectTo=", redirectTo);
        var redirectUrl = string.Concat(redirectBaseUrl, "impersonate/useToken?token=", impersonationToken.TokenId, redirectToParam);
        // Save custom attributes to help redirect users back to the origin Store
        var currentStore = await _storeContext.GetCurrentStoreAsync();
        await _genericAttributeService
               .SaveAttributeAsync<int?>(requestingUser, ExtendedCustomerAttributeNames.ImpersonationOriginStore, currentStore.Id);
        await _genericAttributeService
               .SaveAttributeAsync<string>(requestingUser, ExtendedCustomerAttributeNames.ImpersonationToken, impersonationToken.TokenId);
        // Return the redirection Url to the Impersonation Token API on the target Store
        return redirectUrl;
    }

    [CheckPermission(StandardPermission.Customers.CUSTOMERS_IMPERSONATION)]
    public async Task ImpersonateCustomer(int customerId)
    {
        var customer = await _customerService.GetCustomerByIdAsync(customerId);
        if (customer == null)
        {
            throw new NopException("Customer with ID: {0} does not exists", customerId);
        }
        await StartImpersonationAsync(await _workContext.GetCurrentCustomerAsync(), customer);
    }

    /// <summary>
    /// Requests an Impersonation Token on the given Store and for the given owner (requestingUserId) and impersonated user (impersonatedUserId)
    /// </summary>
    /// <param name="store">The Store on which the Impersonation Token is requested</param>
    /// <param name="requestingUserId">The Id of the user (customer) that is going to perform the impersonation action</param>
    /// <param name="impersonatedUserId">The Id of the user (customer) that is going to be impersonated</param>
    /// <returns>An Impersonation Token that can be used to initiate an Impersonation session on the given Store</returns>
    public async Task<ImpersonateTokenAPIModel> RequestImpersonationToken(Store store, int requestingUserId, int impersonatedUserId)
    {
        return new ImpersonateTokenAPIModel
        {
            TokenId = Guid.NewGuid().ToString(),
            OwnerId = requestingUserId,
            UserId = impersonatedUserId,
            CreatedOn = DateTime.UtcNow
        };
        /*if (store != null)
        {
            // Instantiate an API client that will use HTTP to comunicate with the Impersonation API on the target Store
            var client = new ImpersonationClient(new APIClient(store.Url));
            try
            {
                // Obtain the Impersonation Token from the given Store's API
                var response = await client.RequestImpersonationToken(requestingUserId, impersonatedUserId);
                if (response.IsSuccessful)
                {
                    return response.Data;
                }
                else
                {
                    _logger.Warning(string.Format("Impersonation cannot retrieve token due to error: {0}", response.RawResponse));
                    throw new NopException("Impersonation API call failed");
                }
            }
            catch (Exception ex)
            {
                _logger.Error("Error while trying to perform Impersonation API call", ex);
                throw new NopException("Impersonation API call error");
            }

        }
        else
        {
            var message = string.Format("The provided Store is null");
            _logger.Warning(message);
            throw new NopException(message);
        }*/
    }

    #endregion
}

public class ImpersonateTokenAPIModel
{
    /// <summary>
    /// A string in the form of a GUID that uniquely identifies the Impersonation token
    /// </summary>
    public string TokenId { get; set; }

    /// <summary>
    /// The ID of the user/customer who requested the impersonation
    /// </summary>
    public int OwnerId { get; set; }

    /// <summary>
    /// The ID of the user/customer being impersonated
    /// </summary>
    public int UserId { get; set; }

    public DateTime CreatedOn { get; set; }
}
