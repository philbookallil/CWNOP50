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
}
