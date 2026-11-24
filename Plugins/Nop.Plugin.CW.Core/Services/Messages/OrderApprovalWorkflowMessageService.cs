using Nop.Core;
using Nop.Core.Domain.Common;
using Nop.Core.Domain.Directory;
using Nop.Core.Domain.Messages;
using Nop.Core.Domain.Orders;
using Nop.Core.Events;
using Nop.Plugin.CW.Core.Domain.Approvals;
using Nop.Plugin.CW.Core.Domain.Messages;
using Nop.Plugin.CW.Core.Services.Approvals;
using Nop.Services.Affiliates;
using Nop.Services.Catalog;
using Nop.Services.Common;
using Nop.Services.Customers;
using Nop.Services.Directory;
using Nop.Services.Localization;
using Nop.Services.Messages;
using Nop.Services.Orders;
using Nop.Services.Stores;

namespace Nop.Plugin.CW.Core.Services.Messages;

/// <summary>
/// The e-mails of the order approvals
/// </summary>
/// <remarks>
/// Built on nop's own workflow service so the template lookup, the e-mail account, the language fallback and the
/// queueing are nop's. An approver is not a customer: the request goes to the address the request recorded, in the
/// language of the order.
/// </remarks>
public class OrderApprovalWorkflowMessageService : WorkflowMessageService, IOrderApprovalWorkflowMessageService
{
    #region Fields

    protected readonly ICurrencyService _currencyService;
    protected readonly IPriceFormatter _priceFormatter;
    protected readonly CurrencySettings _currencySettings;

    #endregion

    #region Ctor

    public OrderApprovalWorkflowMessageService(CommonSettings commonSettings,
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
        ICurrencyService currencyService,
        IPriceFormatter priceFormatter,
        CurrencySettings currencySettings) : base(commonSettings,
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
        _currencyService = currencyService;
        _priceFormatter = priceFormatter;
        _currencySettings = currencySettings;
    }

    #endregion

    #region Utilities

    /// <summary>
    /// The tokens every approval e-mail has: the order, the customer who placed it, and the approval
    /// </summary>
    /// <param name="order">The order</param>
    /// <param name="approval">The order's approval</param>
    /// <param name="languageId">Message language identifier</param>
    protected virtual async Task<List<Token>> PrepareCommonTokensAsync(Order order, OrderApproval approval, int languageId)
    {
        var tokens = new List<Token>();
        await _messageTokenProvider.AddOrderTokensAsync(tokens, order, languageId);
        await _messageTokenProvider.AddCustomerTokensAsync(tokens, order.CustomerId);

        //the measured amount is held in the primary store currency, as the thresholds are
        var primaryCurrency = await _currencyService.GetCurrencyByIdAsync(_currencySettings.PrimaryStoreCurrencyId);
        tokens.Add(new Token("OrderApproval.Amount", await _priceFormatter.FormatPriceAsync(approval.MeasuredAmount, true, primaryCurrency, languageId, true, false)));
        tokens.Add(new Token("OrderApproval.Reason", approval.DecisionComment ?? string.Empty));
        tokens.Add(new Token("OrderApproval.DecidedBy", approval.DecidedBy ?? string.Empty));

        return tokens;
    }

    /// <summary>
    /// Queues one template to one address
    /// </summary>
    protected virtual async Task<IList<int>> SendAsync(string messageTemplateName, Order order, int languageId, List<Token> commonTokens, string toEmail, string toName)
    {
        if (string.IsNullOrWhiteSpace(toEmail))
            return new List<int>();

        var store = await _storeService.GetStoreByIdAsync(order.StoreId) ?? await _storeContext.GetCurrentStoreAsync();

        var messageTemplates = await GetActiveMessageTemplatesAsync(messageTemplateName, store.Id);
        if (!messageTemplates.Any())
            return new List<int>();

        return await messageTemplates.SelectAwait(async messageTemplate =>
        {
            var emailAccount = await GetEmailAccountOfMessageTemplateAsync(messageTemplate, languageId);

            var tokens = new List<Token>(commonTokens);
            await _messageTokenProvider.AddStoreTokensAsync(tokens, store, emailAccount, languageId);

            await _eventPublisher.MessageTokensAddedAsync(messageTemplate, tokens);

            return await SendNotificationAsync(messageTemplate, emailAccount, languageId, tokens, toEmail, toName);
        }).ToListAsync();
    }

    #endregion

    #region Methods

    public virtual async Task<IList<int>> SendApproverRequestAsync(Order order, OrderApproval approval, OrderApprovalRequest request, string token, bool reminder)
    {
        ArgumentNullException.ThrowIfNull(order);
        ArgumentNullException.ThrowIfNull(approval);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrEmpty(token);

        var store = await _storeService.GetStoreByIdAsync(order.StoreId) ?? await _storeContext.GetCurrentStoreAsync();
        var languageId = await EnsureLanguageIsActiveAsync(order.CustomerLanguageId, store.Id);

        var tokens = await PrepareCommonTokensAsync(order, approval, languageId);
        tokens.Add(new Token("OrderApproval.ApproverName", request.ApproverName));
        tokens.Add(new Token("OrderApproval.Link", $"{store.Url.TrimEnd('/')}/{OrderApprovalDefaults.ReviewPath}/{token}", true));

        var templateName = reminder
            ? CWMessageTemplateSystemNames.OrderApprovalApproverReminder
            : CWMessageTemplateSystemNames.OrderApprovalApproverRequest;

        return await SendAsync(templateName, order, languageId, tokens, request.ApproverEmail, request.ApproverName);
    }

    public virtual async Task<IList<int>> SendCustomerNotificationAsync(Order order, OrderApproval approval, string messageTemplateName, string approverNames)
    {
        ArgumentNullException.ThrowIfNull(order);
        ArgumentNullException.ThrowIfNull(approval);
        ArgumentException.ThrowIfNullOrEmpty(messageTemplateName);

        var customer = await _customerService.GetCustomerByIdAsync(order.CustomerId);
        if (customer == null)
            return new List<int>();

        var languageId = await EnsureLanguageIsActiveAsync(order.CustomerLanguageId, order.StoreId);

        var tokens = await PrepareCommonTokensAsync(order, approval, languageId);
        tokens.Add(new Token("OrderApproval.Approvers", approverNames ?? string.Empty));

        return await SendAsync(messageTemplateName, order, languageId, tokens, customer.Email, await _customerService.GetCustomerFullNameAsync(customer));
    }

    #endregion
}
