using Nop.Core;
using Nop.Core.Domain.Common;
using Nop.Core.Domain.Messages;
using Nop.Core.Domain.Orders;
using Nop.Core.Events;
using Nop.Plugin.CW.Core.Domain.Exchanges;
using Nop.Plugin.CW.Core.Domain.Messages;
using Nop.Services.Affiliates;
using Nop.Services.Catalog;
using Nop.Services.Common;
using Nop.Services.Customers;
using Nop.Services.Localization;
using Nop.Services.Messages;
using Nop.Services.Orders;
using Nop.Services.Stores;

namespace Nop.Plugin.CW.Core.Services.Messages;

public class ExchangeWorkflowMessageService : WorkflowMessageService, IExchangeWorkflowMessageService
{
    #region Fields

    protected readonly IMessageTokenProviderOverride _messageTokenProviderOverride;

    #endregion

    #region Ctor

    public ExchangeWorkflowMessageService(CommonSettings commonSettings,
        EmailAccountSettings emailAccountSettings,
        IAddressService addressService,
        IAffiliateService affiliateService,
        ICustomerService customerService,
        IEmailAccountService emailAccountService,
        IEventPublisher eventPublisher,
        ILanguageService languageService,
        ILocalizationService localizationService,
        IMessageTemplateService messageTemplateService,
        IMessageTokenProvider messageTokenProvider,
        IOrderService orderService,
        IProductService productService,
        IQueuedEmailService queuedEmailService,
        IStoreContext storeContext,
        IStoreService storeService,
        ITokenizer tokenizer,
        MessagesSettings messagesSettings,
        IMessageTokenProviderOverride messageTokenProviderOverride) : base(commonSettings,
            emailAccountSettings,
            addressService,
            affiliateService,
            customerService,
            emailAccountService,
            eventPublisher,
            languageService,
            localizationService,
            messageTemplateService,
            messageTokenProvider,
            orderService,
            productService,
            queuedEmailService,
            storeContext,
            storeService,
            tokenizer,
            messagesSettings)
    {
        _messageTokenProviderOverride = messageTokenProviderOverride;
    }

    #endregion

    #region Methods

    /// <summary>
    /// Sends the custom Return Request Receipt message template
    /// </summary>
    /// <param name="returnRequest">The return request</param>
    /// <param name="orderItem">The returned order item</param>
    /// <param name="languageId">The language identifier</param>
    /// <returns>A task that represents the asynchronous operation; the task result contains the ids of the queued e-mails</returns>
    public async Task<IList<int>> SendReturnRequestReceivedNotificationAsync(ReturnRequest returnRequest, OrderItem orderItem, int languageId)
    {
        ArgumentNullException.ThrowIfNull(returnRequest);

        var order = await _orderService.GetOrderByIdAsync(orderItem.OrderId);
        var store = await _storeService.GetStoreByIdAsync(order.StoreId) ?? await _storeContext.GetCurrentStoreAsync();
        languageId = await EnsureLanguageIsActiveAsync(languageId, store.Id);

        var messageTemplates = await GetActiveMessageTemplatesAsync(CWMessageTemplateSystemNames.ReturnReceipt, store.Id);
        if (!messageTemplates.Any())
            return new List<int>();

        var customer = await _customerService.GetCustomerByIdAsync(returnRequest.CustomerId);

        //tokens
        var commonTokens = new List<Token>();
        await _messageTokenProvider.AddOrderTokensAsync(commonTokens, order, languageId);
        await _messageTokenProvider.AddCustomerTokensAsync(commonTokens, returnRequest.CustomerId);
        await _messageTokenProvider.AddReturnRequestTokensAsync(commonTokens, returnRequest, order, orderItem, languageId);
        await _messageTokenProviderOverride.AddReturnPackNameTokensAsync(commonTokens, orderItem);

        return await messageTemplates.SelectAwait(async messageTemplate =>
        {
            //email account
            var emailAccount = await GetEmailAccountOfMessageTemplateAsync(messageTemplate, languageId);

            var tokens = new List<Token>(commonTokens);
            await _messageTokenProvider.AddStoreTokensAsync(tokens, store, emailAccount, languageId);

            //event notification
            await _eventPublisher.MessageTokensAddedAsync(messageTemplate, tokens);

            var billingAddress = await _addressService.GetAddressByIdAsync(order.BillingAddressId);

            var toEmail = (await _customerService.IsGuestAsync(customer))
                ? billingAddress.Email
                : customer.Email;
            var toName = (await _customerService.IsGuestAsync(customer))
                ? billingAddress.FirstName
                : await _customerService.GetCustomerFullNameAsync(customer);

            return await SendNotificationAsync(messageTemplate, emailAccount, languageId, tokens, toEmail, toName);
        }).ToListAsync();
    }

    /// <summary>
    /// Sends a notification when a Return is approved and no extra money is needed
    /// </summary>
    /// <param name="returnRequest">The return request</param>
    /// <param name="paidOrder">The order placed for the replacement</param>
    /// <param name="orderItem">The returned order item</param>
    /// <returns>A task that represents the asynchronous operation; the task result contains the ids of the queued e-mails</returns>
    public async Task<IList<int>> SendReturnRequestApprovedExceptedCustomerNotificationAsync(ReturnRequest returnRequest, Order paidOrder,
        OrderItem orderItem)
    {
        ArgumentNullException.ThrowIfNull(returnRequest);

        int languageId = paidOrder.CustomerLanguageId;
        var store = await _storeService.GetStoreByIdAsync(paidOrder.StoreId) ?? await _storeContext.GetCurrentStoreAsync();
        languageId = await EnsureLanguageIsActiveAsync(languageId, store.Id);

        var messageTemplates = await GetActiveMessageTemplatesAsync(CWMessageTemplateSystemNames.ReturnAuthorisedExceptedCustomer, store.Id);
        if (!messageTemplates.Any())
            return new List<int>();

        //tokens
        var commonTokens = new List<Token>();
        await _messageTokenProvider.AddOrderTokensAsync(commonTokens, paidOrder, languageId);
        await _messageTokenProvider.AddCustomerTokensAsync(commonTokens, returnRequest.CustomerId);
        await _messageTokenProvider.AddReturnRequestTokensAsync(commonTokens, returnRequest, await _orderService.GetOrderByIdAsync(orderItem.OrderId), orderItem, languageId);
        await _messageTokenProviderOverride.AddReturnPackNameTokensAsync(commonTokens, orderItem);

        return await messageTemplates.SelectAwait(async messageTemplate =>
        {
            //email account
            var emailAccount = await GetEmailAccountOfMessageTemplateAsync(messageTemplate, languageId);

            var tokens = new List<Token>(commonTokens);
            await _messageTokenProvider.AddStoreTokensAsync(tokens, store, emailAccount, languageId);

            //event notification
            await _eventPublisher.MessageTokensAddedAsync(messageTemplate, tokens);

            var billingAddress = await _addressService.GetAddressByIdAsync(paidOrder.BillingAddressId);

            var customer = await _customerService.GetCustomerByIdAsync(returnRequest.CustomerId);
            var toEmail = (await _customerService.IsGuestAsync(customer))
                ? billingAddress.Email
                : customer.Email;
            var toName = (await _customerService.IsGuestAsync(customer))
                ? billingAddress.FirstName
                : await _customerService.GetCustomerFullNameAsync(customer);

            return await SendNotificationAsync(messageTemplate, emailAccount, languageId, tokens, toEmail, toName);
        }).ToListAsync();
    }

    /// <summary>
    /// Sends a notification when a Return Request is approved but extra money is needed
    /// </summary>
    /// <param name="returnRequest">The return request</param>
    /// <param name="orderItem">The returned order item</param>
    /// <param name="pendingAmount">What the customer is asked to pay</param>
    /// <param name="returnRequestItem">The return request item as the caller holds it, or null to load it</param>
    /// <returns>A task that represents the asynchronous operation; the task result contains the ids of the queued e-mails</returns>
    public async Task<IList<int>> SendReturnRequestApprovedPaymentCustomerNotificationAsync(ReturnRequest returnRequest, OrderItem orderItem,
        decimal pendingAmount, ReturnRequestItem returnRequestItem = null)
    {
        ArgumentNullException.ThrowIfNull(returnRequest);

        var order = await _orderService.GetOrderByIdAsync(orderItem.OrderId);
        var store = await _storeService.GetStoreByIdAsync(order.StoreId) ?? await _storeContext.GetCurrentStoreAsync();
        int languageId = order.CustomerLanguageId;
        languageId = await EnsureLanguageIsActiveAsync(languageId, store.Id);

        var messageTemplates = await GetActiveMessageTemplatesAsync(CWMessageTemplateSystemNames.ReturnAuthorisedPaymentCustomer, store.Id);
        if (!messageTemplates.Any())
            return new List<int>();

        //tokens
        var commonTokens = new List<Token>();
        await _messageTokenProvider.AddOrderTokensAsync(commonTokens, order, languageId);
        await _messageTokenProvider.AddCustomerTokensAsync(commonTokens, returnRequest.CustomerId);
        await _messageTokenProvider.AddReturnRequestTokensAsync(commonTokens, returnRequest, order, orderItem, languageId);
        await _messageTokenProviderOverride.AddReturnPackNameTokensAsync(commonTokens, orderItem);

        await _messageTokenProviderOverride.AddExchangeReturnTokensAsync(commonTokens, returnRequest, pendingAmount, returnRequestItem);

        return await messageTemplates.SelectAwait(async messageTemplate =>
        {
            //email account
            var emailAccount = await GetEmailAccountOfMessageTemplateAsync(messageTemplate, languageId);

            var tokens = new List<Token>(commonTokens);
            await _messageTokenProvider.AddStoreTokensAsync(tokens, store, emailAccount, languageId);

            //event notification
            await _eventPublisher.MessageTokensAddedAsync(messageTemplate, tokens);

            var billingAddress = await _addressService.GetAddressByIdAsync(order.BillingAddressId);

            var customer = await _customerService.GetCustomerByIdAsync(returnRequest.CustomerId);
            var toEmail = (await _customerService.IsGuestAsync(customer))
                ? billingAddress.Email
                : customer.Email;
            var toName = (await _customerService.IsGuestAsync(customer))
                ? billingAddress.FirstName
                : await _customerService.GetCustomerFullNameAsync(customer);

            return await SendNotificationAsync(messageTemplate, emailAccount, languageId, tokens, toEmail, toName);
        }).ToListAsync();
    }

    #endregion
}