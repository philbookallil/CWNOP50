using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;
using Nop.Web.Framework.Models;
using Nop.Web.Framework.Mvc.ModelBinding;

namespace Nop.Plugin.Integration.Odoo.Models;

/// <summary>
/// The filters of the Orders page.
/// </summary>
public partial record OdooOrderSearchModel : BaseSearchModel
{
    [NopResourceDisplayName("Integration.Odoo.Orders.List.Store")]
    public int StoreId { get; set; }

    [NopResourceDisplayName("Integration.Odoo.Orders.List.CreatedFrom")]
    [UIHint("DateNullable")]
    public DateTime? CreatedFrom { get; set; }

    [NopResourceDisplayName("Integration.Odoo.Orders.List.CreatedTo")]
    [UIHint("DateNullable")]
    public DateTime? CreatedTo { get; set; }

    /// <summary>
    /// An <c>OdooOrderSyncFilter</c>; 0 is all.
    /// </summary>
    [NopResourceDisplayName("Integration.Odoo.Orders.List.Status")]
    public int SyncState { get; set; }

    /// <summary>
    /// 0 all, otherwise a nop payment status.
    /// </summary>
    [NopResourceDisplayName("Integration.Odoo.Orders.List.PaymentStatus")]
    public int PaymentStatusId { get; set; }

    public IList<SelectListItem> AvailableStores { get; set; } = new List<SelectListItem>();

    public IList<SelectListItem> AvailableSyncStates { get; set; } = new List<SelectListItem>();

    public IList<SelectListItem> AvailablePaymentStatuses { get; set; } = new List<SelectListItem>();
}
