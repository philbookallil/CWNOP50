using Nop.Web.Framework.Models;
using Nop.Web.Framework.Mvc.ModelBinding;

namespace Nop.Plugin.Payments.NABGateway.Areas.Admin.Models;

/// <summary>
/// Display model for a single NAB Transact XML transaction (grid row + detail modal).
/// </summary>
public record NABTransactionModel : BaseNopEntityModel
{
    [NopResourceDisplayName("Plugins.Payments.NABGateway.Reports.Fields.OrderId")]
    public int OrderId { get; set; }

    [NopResourceDisplayName("Plugins.Payments.NABGateway.Reports.Fields.OrderGuid")]
    public string OrderGuid { get; set; }

    public int CustomerId { get; set; }

    [NopResourceDisplayName("Plugins.Payments.NABGateway.Reports.Fields.UserEmail")]
    public string UserEmail { get; set; }

    [NopResourceDisplayName("Plugins.Payments.NABGateway.Reports.Fields.UserName")]
    public string UserFullName { get; set; }

    [NopResourceDisplayName("Plugins.Payments.NABGateway.Reports.Fields.Approved")]
    public bool Approved { get; set; }

    [NopResourceDisplayName("Plugins.Payments.NABGateway.Reports.Fields.MessageId")]
    public string MessageId { get; set; }

    [NopResourceDisplayName("Plugins.Payments.NABGateway.Reports.Fields.TxnId")]
    public string TxnId { get; set; }

    [NopResourceDisplayName("Plugins.Payments.NABGateway.Reports.Fields.AuthId")]
    public string AuthId { get; set; }

    [NopResourceDisplayName("Plugins.Payments.NABGateway.Reports.Fields.ResponseCode")]
    public string ResponseCode { get; set; }

    [NopResourceDisplayName("Plugins.Payments.NABGateway.Reports.Fields.ResponseText")]
    public string ResponseText { get; set; }

    [NopResourceDisplayName("Plugins.Payments.NABGateway.Reports.Fields.SettlementDate")]
    public string SettlementDate { get; set; }

    [NopResourceDisplayName("Plugins.Payments.NABGateway.Reports.Fields.MaskedPan")]
    public string MaskedPan { get; set; }

    [NopResourceDisplayName("Plugins.Payments.NABGateway.Reports.Fields.CardType")]
    public string CardType { get; set; }

    [NopResourceDisplayName("Plugins.Payments.NABGateway.Reports.Fields.Amount")]
    public string Amount { get; set; }

    [NopResourceDisplayName("Plugins.Payments.NABGateway.Reports.Fields.PurchaseOrderNo")]
    public string PurchaseOrderNo { get; set; }

    [NopResourceDisplayName("Plugins.Payments.NABGateway.Reports.Fields.RawResponse")]
    public string RawResponseXml { get; set; }

    [NopResourceDisplayName("Plugins.Payments.NABGateway.Reports.Fields.CreatedOn")]
    public string CreatedOn { get; set; }
}
