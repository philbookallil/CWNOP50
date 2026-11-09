namespace Nop.Plugin.CW.Core.Domain.Messages;

/// <summary>
/// Represents message template system names
/// </summary>
public static partial class CWMessageTemplateSystemNames
{
    #region Exchanges and Returns templates

    public const string ReturnReceipt = "Return.Receipt";
    public const string ReturnAuthorisedPaymentCustomer = "ReturnAuthorisedPayment.CustomerNotification";
    public const string ReturnAuthorisedExceptedCustomer = "ReturnAuthorisedExcepted.CustomerNotification";

    #endregion

    #region PackName templates

    /// <summary>
    /// The welcome e-mail of a customer registered by a manager or an import: packname details, assigned
    /// subsidies and a link to set the password
    /// </summary>
    public const string CustomerRegistrationImport = "Customer.Subsidy.CustomerRegistrationImport";

    #endregion

    #region Subsidy templates

    /// <summary>
    /// Subsidy expiry reminder, follow up #1: the earliest before a subsidy's season ends
    /// </summary>
    public const string SubsidyExpiryFollowUp1 = "Customer.Subsidy.ExpiryReminder.FollowUp1";

    /// <summary>
    /// Subsidy expiry reminder, follow up #2
    /// </summary>
    public const string SubsidyExpiryFollowUp2 = "Customer.Subsidy.ExpiryReminder.FollowUp2";

    /// <summary>
    /// Subsidy expiry reminder, follow up #3: the latest before a subsidy's season ends
    /// </summary>
    public const string SubsidyExpiryFollowUp3 = "Customer.Subsidy.ExpiryReminder.FollowUp3";

    #endregion
}