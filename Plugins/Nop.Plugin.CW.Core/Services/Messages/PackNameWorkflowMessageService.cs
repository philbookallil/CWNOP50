using Nop.Core;
using Nop.Core.Domain.Common;
using Nop.Core.Domain.Customers;
using Nop.Core.Domain.Directory;
using Nop.Core.Domain.Messages;
using Nop.Core.Events;
using Nop.Plugin.CW.Core.Domain.Messages;
using Nop.Plugin.CW.Core.Domain.PackNames;
using Nop.Plugin.CW.Core.Helpers;
using Nop.Plugin.CW.Core.Services.Subsidies;
using Nop.Services.Affiliates;
using Nop.Services.Catalog;
using Nop.Services.Common;
using Nop.Services.Customers;
using Nop.Services.Directory;
using Nop.Services.Helpers;
using Nop.Services.Localization;
using Nop.Services.Messages;
using Nop.Services.Orders;
using Nop.Services.Stores;
using System.Net;

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
    protected readonly ISubsidyReportingService _subsidyReportingService;
    protected readonly IDateTimeHelper _dateTimeHelper;
    protected readonly IPriceFormatter _priceFormatter;
    protected readonly ICurrencyService _currencyService;
    protected readonly CurrencySettings _currencySettings;

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
        IGenericAttributeService genericAttributeService,
        ISubsidyReportingService subsidyReportingService,
        IDateTimeHelper dateTimeHelper,
        IPriceFormatter priceFormatter,
        ICurrencyService currencyService,
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
        _genericAttributeService = genericAttributeService;
        _subsidyReportingService = subsidyReportingService;
        _dateTimeHelper = dateTimeHelper;
        _priceFormatter = priceFormatter;
        _currencyService = currencyService;
        _currencySettings = currencySettings;
    }

    #endregion

    #region Utilities

    /// <summary>
    /// The subsidies of a packname as the welcome e-mail lists them: a title, then one line per live assignment with its
    /// current season, its credit and its items; empty when the packname has none.
    /// </summary>
    /// <param name="packName">The packname</param>
    /// <param name="languageId">Message language identifier</param>
    /// <returns>A task that represents the asynchronous operation; the task result contains the HTML, encoded</returns>
    protected virtual async Task<string> PrepareSubsidyDetailsAsync(PackName packName, int languageId)
    {
        var assignments = (await _subsidyReportingService.GetSubsidyDetailsByPackNameIdAsync(packName.Id, inOrderingPeriodOnly: false))
            .Where(assignment => assignment.Active && assignment.AssignmentActive)
            .ToList();

        if (!assignments.Any())
            return string.Empty;

        //the season is judged on the store's calendar day, the frame the periods are expressed in
        var today = SubsidyPeriodHelper.ToStoreLocal(DateTime.UtcNow, _dateTimeHelper.DefaultStoreTimeZone).Date;

        var periodFormat = await _localizationService.GetResourceAsync("Plugins.CW.Core.Messages.Subsidy.Details.Period", languageId);
        var creditFormat = await _localizationService.GetResourceAsync("Plugins.CW.Core.Messages.Subsidy.Details.Credit", languageId);
        var unitsFormat = await _localizationService.GetResourceAsync("Plugins.CW.Core.Messages.Subsidy.Details.Units", languageId);

        //credit is held in the primary store currency, the customer's e-mail states it in that currency
        var primaryCurrency = await _currencyService.GetCurrencyByIdAsync(_currencySettings.PrimaryStoreCurrencyId);

        var lines = new List<string>();

        foreach (var assignment in assignments)
        {
            var (seasonStart, seasonEnd) = CustomerSubsidyHelper.SeasonFor(assignment.DurationStart, assignment.DurationEnd, assignment.AnnualRecurrence, today);

            var productGroups = await _subsidyReportingService.GetSubsidyPackNameProductGroupDetailsAsync(assignment.Id, assignment.SubsidyId, activeOnly: true);
            var units = productGroups.Sum(productGroup => productGroup.Quantity + productGroup.TopupQuantity);
            //what the packname can spend: the subsidy's own credit and every product group's, each with its top-ups
            var credit = assignment.Credit + assignment.TopupCredit + productGroups.Sum(productGroup => productGroup.Credit + productGroup.TopupCredit);

            lines.Add(CustomerSubsidyHelper.DetailsLine(
                string.Format(periodFormat, WebUtility.HtmlEncode(assignment.Subsidy), seasonStart.ToShortDateString(), seasonEnd.ToShortDateString()),
                credit > decimal.Zero ? string.Format(creditFormat, WebUtility.HtmlEncode(await _priceFormatter.FormatPriceAsync(credit, true, primaryCurrency))) : null,
                units > 0 ? string.Format(unitsFormat, units) : null));
        }

        var title = await _localizationService.GetResourceAsync("Plugins.CW.Core.Messages.Subsidy.Details.Title", languageId);

        return $"{WebUtility.HtmlEncode(title)}<br />{string.Join("<br />", lines)}<br />";
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
        var subsidyDetails = string.Empty;

        if (subsidyNames?.Count > 0)
        {
            //the e-mail is sent inside the registration's transaction: a mistyped details resource must not undo the registration
            try
            {
                subsidyDetails = await PrepareSubsidyDetailsAsync(packName, languageId);
            }
            catch (FormatException)
            {
                subsidyDetails = string.Empty;
            }
        }

        commonTokens.Add(new Token("Subsidy.Details", subsidyDetails, true));

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