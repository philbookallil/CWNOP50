namespace Nop.Plugin.CW.Core.Services.Messages;

/// <summary>
/// Class that contains custom Token Group Names for tokens used in the CW Message Templates
/// </summary>
public static class CWTokenGroupNames
{
    /// <summary>
    /// Represents tokens with information about CW Exchanges and Returns
    /// </summary>
    public const string ExchangeReturnsTokens = "Exchanges and Returns tokens";

    /// <summary>
    /// Represents tokens with information about the packname a customer was registered for and its subsidies
    /// </summary>
    public const string SubsidyTokens = "Subsidy tokens";

    /// <summary>
    /// Represents the token naming the packname a returned item was ordered for
    /// </summary>
    public const string ReturnPackNameTokens = "Return packname tokens";

    /// <summary>
    /// Represents the tokens of the subsidy expiry reminders: the seasons ending and the days left
    /// </summary>
    public const string SubsidyExpiryTokens = "Subsidy expiry tokens";
}