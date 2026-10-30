using Nop.Web.Areas.Admin.Models.Orders;

namespace Nop.Plugin.CW.Core.Areas.Admin.Models.Exchanges;

/// <summary>
/// Custom class to customize <see cref="ReturnRequestSearchModel"/>
/// </summary>
public record CWReturnRequestSearchModel : ReturnRequestSearchModel
{
    #region Properties

    /// <summary>
    /// The customer's first name
    /// </summary>
    public string FirstName { get; set; }

    /// <summary>
    /// The customer's last name
    /// </summary>
    public string LastName { get; set; }

    #endregion

    #region Ctor

    public CWReturnRequestSearchModel() : base()
    {

    }

    public CWReturnRequestSearchModel(ReturnRequestSearchModel model) : base()
    {
        this.AvailablePageSizes = model.AvailablePageSizes;
        this.CustomNumber = model.CustomNumber;
        this.CustomProperties = model.CustomProperties;
        this.EndDate = model.EndDate;
        this.ReturnRequestStatusId = model.ReturnRequestStatusId;
        this.ReturnRequestStatusList = model.ReturnRequestStatusList;
        this.StartDate = model.StartDate;
    }

    #endregion
}
