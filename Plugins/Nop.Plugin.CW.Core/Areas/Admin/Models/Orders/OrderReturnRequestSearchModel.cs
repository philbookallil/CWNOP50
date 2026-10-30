using Nop.Web.Framework.Models;

namespace Nop.Plugin.CW.Core.Areas.Admin.Models.Orders;

/// <summary>
/// Composite model for the CW content injected into the admin order-details block
/// (AdminWidgetZones.OrderDetailsBlock): the credit-notes-applied summary and the return-requests grid.
/// </summary>
public partial record OrderReturnRequestSearchModel : BaseSearchModel
{
    public int OrderId { get; set; }

    /// <summary>True when the return-requests grid should be shown (ADMIN_ACCESS_EXCHANGES).</summary>
    public bool ShowReturns { get; set; }

    /// <summary>Formatted total credit applied to this order, or null when none / not permitted.</summary>
    public string CreditNotesApplied { get; set; }
}