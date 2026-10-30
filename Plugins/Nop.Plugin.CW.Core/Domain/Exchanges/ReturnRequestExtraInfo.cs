namespace Nop.Plugin.CW.Core.Domain.Exchanges;

/// <summary>
/// Encapsulates extra information for a Return Request
/// </summary>
public class ReturnRequestExtraInfo
{
    #region Properties

    public string ReturnedItemSku { get; set; }

    public string ReturnedItemAttributes { get; set; }

    public string CustomerName { get; set; }

    public int OrderId { get; set; }

    #endregion
}
