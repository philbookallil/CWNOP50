using System.Globalization;
using Microsoft.AspNetCore.Http;
using Nop.Core;
using Nop.Core.Domain.Customers;
using Nop.Core.Domain.Directory;
using Nop.Core.Domain.Logging;
using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Payments;
using Nop.Plugin.Payments.NABGateway.Components;
using Nop.Plugin.Payments.NABGateway.Domain;
using Nop.Plugin.Payments.NABGateway.Helpers;
using Nop.Plugin.Payments.NABGateway.Models;
using Nop.Plugin.Payments.NABGateway.Models.Request;
using Nop.Plugin.Payments.NABGateway.Services;
using Nop.Plugin.Payments.NABGateway.Validators;
using Nop.Services.Configuration;
using Nop.Services.Customers;
using Nop.Services.Directory;
using Nop.Services.Localization;
using Nop.Services.Helpers;
using Nop.Services.Logging;
using Nop.Services.Payments;
using Nop.Services.Plugins;

namespace Nop.Plugin.Payments.NABGateway;

/// <summary>
/// NAB Transact XML payment processor for nopCommerce 5.00.
/// </summary>
public class NABXMLPaymentProcessor : BasePlugin, IPaymentMethod
{
    #region Fields

    protected readonly ICustomerService _customerService;
    protected readonly ICurrencyService _currencyService;
    protected readonly ILocalizationService _localizationService;
    protected readonly ILogger _logger;
    protected readonly ISettingService _settingService;
    protected readonly IStoreContext _storeContext;
    protected readonly IWebHelper _webHelper;
    protected readonly IWorkContext _workContext;
    protected readonly NABXMLGatewayClient _gatewayClient;
    protected readonly NABXMLPaymentSettings _settings;
    protected readonly CurrencySettings _currencySettings;
    protected readonly INABXMLCardDataStore _cardDataStore;
    protected readonly INABXMLTransactionService _transactionService;

    #endregion

    #region Ctor

    public NABXMLPaymentProcessor(ICustomerService customerService,
        ICurrencyService currencyService,
        ILocalizationService localizationService,
        ILogger logger,
        ISettingService settingService,
        IStoreContext storeContext,
        IWebHelper webHelper,
        IWorkContext workContext,
        NABXMLGatewayClient gatewayClient,
        NABXMLPaymentSettings settings,
        CurrencySettings currencySettings,
        INABXMLCardDataStore cardDataStore,
        INABXMLTransactionService transactionService)
    {
        _customerService = customerService;
        _currencyService = currencyService;
        _localizationService = localizationService;
        _logger = logger;
        _settingService = settingService;
        _storeContext = storeContext;
        _webHelper = webHelper;
        _workContext = workContext;
        _gatewayClient = gatewayClient;
        _settings = settings;
        _currencySettings = currencySettings;
        _cardDataStore = cardDataStore;
        _transactionService = transactionService;
    }

    #endregion

    #region Utilities

    /// <summary>
    /// Generates a description for the NAB transaction. This is shown on the gateway side.
    /// </summary>
    protected virtual async Task<string> GeneratePaymentRequestDescriptionAsync(ProcessPaymentRequest paymentRequest)
    {
        var store = await _storeContext.GetCurrentStoreAsync();

        // TODO: branch enrichment.
        // The 4.1 implementation called IBranchService.GetBranchForStaff(customerId) (CW.Core) to append
        // the staff member's branch name to the description. That coupling has been intentionally dropped
        // in this initial 4.90 migration. Re-introduce by coupling to CW.Core (DLL ref / Pattern C) once
        // a cross-plugin dependency on CW.Core's IBranchService is approved.

        return $"Uniforms Order on {store.Name}";
    }

    /// <summary>
    /// Generates the order reference for the NAB transaction (used as <c>purchaseOrderNo</c>).
    /// </summary>
    protected virtual async Task<string> GenerateOrderReferenceAsync(ProcessPaymentRequest paymentRequest)
    {
        var store = await _storeContext.GetCurrentStoreAsync();
        var storeNameToken = BuildNameToken(store.Name);

        // Default to the customer Id if we can't resolve anything more meaningful.
        var customerNameToken = paymentRequest.CustomerId.ToString();
        var customer = await _customerService.GetCustomerByIdAsync(paymentRequest.CustomerId);
        if (customer != null)
        {
            var fullName = await _customerService.GetCustomerFullNameAsync(customer);
            if (!string.IsNullOrWhiteSpace(fullName))
            {
                customerNameToken = fullName;
            }
            else
            {
                var email = await ResolveEmailAsync(customer);
                if (!string.IsNullOrEmpty(email))
                {
                    var atIndex = email.IndexOf('@');
                    customerNameToken = atIndex > 0 ? email[..atIndex] : email;
                }
            }
        }

        // TODO: branch enrichment (see GeneratePaymentRequestDescriptionAsync).
        return $"{storeNameToken}-{BuildNameToken(customerNameToken, 15)}";
    }

    /// <summary>
    /// Returns the customer's email, falling back to the billing address when not set on the customer.
    /// </summary>
    protected virtual async Task<string> ResolveEmailAsync(Customer customer)
    {
        if (!string.IsNullOrEmpty(customer.Email))
            return customer.Email;

        var billing = await _customerService.GetCustomerBillingAddressAsync(customer);
        return billing?.Email;
    }

    /// <summary>
    /// Builds a sanitised, length-capped name token. Mirrors the 4.1 behaviour: take the first <paramref name="maxNameLength"/>
    /// characters, trim trailing punctuation, replace inner spaces with dashes.
    /// </summary>
    internal static string BuildNameToken(string name, int maxNameLength = 10)
    {
        if (string.IsNullOrEmpty(name))
            return string.Empty;

        var token = (name.Length < maxNameLength ? name : name[..maxNameLength]).Trim();

        var endingCharsToRemove = new[] { '-', '.' };
        while (token.Length > 0 && endingCharsToRemove.Contains(token[^1]))
            token = token[..^1];

        return token.Replace(' ', '-');
    }

    /// <summary>
    /// Builds the NAB Transact request payload from a nopCommerce <see cref="ProcessPaymentRequest"/>.
    /// </summary>
    protected virtual async Task<NABTransactMessage> BuildRequestAsync(ProcessPaymentRequest paymentRequest, NABXMLCardData card)
    {
        var transactionId = Guid.NewGuid().ToString("N");
        var timestamp = DateTime.UtcNow.ToString("yyyyddMMHHmmssfff", CultureInfo.InvariantCulture) + "000000";

        var currency = await _currencyService.GetCurrencyByIdAsync(_currencySettings.PrimaryStoreCurrencyId);
        var currencyCode = currency?.CurrencyCode ?? "AUD";

        var orderAmount = Math.Floor(paymentRequest.OrderTotal * 100m)
            .ToString("0", CultureInfo.InvariantCulture);

        var expiryMonth = card.ExpireMonth.ToString("D2", CultureInfo.InvariantCulture);
        var expiryYear = card.ExpireYear.ToString(CultureInfo.InvariantCulture);

        return new NABTransactMessage
        {
            MessageInfo = new NABTransactMessageMessageInfo
            {
                messageID = transactionId,
                messageTimestamp = timestamp,
                apiVersion = NABXMLPaymentDefaults.ApiVersion,
                timeoutValue = NABXMLPaymentDefaults.GatewayTimeoutSeconds
            },
            MerchantInfo = new NABTransactMessageMerchantInfo
            {
                merchantID = _settings.MerchantId,
                password = _settings.Password
            },
            RequestType = "Payment",
            Payment = new NABTransactMessagePayment
            {
                TxnList = new NABTransactMessagePaymentTxnList
                {
                    count = 1,
                    Txn = new NABTransactMessagePaymentTxnListTxn
                    {
                        ID = "1",
                        txnType = "0",
                        txnSource = "23",
                        amount = orderAmount,
                        currency = currencyCode,
                        purchaseOrderNo = await GenerateOrderReferenceAsync(paymentRequest),
                        CreditCardInfo = new NABTransactMessagePaymentTxnListTxnCreditCardInfo
                        {
                            cardNumber = card.Number,
                            expiryDate = $"{expiryMonth}/{expiryYear}",
                            cardHolderName = card.HolderName,
                            cvv = card.Cvv,
                            recurringflag = "no"
                        }
                    }
                }
            }
        };
    }

    /// <summary>
    /// Persists a NAB transaction record so it surfaces in the admin Payments / Failed Transactions reports.
    /// Persistence failures must never break the checkout flow, so they are swallowed (and logged).
    /// </summary>
    protected virtual async Task PersistTransactionAsync(ProcessPaymentRequest paymentRequest,
        Models.Response.NABTransactMessage response,
        string rawResponse,
        bool approved)
    {
        try
        {
            var txn = response?.Payment?.TxnList?.Txn;

            string userEmail = null;
            string userFullName = null;
            var customer = await _customerService.GetCustomerByIdAsync(paymentRequest.CustomerId);
            if (customer != null)
            {
                userEmail = await ResolveEmailAsync(customer);
                userFullName = await _customerService.GetCustomerFullNameAsync(customer);
            }

            var record = new NABXMLTransaction
            {
                OrderGuid = paymentRequest.OrderGuid,
                CustomerId = paymentRequest.CustomerId,
                UserEmail = userEmail,
                UserFullName = userFullName,
                Approved = approved,
                MessageId = response?.MessageInfo?.messageID,
                TxnId = txn?.txnID,
                AuthId = txn?.authID,
                ResponseCode = txn?.responseCode ?? response?.Status?.statusCode,
                ResponseText = txn?.responseText ?? response?.Status?.statusDescription,
                SettlementDate = txn?.settlementDate,
                MaskedPan = txn?.CreditCardInfo?.pan,
                CardType = txn?.CreditCardInfo?.cardType ?? txn?.CreditCardInfo?.cardDescription,
                Amount = paymentRequest.OrderTotal,
                CurrencyCode = txn?.currency,
                PurchaseOrderNo = txn?.purchaseOrderNo,
                RawResponseXml = rawResponse,
                CreatedOnUtc = DateTime.UtcNow
            };

            await _transactionService.InsertTransactionAsync(record);
        }
        catch (Exception ex)
        {
            await _logger.InsertLogAsync(LogLevel.Error,
                $"NAB XML Payment: failed to persist transaction record. Order GUID: {paymentRequest.OrderGuid}",
                ex.ToString());
        }
    }

    #endregion

    #region IPaymentMethod methods

    /// <summary>
    /// Process a payment by submitting the credit card details to the NAB Transact XML gateway.
    /// </summary>
    public async Task<ProcessPaymentResult> ProcessPaymentAsync(ProcessPaymentRequest processPaymentRequest)
    {
        ArgumentNullException.ThrowIfNull(processPaymentRequest);

        var result = new ProcessPaymentResult();

        //the card is used once: taken here, whatever the gateway answers
        var card = _cardDataStore.Take(processPaymentRequest.StoreId, processPaymentRequest.CustomerId);
        if (card is null)
        {
            result.AddError(await _localizationService.GetResourceAsync("Plugins.Payments.NABGateway.Payment.CardNotAvailable"));
            return result;
        }

        try
        {
            var request = await BuildRequestAsync(processPaymentRequest, card);
            var description = await GeneratePaymentRequestDescriptionAsync(processPaymentRequest);

            // Description is logged with the call for traceability; NAB doesn't have a dedicated description field in the txn block.
            var endpointUrl = (_settings.GatewayURL?.TrimEnd('/') ?? string.Empty) + NABXMLPaymentDefaults.PaymentEndpointPath;
            var (response, rawResponse) = await _gatewayClient.SendPaymentAsync(request, endpointUrl);

            var txn = response?.Payment?.TxnList?.Txn;
            if (response?.Status?.statusCode == "000"
                && txn != null
                && string.Equals(txn.approved, "Yes", StringComparison.OrdinalIgnoreCase))
            {
                await _logger.InsertLogAsync(LogLevel.Information,
                    $"NAB XML Payment success. Order GUID: {processPaymentRequest.OrderGuid}. {description}",
                    rawResponse);

                await PersistTransactionAsync(processPaymentRequest, response, rawResponse, approved: true);

                result.CaptureTransactionId = response.MessageInfo?.messageID;
                result.CaptureTransactionResult = txn.responseText;
                result.AuthorizationTransactionCode = txn.authID;
                result.NewPaymentStatus = PaymentStatus.Paid;
            }
            else
            {
                await _logger.InsertLogAsync(LogLevel.Warning,
                    $"NAB XML Payment failed. Order GUID: {processPaymentRequest.OrderGuid}. {description}",
                    rawResponse);

                await PersistTransactionAsync(processPaymentRequest, response, rawResponse, approved: false);

                var gatewayReason = txn?.responseText
                    ?? response?.Status?.statusDescription
                    ?? "Unknown gateway error";
                result.AddError($"Order payment failed. Payment Gateway reason: {gatewayReason}");
            }
        }
        catch (Exception ex)
        {
            await _logger.InsertLogAsync(LogLevel.Error,
                $"NAB XML Payment exception. Order GUID: {processPaymentRequest.OrderGuid}",
                ex.ToString());

            await PersistTransactionAsync(processPaymentRequest, response: null, rawResponse: ex.ToString(), approved: false);

            throw new NopException("Error performing Order payment", ex);
        }

        return result;
    }

    /// <summary>
    /// Post process payment — not used for NAB (no redirect required).
    /// </summary>
    public Task PostProcessPaymentAsync(PostProcessPaymentRequest postProcessPaymentRequest)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// Hide the payment method during checkout when no logic dictates otherwise.
    /// </summary>
    public Task<bool> HidePaymentMethodAsync(IList<ShoppingCartItem> cart)
    {
        return Task.FromResult(false);
    }

    /// <summary>
    /// No additional handling fee for NAB.
    /// </summary>
    public Task<decimal> GetAdditionalHandlingFeeAsync(IList<ShoppingCartItem> cart)
    {
        return Task.FromResult(decimal.Zero);
    }

    /// <summary>
    /// Capture is not supported — NAB transactions are captured at process time.
    /// </summary>
    public Task<CapturePaymentResult> CaptureAsync(CapturePaymentRequest capturePaymentRequest)
    {
        return Task.FromResult(new CapturePaymentResult { Errors = new[] { "Capture method not supported" } });
    }

    /// <summary>
    /// Refund is not supported.
    /// </summary>
    public Task<RefundPaymentResult> RefundAsync(RefundPaymentRequest refundPaymentRequest)
    {
        return Task.FromResult(new RefundPaymentResult { Errors = new[] { "Refund method not supported" } });
    }

    /// <summary>
    /// Void is not supported.
    /// </summary>
    public Task<VoidPaymentResult> VoidAsync(VoidPaymentRequest voidPaymentRequest)
    {
        return Task.FromResult(new VoidPaymentResult { Errors = new[] { "Void method not supported" } });
    }

    /// <summary>
    /// Recurring payments are not supported.
    /// </summary>
    public Task<ProcessPaymentResult> ProcessRecurringPaymentAsync(ProcessPaymentRequest processPaymentRequest)
    {
        return Task.FromResult(new ProcessPaymentResult { Errors = new[] { "Recurring payment not supported" } });
    }

    /// <summary>
    /// Cancelling a recurring payment is not supported.
    /// </summary>
    public Task<CancelRecurringPaymentResult> CancelRecurringPaymentAsync(CancelRecurringPaymentRequest cancelPaymentRequest)
    {
        return Task.FromResult(new CancelRecurringPaymentResult { Errors = new[] { "Recurring payment not supported" } });
    }

    /// <summary>
    /// Allow re-posting the payment after at least 5 seconds, mirroring the 4.1 behaviour.
    /// </summary>
    public Task<bool> CanRePostProcessPaymentAsync(Order order)
    {
        ArgumentNullException.ThrowIfNull(order);

        return Task.FromResult((DateTime.UtcNow - order.CreatedOnUtc).TotalSeconds >= 5);
    }

    /// <summary>
    /// Validate the public payment form using <see cref="PaymentInfoValidator"/>.
    /// </summary>
    public Task<IList<string>> ValidatePaymentFormAsync(IFormCollection form)
    {
        ArgumentNullException.ThrowIfNull(form);

        var warnings = new List<string>();

        var validator = new PaymentInfoValidator(_localizationService);
        var model = new PaymentInfoModel
        {
            CardNumber = CardNumberHelper.Sanitize(form["CardNumber"]),
            CardCode = form["CardCode"],
            ExpireMonth = form["ExpireMonth"],
            ExpireYear = form["ExpireYear"],
            CardholderName = form["CardholderName"]
        };

        var validationResult = validator.Validate(model);
        if (!validationResult.IsValid)
            warnings.AddRange(validationResult.Errors.Select(error => error.ErrorMessage));

        return Task.FromResult<IList<string>>(warnings);
    }

    /// <summary>
    /// Read the public payment form. The card goes to <see cref="INABXMLCardDataStore"/>, not onto the returned
    /// request: nop saves that request as JSON in the database until the order is confirmed.
    /// </summary>
    public async Task<ProcessPaymentRequest> GetPaymentInfoAsync(IFormCollection form)
    {
        ArgumentNullException.ThrowIfNull(form);

        var store = await _storeContext.GetCurrentStoreAsync();
        var customer = await _workContext.GetCurrentCustomerAsync();

        _cardDataStore.Save(store.Id, customer.Id, new NABXMLCardData(
            Number: CardNumberHelper.Sanitize(form["CardNumber"]),
            HolderName: form["CardholderName"],
            ExpireMonth: int.Parse(form["ExpireMonth"], CultureInfo.InvariantCulture),
            ExpireYear: int.Parse(form["ExpireYear"], CultureInfo.InvariantCulture),
            Cvv: form["CardCode"]));

        return new ProcessPaymentRequest();
    }

    /// <summary>
    /// URL to the plugin's admin configuration page.
    /// </summary>
    public override string GetConfigurationPageUrl()
    {
        return $"{_webHelper.GetStoreLocation()}Admin/NABXMLPayment/Configure";
    }

    /// <summary>
    /// View component type for the public "payment info" checkout step.
    /// </summary>
    public Type GetPublicViewComponent()
    {
        return typeof(NABXMLPaymentViewComponent);
    }

    /// <summary>
    /// Localised description displayed on the checkout "payment method" step.
    /// </summary>
    public async Task<string> GetPaymentMethodDescriptionAsync()
    {
        return await _localizationService.GetResourceAsync("Plugins.Payments.NABGateway.PaymentMethodDescription");
    }

    #endregion

    #region BasePlugin lifecycle

    /// <summary>
    /// Install the plugin: persist default settings and seed locale resources.
    /// </summary>
    public override async Task InstallAsync()
    {
        await _settingService.SaveSettingAsync(new NABXMLPaymentSettings
        {
            GatewayURL = string.Empty,
            MerchantId = string.Empty,
            Password = string.Empty
        });

        await AddOrUpdateLocaleResourcesAsync();

        await base.InstallAsync();
    }

    /// <summary>
    /// Update the plugin. Refreshes locale resources when the target version includes a
    /// <c>DATA</c> marker (same convention as <c>OdooIntegrationPlugin</c>).
    /// </summary>
    /// <param name="currentVersion">Current installed version.</param>
    /// <param name="targetVersion">Target version being upgraded to.</param>
    public override async Task UpdateAsync(string currentVersion, string targetVersion)
    {
        if (targetVersion.Contains("DATA"))
            await AddOrUpdateLocaleResourcesAsync();

        await base.UpdateAsync(currentVersion, targetVersion);
    }

    /// <summary>
    /// Uninstall the plugin: remove settings and locale resources.
    /// </summary>
    public override async Task UninstallAsync()
    {
        await _settingService.DeleteSettingAsync<NABXMLPaymentSettings>();
        await _localizationService.DeleteLocaleResourcesAsync("Plugins.Payments.NABGateway");

        await base.UninstallAsync();
    }

    private async Task AddOrUpdateLocaleResourcesAsync()
    {
        await _localizationService.AddOrUpdateLocaleResourceAsync(new Dictionary<string, string>
        {
            ["Plugins.Payments.NABGateway.Fields.GatewayURL"] = "Gateway URL",
            ["Plugins.Payments.NABGateway.Fields.GatewayURL.Hint"] = "The base URL of the NAB Transact XML API (e.g. https://transact.nab.com.au/live).",
            ["Plugins.Payments.NABGateway.Fields.MerchantId"] = "Merchant ID",
            ["Plugins.Payments.NABGateway.Fields.MerchantId.Hint"] = "The merchant identifier issued by NAB.",
            ["Plugins.Payments.NABGateway.Fields.Password"] = "Password",
            ["Plugins.Payments.NABGateway.Fields.Password.Hint"] = "The gateway password associated with the merchant identifier.",
            ["Plugins.Payments.NABGateway.Fields.DebugMode"] = "Debug mode",
            ["Plugins.Payments.NABGateway.Fields.DebugMode.Hint"] = "When enabled, the request payload (card number and CVV masked), endpoint and raw gateway response are written to the system log at Debug level. Use for troubleshooting only.",
            ["Plugins.Payments.NABGateway.Fields.IgnoreSslErrors"] = "Ignore SSL errors",
            ["Plugins.Payments.NABGateway.Fields.IgnoreSslErrors.Hint"] = "When enabled, server SSL/TLS certificate validation errors are ignored when contacting the gateway. For test/sandbox endpoints only — never enable in production.",
            ["Plugins.Payments.NABGateway.PaymentMethodDescription"] = "Pay with credit / debit card",

            //payment form (nop 5.00 no longer ships the Payment.Card* resources)
            ["Plugins.Payments.NABGateway.Payment.CardholderName"] = "Cardholder name",
            ["Plugins.Payments.NABGateway.Payment.CardholderName.Required"] = "Enter cardholder name",
            ["Plugins.Payments.NABGateway.Payment.CardNumber"] = "Card number",
            ["Plugins.Payments.NABGateway.Payment.CardNumber.Required"] = "Enter card number",
            ["Plugins.Payments.NABGateway.Payment.CardNumber.Wrong"] = "Wrong card number",
            ["Plugins.Payments.NABGateway.Payment.CardCode"] = "Card code",
            ["Plugins.Payments.NABGateway.Payment.CardCode.Wrong"] = "Wrong card code",
            ["Plugins.Payments.NABGateway.Payment.ExpirationDate"] = "Expiration date",
            ["Plugins.Payments.NABGateway.Payment.ExpirationDate.Expired"] = "Card is expired",
            ["Plugins.Payments.NABGateway.Payment.ExpireMonth.Required"] = "Expire month is required",
            ["Plugins.Payments.NABGateway.Payment.ExpireYear.Required"] = "Expire year is required",
            ["Plugins.Payments.NABGateway.Payment.CardNotAvailable"] = "Your card details are no longer available. Please go back to the payment step and enter your card again.",

            //admin menu
            ["Plugins.Payments.NABGateway.Reports.Menu"] = "NAB Payments",
            ["Plugins.Payments.NABGateway.Reports.Payments.Menu"] = "Order Payments",
            ["Plugins.Payments.NABGateway.Reports.Failed.Menu"] = "Failed Transactions",

            //report pages
            ["Plugins.Payments.NABGateway.Reports.Payments.Title"] = "NAB Payments for Orders",
            ["Plugins.Payments.NABGateway.Reports.Failed.Title"] = "NAB Failed Transactions",
            ["Plugins.Payments.NABGateway.Reports.Details.Title"] = "Transaction Details",
            ["Plugins.Payments.NABGateway.Reports.Details.Close"] = "Close",

            //report fields
            ["Plugins.Payments.NABGateway.Reports.Fields.StartDate"] = "Start Date",
            ["Plugins.Payments.NABGateway.Reports.Fields.EndDate"] = "End Date",
            ["Plugins.Payments.NABGateway.Reports.Fields.UserEmail"] = "User Email",
            ["Plugins.Payments.NABGateway.Reports.Fields.UserName"] = "User Name",
            ["Plugins.Payments.NABGateway.Reports.Fields.OrderId"] = "Order Id",
            ["Plugins.Payments.NABGateway.Reports.Fields.OrderGuid"] = "Order GUID",
            ["Plugins.Payments.NABGateway.Reports.Fields.Approved"] = "Approved",
            ["Plugins.Payments.NABGateway.Reports.Fields.MessageId"] = "Message Id",
            ["Plugins.Payments.NABGateway.Reports.Fields.TxnId"] = "Transaction Id",
            ["Plugins.Payments.NABGateway.Reports.Fields.AuthId"] = "Authorization Id",
            ["Plugins.Payments.NABGateway.Reports.Fields.ResponseCode"] = "Response Code",
            ["Plugins.Payments.NABGateway.Reports.Fields.ResponseText"] = "Response Text",
            ["Plugins.Payments.NABGateway.Reports.Fields.SettlementDate"] = "Settlement Date",
            ["Plugins.Payments.NABGateway.Reports.Fields.MaskedPan"] = "Card Number",
            ["Plugins.Payments.NABGateway.Reports.Fields.CardType"] = "Card Type",
            ["Plugins.Payments.NABGateway.Reports.Fields.Amount"] = "Amount",
            ["Plugins.Payments.NABGateway.Reports.Fields.PurchaseOrderNo"] = "Purchase Order No",
            ["Plugins.Payments.NABGateway.Reports.Fields.RawResponse"] = "Raw Response",
            ["Plugins.Payments.NABGateway.Reports.Fields.CreatedOn"] = "Created On"
        });
    }

    #endregion

    #region IPaymentMethod properties

    public bool SupportCapture => false;

    public bool SupportPartiallyRefund => false;

    public bool SupportRefund => false;

    public bool SupportVoid => false;

    public RecurringPaymentType RecurringPaymentType => RecurringPaymentType.NotSupported;

    public PaymentMethodType PaymentMethodType => PaymentMethodType.Standard;

    public bool SkipPaymentInfo => false;

    #endregion
}
