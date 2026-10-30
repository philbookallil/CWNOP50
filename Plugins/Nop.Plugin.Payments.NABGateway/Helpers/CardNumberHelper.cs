namespace Nop.Plugin.Payments.NABGateway.Helpers;

/// <summary>
/// Small helpers for working with credit card numbers
/// </summary>
public static class CardNumberHelper
{
    /// <summary>
    /// Removes whitespace and visual separators from a credit card number.
    /// </summary>
    /// <param name="cardNumber">The card number as entered by the user</param>
    /// <returns>A digits-only string (empty if input is null/empty)</returns>
    public static string Sanitize(string cardNumber)
    {
        if (string.IsNullOrWhiteSpace(cardNumber))
            return string.Empty;

        return new string(cardNumber.Where(char.IsDigit).ToArray());
    }
}
