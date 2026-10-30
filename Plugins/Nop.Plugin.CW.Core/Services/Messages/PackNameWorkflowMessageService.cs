using Nop.Core;
using Nop.Core.Domain.Common;
using Nop.Core.Domain.Customers;
using Nop.Core.Domain.Messages;
using Nop.Core.Events;
using Nop.Plugin.CW.Core.Domain.Messages;
using Nop.Plugin.CW.Core.Domain.PackNames;
using Nop.Services.Affiliates;
using Nop.Services.Catalog;
using Nop.Services.Common;
using Nop.Services.Customers;
using Nop.Services.Localization;
using Nop.Services.Messages;
using Nop.Services.Orders;
using Nop.Services.Stores;

namespace Nop.Plugin.CW.Core.Services.Messages;

/// <summary>
/// The e-mails a packname customer receives from the packname flows.
/// </summary>
/// <remarks>
/// Built on nop's own workflow service so the template lookup, the e-mail account, the language fallback and
/// the queueing are nop's. The welcome e-mail carries nop's password-recovery link: a customer registered by a
/// manager never receives a password, they set their own from the e-mail, within the days
/// <c>CustomerSettings.PasswordRecoveryLinkDaysValid</c> allows.
/// </remarks>
public class PackNameWorkflowMessageService : WorkflowMessageService, IPackNameWorkflowMessageService
{
    #region Fields

    protected readonly IGenericAttributeService _genericAttributeService;

    #endregion

    #region Ctor

    public PackNameWorkflowMessageService(CommonSettings commonSettings,
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
        IGenericAttributeService genericAttributeService) : base(commonSettings,
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
        _genericAttributeService = genericAttributeService;
    }

    #endregion

    #region Methods

    public virtual async Task<IList<int>> SendCustomerRegisteredByManagerMessageAsync(Customer customer, PackName packName, string branchName, IList<string> subsidyNames, int storeId, int languageId)
    {
        ArgumentNullException.ThrowIfNull(customer);
        ArgumentNullException.ThrowIfNull(packName);

        var store = await _storeService.GetStoreByIdAsync(storeId) ?? await _storeContext.GetCurrentStoreAsync();
        languageId = await EnsureLanguageIsActiveAsync(languageId, store.Id);

        var messageTemplates = await GetActiveMessageTemplatesAsync(CWMessageTemplateSystemNames.CustomerRegistrationImport, store.Id);
        if (!messageTemplates.Any())
            return new List<int>();

        //the set-password link in the e-mail is nop's password-recovery link, so the token it reads is written here,
        //the way nop's own password-recovery form writes it
        await _genericAttributeService.SaveAttributeAsync(customer, NopCustomerDefaults.PasswordRecoveryTokenAttribute, Guid.NewGuid().ToString());
        await _genericAttributeService.SaveAttributeAsync(customer, NopCustomerDefaults.PasswordRecoveryTokenDateGeneratedAttribute, (DateTime?)DateTime.UtcNow);

        //tokens
        var commonTokens = new List<Token>();
        await _messageTokenProvider.AddCustomerTokensAsync(commonTokens, customer);

        commonTokens.Add(new Token("PackName.Name", packName.Name));
        commonTokens.Add(new Token("PackName.EmployeeNumber", packName.EmployeeNumber ?? string.Empty));
        commonTokens.Add(new Token("PackName.Branch", branchName ?? string.Empty));
        commonTokens.Add(new Token("Subsidy.SubsidyName", string.Join(", ", subsidyNames ?? new List<string>())));

        return await messageTemplates.SelectAwait(async messageTemplate =>
        {
            //email account
            var emailAccount = await GetEmailAccountOfMessageTemplateAsync(messageTemplate, languageId);

            var tokens = new List<Token>(commonTokens);
            await _messageTokenProvider.AddStoreTokensAsync(tokens, store, emailAccount, languageId);

            //event notification
            await _eventPublisher.MessageTokensAddedAsync(messageTemplate, tokens);

            var toEmail = customer.Email;
            var toName = await _customerService.GetCustomerFullNameAsync(customer);

            return await SendNotificationAsync(messageTemplate, emailAccount, languageId, tokens, toEmail, toName);
        }).ToListAsync();
    }

    #endregion
}