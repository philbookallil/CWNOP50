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

namespace Nop.Plugin.CW.Core.Services.Messages
{
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

        #region Utilities

        private async Task<EmailAccount> GetDefaultEmailAccountAsync()
        {
            var emailAccount = await _emailAccountService.GetEmailAccountByIdAsync(_emailAccountSettings.DefaultEmailAccountId);
            emailAccount ??= (await _emailAccountService.GetAllEmailAccountsAsync()).FirstOrDefault();

            return emailAccount;
        }

        #endregion

        #region Methods

        /// <summary>
        /// Sends the custom Return Request Receipt message template
        /// </summary>
        /// <param name="returnRequest"></param>
        /// <param name="orderItem"></param>
        /// <param name="languageId"></param>
        /// <returns></returns>
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
        /// <param name="returnRequest"></param>
        /// <param name="orderItem"></param>
        /// <param name="languageId"></param>
        /// <returns></returns>
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
        /// <param name="returnRequest"></param>
        /// <param name="orderItem"></param>
        /// <param name="languageId"></param>
        /// <returns></returns>
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

        public async Task<int> SendPendingReturnRequestNotificationAsync(ReturnRequest returnRequest)
        {
            string subject = "Return Request requires further actions";
            string body = "A Return Request you sent has been approved but it requires additional actions in order to be completed. " +
                            "Please go to our store by following this link: {0}, login and go to your cart to complete the checkout";

            var emailAccount = await GetDefaultEmailAccountAsync();
            if (emailAccount == null)
                throw new NopException("Could not obtain the default System Email account");

            var customerId = returnRequest.CustomerId;
            var customer = await _customerService.GetCustomerByIdAsync(returnRequest.CustomerId);
            if (customer == null)
                throw new NopException("The Return Request related customer is null");

            var store = await _storeService.GetStoreByIdAsync(returnRequest.StoreId);
            if (store == null)
                throw new NopException("Could not determine the Store related to the Return Request");

            var email = new QueuedEmail
            {
                Priority = QueuedEmailPriority.High,
                From = emailAccount.Email,
                FromName = emailAccount.DisplayName,
                To = customer.Email,
                ToName = await _customerService.GetCustomerFullNameAsync(customer),
                CC = string.Empty,
                Subject = subject,
                Body = String.Format(body, store.Url),
                CreatedOnUtc = DateTime.UtcNow,
                EmailAccountId = emailAccount.Id
            };

            await _queuedEmailService.InsertQueuedEmailAsync(email);

            return email.Id;
        }

        #endregion
    }
}
