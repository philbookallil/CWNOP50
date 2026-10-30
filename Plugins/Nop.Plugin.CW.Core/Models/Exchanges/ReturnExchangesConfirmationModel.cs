using Nop.Web.Framework.Models;

namespace Nop.Plugin.CW.Core.Models.Exchanges;

public record ReturnExchangesConfirmationModel : BaseNopModel
{
    #region Ctor

    public ReturnExchangesConfirmationModel()
    {
        Items = new List<CustomerExchangeRequestModel>();
    }

    #endregion

    #region Properties

    /// <summary>
    /// When true the customer may dismiss the panel without printing
    /// </summary>
    public bool AllowCloseWithoutPrinting { get; set; }

    /// <summary>
    /// Each return request's heading: {0} its number, {1} its status (nop's wording, or its EU withdrawal wording)
    /// </summary>
    public string TitlePattern { get; set; }

    public IList<CustomerExchangeRequestModel> Items { get; set; }

    #endregion
}