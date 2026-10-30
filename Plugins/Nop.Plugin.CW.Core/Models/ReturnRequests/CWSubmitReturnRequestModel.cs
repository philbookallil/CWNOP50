using Microsoft.AspNetCore.Mvc.Rendering;
using Nop.Web.Models.Order;

namespace Nop.Plugin.CW.Core.Models.ReturnRequests;

/// <summary>
/// Customization of the <see cref="SubmitReturnRequestModel"/> class from NOP
/// </summary>
public record CWSubmitReturnRequestModel : SubmitReturnRequestModel
{
    #region Ctor

    public CWSubmitReturnRequestModel()
    {
    }

    /// <summary>
    /// Wraps a NOP model, preserving any values already posted by the customer
    /// </summary>
    /// <remarks>nop's record copy constructor copies every value, those nop adds later included</remarks>
    public CWSubmitReturnRequestModel(SubmitReturnRequestModel model) : base(model)
    {
    }

    #endregion

    #region Properties

    /// <summary>
    /// Return reasons, ready to bind to a dropdown
    /// </summary>
    public IList<SelectListItem> ReturnReasonItems { get; set; } = new List<SelectListItem>();

    /// <summary>
    /// Return actions for the currently selected reason, ready to bind to a dropdown
    /// </summary>
    public IList<SelectListItem> ReturnActionItems { get; set; } = new List<SelectListItem>();

    /// <summary>
    /// Whether the store's return rules offer no reason or no action, so no return can be requested on the page
    /// </summary>
    public bool NoReturnOptions { get; set; }

    #endregion

    /// <summary>
    /// Customization of <see cref="SubmitReturnRequestModel.OrderItemModel"/> class from NOP
    /// </summary>
    public record CWOrderItemModel : SubmitReturnRequestModel.OrderItemModel
    {
        #region Ctor

        public CWOrderItemModel(SubmitReturnRequestModel.OrderItemModel model) : base(model)
        {
        }

        #endregion

        #region Properties

        public string Packname { get; set; }

        /// <summary>
        /// Thumbnail of the returned product, so the customer can see what they are sending back
        /// </summary>
        public string PictureUrl { get; set; }

        #endregion
    }
}
