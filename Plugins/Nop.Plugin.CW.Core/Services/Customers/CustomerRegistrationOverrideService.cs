using Microsoft.AspNetCore.Mvc;
using Nop.Core;
using Nop.Core.Domain.Customers;
using Nop.Core.Events;
using Nop.Core.Http;
using Nop.Plugin.CW.Core.Services.PackNames;
using Nop.Services.Authentication;
using Nop.Services.Authentication.MultiFactor;
using Nop.Services.Common;
using Nop.Services.Customers;
using Nop.Services.Helpers;
using Nop.Services.Localization;
using Nop.Services.Logging;
using Nop.Services.Messages;
using Nop.Services.Orders;
using Nop.Services.Security;

namespace Nop.Plugin.CW.Core.Services.Customers;

/// <summary>
/// Sends a qualifying customer to the subsidy catalogue or the bulk ordering screen instead of the homepage
/// when they sign in (<c>SubsidySendUserAfterLogin</c>, <c>BulkOrderSendUserAfterLogin</c>).
/// </summary>
/// <remarks>
/// <para>
/// The service rather than the controller, because <c>SignInCustomerAsync</c> is where the destination is
/// actually decided and every route into the store passes through it: the login form, registration
/// (<c>CustomerController :1074</c>) and external authentication all call it. 4.6 patched each of its own
/// logon call sites separately and had to keep them in step by hand.
/// </para>
/// <para>
/// One method is overridden and the base is always called first, so sign-in itself — cart migration, the
/// authentication cookie, <c>CustomerLoggedinEvent</c>, the activity log — is entirely nop's and stays
/// nop's. Only the destination is reconsidered, and only in one case.
/// </para>
/// </remarks>
public class CustomerRegistrationOverrideService : CustomerRegistrationService
{
    #region Fields

    protected readonly IPackNameLandingService _packNameLandingService;

    #endregion

    #region Ctor

    public CustomerRegistrationOverrideService(CustomerSettings customerSettings,
        IAuthenticationService authenticationService,
        ICustomerActivityService customerActivityService,
        ICustomerService customerService,
        IEncryptionService encryptionService,
        IEventPublisher eventPublisher,
        IGenericAttributeService genericAttributeService,
        ILocalizationService localizationService,
        IMultiFactorAuthenticationPluginManager multiFactorAuthenticationPluginManager,
        INewsLetterSubscriptionService newsLetterSubscriptionService,
        INotificationService notificationService,
        IPermissionService permissionService,
        IRewardPointService rewardPointService,
        IShoppingCartService shoppingCartService,
        IStoreContext storeContext,
        IWebHelper webHelper,
        IWorkContext workContext,
        IWorkflowMessageService workflowMessageService,
        RewardPointsSettings rewardPointsSettings,
        IPackNameLandingService packNameLandingService) : base(customerSettings,
            authenticationService,
            customerActivityService,
            customerService,
            encryptionService,
            eventPublisher,
            genericAttributeService,
            localizationService,
            multiFactorAuthenticationPluginManager,
            newsLetterSubscriptionService,
            notificationService,
            permissionService,
            rewardPointService,
            shoppingCartService,
            storeContext,
            webHelper,
            workContext,
            workflowMessageService,
            rewardPointsSettings)
    {
        _packNameLandingService = packNameLandingService;
    }

    #endregion

    #region Utilities

    /// <summary>
    /// Whether this result sends the customer to the homepage and nowhere more specific — by route, or by a
    /// returnUrl that resolves to the site root.
    /// </summary>
    /// <remarks>
    /// The URL comparison is deliberately narrow: only the site root in its handful of spellings counts.
    /// Anything with a path, including a query or a fragment, is treated as a real destination. Being wrong
    /// in that direction costs a customer nothing — they land where they asked to — while being wrong the
    /// other way loses whatever they were doing.
    /// </remarks>
    protected virtual bool IsHomepageDestination(IActionResult result)
    {
        if (result is RedirectToRouteResult route)
            return route.RouteName == NopRouteNames.General.HOMEPAGE;

        if (result is not RedirectResult redirect)
            return false;

        var url = redirect.Url?.Trim();

        return string.IsNullOrEmpty(url) || url == "/" || url == "~/";
    }

    #endregion

    #region Methods

    /// <summary>
    /// nop's sign-in, with the homepage default replaced by the subsidy catalogue or the bulk ordering screen
    /// where the store asks for it and the customer qualifies.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>An explicit returnUrl always wins, and the test for that is exact rather than a guess.</b> The base
    /// method returns a <c>RedirectResult</c> when it honoured a local <c>returnUrl</c>, and a
    /// <c>RedirectToRouteResult</c> named <c>HOMEPAGE</c> when it fell back
    /// (<c>CustomerRegistrationService :455-459</c>). Only the second is replaced, so a customer who signed
    /// in from a product page, a cart, or midway through checkout still arrives where they were going. A
    /// setting called "send the user after login" is not a licence to interrupt something already in
    /// progress — it decides where somebody lands when nop had nowhere particular to put them.
    /// </para>
    /// <para>
    /// Registration is deliberately included: nop routes it through this same method, so it comes for free
    /// and excluding it would take extra code. A customer who has just registered has nowhere they were
    /// going, which is exactly the case this replaces.
    /// </para>
    /// <para>
    /// The customer is passed to the gate explicitly. By this point the base has repointed the work context,
    /// so reading it would give the same answer today — but the parameter is the fact and the work context
    /// is a side effect, and the impersonation path (which shares this gate) has no such side effect at all.
    /// </para>
    /// </remarks>
    public override async Task<IActionResult> SignInCustomerAsync(Customer customer, string returnUrl, bool isPersist = false)
    {
        var result = await base.SignInCustomerAsync(customer, returnUrl, isPersist);

        //What matters is where the customer is being SENT, not how nop expressed it. The base returns a
        //RedirectToRouteResult for its homepage default and a RedirectResult when it honoured a local
        //returnUrl - but a returnUrl of "/" IS the homepage, and it is what the login link carries in the
        //commonest case of all: log out, land on the homepage, log back in from there. Testing only for the
        //route result made the setting look broken for exactly the customers most likely to notice.
        //A returnUrl pointing anywhere else is somebody in the middle of something - a product page, a
        //cart, a checkout step - and still wins.
        if (!IsHomepageDestination(result))
            return result;

        var route = await _packNameLandingService.ResolveSignInLandingRouteAsync(customer);

        return route != null ? new RedirectToRouteResult(route, null) : result;
    }

    #endregion
}