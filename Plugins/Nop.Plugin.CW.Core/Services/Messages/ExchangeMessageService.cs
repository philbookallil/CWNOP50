using Nop.Core;
using Nop.Core.Domain.Messages;
using Nop.Core.Domain.Orders;
using Nop.Core.Events;
using Nop.Plugin.CW.Core.Domain.Messages;
using Nop.Services.Localization;
using Nop.Services.Messages;
using Nop.Services.Orders;
using Nop.Services.Stores;

namespace Nop.Plugin.CW.Core.Services.Messages
{
    /// <summary>
    /// Service to deal with Messages and Notifications
    /// </summary>
    public class ExchangeMessageService : IExchangeMessageService
    {
        #region Fields

        protected readonly IMessageTokenProvider _messageTokenProvider;
        protected readonly IOrderService _orderService;
        protected readonly IStoreContext _storeContext;
        protected readonly IStoreService _storeService;
        protected readonly IWorkContext _workContext;
        protected readonly IEventPublisher _eventPublisher;
        protected readonly ILocalizationService _localizationService;
        protected readonly ITokenizer _tokenizer;
        protected readonly ILanguageService _languageService;
        protected readonly IMessageTemplateService _messageTemplateService;
        protected readonly IEmailAccountService _emailAccountService;
        protected readonly EmailAccountSettings _emailAccountSettings;

        #endregion

        #region Ctor

        public ExchangeMessageService(IMessageTokenProvider messageTokenProvider,
            IOrderService orderService,
            IStoreContext storeContext,
            IStoreService storeService,
            IWorkContext workContext,
            IEventPublisher eventPublisher,
            ILocalizationService localizationService,
            ITokenizer tokenizer,
            ILanguageService languageService,
            IMessageTemplateService messageTemplateService,
            IEmailAccountService emailAccountService,
            EmailAccountSettings emailAccountSettings)
        {
            _messageTokenProvider = messageTokenProvider;
            _orderService = orderService;
            _storeContext = storeContext;
            _storeService = storeService;
            _workContext = workContext;
            _eventPublisher = eventPublisher;
            _localizationService = localizationService;
            _tokenizer = tokenizer;
            _languageService = languageService;
            _messageTemplateService = messageTemplateService;
            _emailAccountService = emailAccountService;
            _emailAccountSettings = emailAccountSettings;
        }

        #endregion

        #region Utilities

        protected async Task<int> EnsureLanguageIsActiveAsync(int languageId, int storeId)
        {
            //load language by specified ID
            var language = await _languageService.GetLanguageByIdAsync(languageId);

            if (language == null || !language.Published)
            {
                //load any language from the specified store
                language = (await _languageService.GetAllLanguagesAsync(storeId: storeId)).FirstOrDefault();
            }

            if (language == null || !language.Published)
            {
                //load any language
                language = (await _languageService.GetAllLanguagesAsync()).FirstOrDefault();
            }

            if (language == null)
                throw new Exception("No active language could be loaded");

            return language.Id;
        }

        /// <summary>
        /// Gets the list of active message templates with the given name
        /// </summary>
        /// <param name="messageTemplateName"></param>
        /// <param name="storeId"></param>
        /// <returns></returns>
        protected async Task<IList<MessageTemplate>> GetActiveMessageTemplatesAsync(string messageTemplateName, int storeId)
        {
            //get message templates by the name
            var messageTemplates = await _messageTemplateService.GetMessageTemplatesByNameAsync(messageTemplateName, storeId);

            //no template found
            if (!messageTemplates?.Any() ?? true)
                return new List<MessageTemplate>();

            //filter active templates
            messageTemplates = messageTemplates.Where(messageTemplate => messageTemplate.IsActive).ToList();

            return messageTemplates;
        }

        /// <summary>
        /// Gets the email account that is the sender email for the given message template
        /// </summary>
        /// <param name="messageTemplate"></param>
        /// <param name="languageId"></param>
        /// <returns></returns>
        protected async Task<EmailAccount> GetEmailAccountOfMessageTemplateAsync(MessageTemplate messageTemplate, int languageId)
        {
            var emailAccountId = await _localizationService.GetLocalizedAsync(messageTemplate, mt => mt.EmailAccountId, languageId);

            //some 0 validation (for localizable "Email account" dropdownlist which saves 0 if "Standard" value is chosen)
            if (emailAccountId == 0)
                emailAccountId = messageTemplate.EmailAccountId;

            var emailAccount = (await _emailAccountService.GetEmailAccountByIdAsync(emailAccountId) ?? await _emailAccountService.GetEmailAccountByIdAsync(_emailAccountSettings.DefaultEmailAccountId)) ??
                               (await _emailAccountService.GetAllEmailAccountsAsync()).FirstOrDefault();

            return emailAccount;
        }

        #endregion

        #region Methods

        /// <summary>
        /// Gets the text of the message template that should be used when generating the Exchange/Returns receipt
        /// </summary>
        /// <param name="returnRequest"></param>
        /// <param name="storeId"></param>
        /// <param name="languageId"></param>
        /// <returns></returns>
        public async Task<string> GetExchangesReturnReceiptMessageAsync(ReturnRequest returnRequest)
        {
            var messageTemplates = await GetActiveMessageTemplatesAsync(CWMessageTemplateSystemNames.ReturnReceipt, returnRequest.StoreId);
            if (!messageTemplates.Any())
                return null;

            var messageTemplate = messageTemplates.First();

            var orderItem = await _orderService.GetOrderItemByIdAsync(returnRequest.OrderItemId);
            var order = await _orderService.GetOrderByIdAsync(orderItem.OrderId);
            var store = await _storeService.GetStoreByIdAsync(order.StoreId) ?? await _storeContext.GetCurrentStoreAsync();
            var languageId = await EnsureLanguageIsActiveAsync(order.CustomerLanguageId, store.Id);

            var emailAccount = await GetEmailAccountOfMessageTemplateAsync(messageTemplate, languageId);

            // tokens
            var commonTokens = new List<Token>();
            await _messageTokenProvider.AddOrderTokensAsync(commonTokens, order, languageId);
            await _messageTokenProvider.AddCustomerTokensAsync(commonTokens, returnRequest.CustomerId);
            await _messageTokenProvider.AddReturnRequestTokensAsync(commonTokens, returnRequest, order, orderItem, languageId);
            await _messageTokenProvider.AddStoreTokensAsync(commonTokens, store, emailAccount, languageId);

            // event notification
            await _eventPublisher.MessageTokensAddedAsync(messageTemplate, commonTokens);

            // message body parsing
            var messageBody = await _localizationService.GetLocalizedAsync(messageTemplate, mt => mt.Body, languageId);

            return _tokenizer.Replace(messageBody, commonTokens, true);
        }

        #endregion
    }
}
