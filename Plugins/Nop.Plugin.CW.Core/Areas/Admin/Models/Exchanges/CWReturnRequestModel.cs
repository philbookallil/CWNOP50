using Microsoft.AspNetCore.Mvc.Rendering;
using Nop.Plugin.CW.Core.Domain.Exchanges;
using Nop.Plugin.CW.Core.Models.Exchanges.FeeRule;
using Nop.Web.Areas.Admin.Models.Orders;
using Nop.Web.Framework.Mvc.ModelBinding;

namespace Nop.Plugin.CW.Core.Areas.Admin.Models.Exchanges;

public record CWReturnRequestModel : ReturnRequestModel
{
    #region Ctor

    public CWReturnRequestModel()
    {

    }

    public CWReturnRequestModel(ReturnRequestModel requestData)
    {
        AttributeInfo = requestData.AttributeInfo;
        CreatedOn = requestData.CreatedOn;
        CustomerComments = requestData.CustomerComments;
        CustomerId = requestData.CustomerId;
        CustomerInfo = requestData.CustomerInfo;
        CustomNumber = requestData.CustomNumber;
        CustomOrderNumber = requestData.CustomOrderNumber;
        CustomProperties = requestData.CustomProperties;
        Id = requestData.Id;
        OrderId = requestData.OrderId;
        ProductId = requestData.ProductId;
        ProductName = requestData.ProductName;
        Quantity = requestData.Quantity;
        ReturnedQuantity = requestData.ReturnedQuantity;
        ReasonForReturn = requestData.ReasonForReturn;
        RequestedAction = requestData.RequestedAction;
        ReturnRequestStatusId = requestData.ReturnRequestStatusId;
        ReturnRequestStatusStr = requestData.ReturnRequestStatusStr;
        StaffNotes = requestData.StaffNotes;
        UploadedFileGuid = requestData.UploadedFileGuid;
    }

    #endregion

    #region Properties

    [NopResourceDisplayName("Plugins.CW.Core.Admin.Returns.Fields.StaffShipQty")]
    public int StaffShipQty { get; set; }

    [NopResourceDisplayName("Plugins.CW.Core.Admin.Returns.Fields.StaffRepackQty")]
    public int StaffRepackQty { get; set; }

    [NopResourceDisplayName("Plugins.CW.Core.Admin.Returns.Fields.ProductPrice")]
    public decimal ProductPrice { get; set; }

    [NopResourceDisplayName("Plugins.CW.Core.Admin.Returns.Fields.EnableAdditionalFee")]
    public bool EnableAdditionalFee { get; set; }

    [NopResourceDisplayName("Plugins.CW.Core.Admin.Returns.Fields.AdditionalFeeName")]
    public string AdditionalFeeName { get; set; }

    [NopResourceDisplayName("Plugins.CW.Core.Admin.Returns.Fields.AdditionalFeeAmount")]
    public decimal? AdditionalFeeAmount { get; set; }

    [NopResourceDisplayName("Plugins.CW.Core.Admin.Returns.Fields.DispositionReason")]
    public string DispositionReason { get; set; }

    public int ReturnCaseId { get; set; }

    public string ReturnCaseNumber { get; set; }

    /// <summary>
    /// Exchange or Credit Note
    /// </summary>
    public int ExchangeModeId { get; set; }

    public ExchangeReturnMode ExchangeReturnMode
    {
        get
        {
            return (ExchangeReturnMode)this.ExchangeModeId;
        }
    }

    [NopResourceDisplayName("Plugins.CW.Core.Admin.Returns.Fields.ExchangeMode")]
    public string ExchangeModeName { get; set; }

    /// <summary>
    /// Same Style, Different Style, Exact Replacement
    /// </summary>
    public int ExchangeTypeId { get; set; }

    public ExchangeType ExchangeType
    {
        get
        {
            return (ExchangeType)this.ExchangeTypeId;
        }
    }

    [NopResourceDisplayName("Plugins.CW.Core.Admin.Returns.Fields.ExchangeType")]
    public string ExchangeTypeName { get; set; }

    public int ReplacementProductId { get; set; }

    [NopResourceDisplayName("Plugins.CW.Core.Admin.Returns.Fields.ReplacementProduct")]
    public string ReplacementProductName { get; set; }

    [NopResourceDisplayName("Plugins.CW.Core.Admin.Returns.Fields.ReplacementOptions")]
    public string ReplacementProductAttrInfo { get; set; }

    [NopResourceDisplayName("Plugins.CW.Core.Admin.Returns.Fields.ReplacementPrice")]
    public decimal ReplacementProductPrice { get; set; }

    [NopResourceDisplayName("Plugins.CW.Core.Admin.Returns.Fields.ReasonForReturn")]
    public int ReturnRequestReasonId { get; set; }

    [NopResourceDisplayName("Plugins.CW.Core.Admin.Returns.Fields.RequestedAction")]
    public int ReturnRequestActionId { get; set; }

    public SelectList ReturnReasonsList { get; set; }

    public SelectList ReturnActionsList { get; set; }

    public FeeConceptItem ShippingFeeConfig { get; set; }

    public FeeConceptItem RepackagingFeeConfig { get; set; }

    /// <summary>
    /// What this return request is actually charged for redelivery, priced over its own Re-Shipped
    /// Quantity. Zero when no redelivery rule applies to it, or when staff zeroed that quantity.
    /// </summary>
    public decimal ShippingFeeAmount { get; set; }

    /// <summary>
    /// The quantity the redelivery fee was priced over — this return request's staff quantity
    /// </summary>
    public int ShippingFeeQuantity { get; set; }

    /// <summary>
    /// What this return is actually charged for repackaging
    /// </summary>
    public decimal RepackagingFeeAmount { get; set; }

    /// <summary>
    /// What this return is actually charged as an ad-hoc staff fee. Equal to
    /// <see cref="AdditionalFeeAmount"/> while the case is unresolved; once it has resolved this is the
    /// frozen charge, and the editable field above no longer moves the money.
    /// </summary>
    public decimal AdditionalFeeChargedAmount { get; set; }

    /// <summary>
    /// The credit note actually raised for this return request, or null when the case has not resolved
    /// yet. Zero-or-null on a resolved case means no note was raised, which is the normal outcome for a
    /// subsidy-funded line: its value went back to the employee's entitlement instead.
    /// The panel must show this rather than deriving a note from the returned value.
    /// </summary>
    public decimal? CreditNoteRaisedAmount { get; set; }

    public int OriginalRequestStatusId { get; set; }

    /// <summary>
    /// Whether to show a redelivery row: a live rule applies to this return, or a charge was frozen for
    /// it. Gating on the rule alone would hide a charge that is still being collected — the frozen row is
    /// what the customer pays, and a store turning the rule off afterwards does not refund it.
    /// </summary>
    public bool ShowShippingFee => ShippingFeeConfig != null || ShippingFeeAmount > decimal.Zero;

    /// <summary>
    /// Whether to show a repackaging row — see <see cref="ShowShippingFee"/>.
    /// </summary>
    public bool ShowRepackagingFee => RepackagingFeeConfig != null || RepackagingFeeAmount > decimal.Zero;

    public bool NeedsCompletion { get; set; }

    public bool IsEditable { get; set; }

    public bool IsCompleted { get; set; }

    /// <summary>
    /// The return was rejected or cancelled: nothing is credited, exchanged or charged for it
    /// </summary>
    public bool IsRejected { get; set; }

    public int StoreId { get; set; }

    [NopResourceDisplayName("Plugins.CW.Core.Admin.Returns.Fields.Store")]
    public string StoreName { get; set; }

    public int BranchId { get; set; }

    [NopResourceDisplayName("Plugins.CW.Core.Admin.Returns.Fields.Branch")]
    public string BranchName { get; set; }

    [NopResourceDisplayName("Plugins.CW.Core.Admin.Returns.Fields.Packname")]
    public string Packname { get; set; }

    [NopResourceDisplayName("Plugins.CW.Core.Admin.Returns.Fields.CustomerName")]
    public string CustomerName { get; set; }

    //Odoo ERP Number
    public string ErpNumber { get; set; }

    /// <summary>
    /// The other items returned in the same parcel, so staff can see the whole return
    /// </summary>
    public IList<CaseLineModel> CaseLines { get; set; } = new List<CaseLineModel>();

    public record CaseLineModel
    {
        public int ReturnRequestId { get; set; }

        public string ProductName { get; set; }

        public int Quantity { get; set; }

        public string Mode { get; set; }

        public string Disposition { get; set; }

        public bool IsCurrent { get; set; }
    }

    #endregion
}