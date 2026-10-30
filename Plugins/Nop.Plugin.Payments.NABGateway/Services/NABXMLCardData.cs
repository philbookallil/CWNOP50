namespace Nop.Plugin.Payments.NABGateway.Services;

/// <summary>
/// Card details entered on the payment-info step. Never persisted — see <see cref="NABXMLCardDataStore"/>.
/// </summary>
/// <param name="Number">Card number, digits only</param>
/// <param name="HolderName">Cardholder name</param>
/// <param name="ExpireMonth">Expiry month (1–12)</param>
/// <param name="ExpireYear">Expiry year (four digits)</param>
/// <param name="Cvv">Card verification value</param>
public record NABXMLCardData(string Number, string HolderName, int ExpireMonth, int ExpireYear, string Cvv);
