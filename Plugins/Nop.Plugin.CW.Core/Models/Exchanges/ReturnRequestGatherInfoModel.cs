using Microsoft.AspNetCore.Mvc.Rendering;
using Nop.Plugin.CW.Core.Domain.Exchanges;
using Nop.Web.Framework.Models;
using Nop.Web.Models.Order;

namespace Nop.Plugin.CW.Core.Models.Exchanges;

public record ReturnRequestGatherInfoModel : BaseNopModel
{
    #region Ctor

    public ReturnRequestGatherInfoModel()
    {
        ReturnedItems = new List<ReturnedOrderItemModel>();
        EnabledExchangeModes = new List<ExchangeReturnMode>();
        EstimatedFees = new List<EstimatedFeeModel>();
    }

    #endregion

    #region Properties

    public int OrderId { get; set; }

    /// <summary>
    /// The order's number as the customer knows it
    /// </summary>
    public string CustomOrderNumber { get; set; }

    public string Comments { get; set; }

    public int ReturnRequestActionId { get; set; }

    public string ReturnRequestActionName { get; set; }

    public int ReturnRequestReasonId { get; set; }

    public string ReturnRequestReasonName { get; set; }

    public Guid UploadedFileGuid { get; set; }

    public IList<ReturnedOrderItemModel> ReturnedItems { get; set; }

    public IList<ExchangeReturnMode> EnabledExchangeModes { get; set; }

    public Guid ReturnRequestGroupId { get; set; }

    /// <summary>
    /// The draft return case this submission belongs to. Round-trips through the wizard's form posts.
    /// Zero when no case could be created — the legacy path still applies.
    /// </summary>
    public int ReturnCaseId { get; set; }

    /// <summary>
    /// The mode the customer chose. Set when redisplaying so the step reopens where they left it,
    /// rather than collapsing back to the mode buttons.
    /// </summary>
    public ExchangeReturnMode? Mode { get; set; }

    /// <summary>
    /// The fee rules that may apply, so the customer sees them before choosing rather than after.
    /// Amounts are per-rule; the total is not knowable until quantities are final.
    /// </summary>
    public IList<EstimatedFeeModel> EstimatedFees { get; set; }

    #endregion

    #region inner classes

    public record ReturnedOrderItemModel : BaseNopModel
    {
        public SubmitReturnRequestModel.OrderItemModel Item { get; set; }

        public SelectList AvailableExchangeTypes { get; set; }
        public int ExchangeTypeId { get; set; }

        public ReplacementProductModel ReplacementProduct { get; set; }

        public int ReturnedQuantity { get; set; }

        public decimal UnitPrice { get; set; }

        public string UnitPriceDisplay { get; set; }

    }

    public record ReplacementProductModel : BaseNopModel
    {
        public int ProductId { get; set; }

        public string AttributesInfo { get; set; }

        public string AttributesXml { get; set; }

        public string ProductName { get; set; }

        public string ProductSeName { get; set; }

        public string UnitPriceDisplay { get; set; }

        public decimal UnitPrice { get; set; }

        public IList<ReplacementProductAttrModel> Attributes { get; set; }

        /// <summary>
        /// Thumbnail of the chosen replacement, so a row restored after a validation failure looks
        /// the same as one the customer just picked
        /// </summary>
        public string PictureUrl { get; set; }

        public ReplacementProductModel()
        {
            Attributes = new List<ReplacementProductAttrModel>();
        }
    }

    public record ReplacementProductAttrModel : BaseNopModel
    {
        public int AttributeId { get; set; }

        public int AttributeValueId { get; set; }
    }

    /// <summary>One fee rule, already formatted for display</summary>
    public record EstimatedFeeModel : BaseNopModel
    {
        public string Concept { get; set; }

        public string AmountDisplay { get; set; }
    }

    #endregion
}