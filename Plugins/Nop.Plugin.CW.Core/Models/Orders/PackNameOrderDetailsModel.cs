using Nop.Plugin.CW.Core.Models.Subsidies;
using Nop.Web.Framework.Models;
using Nop.Web.Models.Order;

namespace Nop.Plugin.CW.Core.Models.Orders;

public record PackNameOrderDetailsModel : BaseNopModel
{
    public bool HasPackNameItems => Groups.Count > 0;

    public bool ShowProductThumbnail { get; set; }

    public IList<PackNameOrderGroupModel> Groups { get; set; } = new List<PackNameOrderGroupModel>();

    /// <summary>Null when neither subsidy nor store credit was applied to this order.</summary>
    public PackNameOrderSettlementModel SettlementTotals { get; set; }

    public bool HasSettlement => SettlementTotals != null;

    /// <summary>The order page's print version: no links</summary>
    public bool PrintMode { get; set; }

    /// <summary>Whether the SKU column shows, as on nop's order page</summary>
    public bool ShowSku { get; set; } = true;

    /// <summary>Whether the vendor column shows, as on nop's order page</summary>
    public bool ShowVendorName { get; set; }

    /// <summary>
    /// nop's own lines of the order, by order item id: their download and licence links, rental dates and vendor; empty
    /// where the order page's model is not at hand (the packname completed page)
    /// </summary>
    public IDictionary<int, OrderDetailsModel.OrderItemModel> OrderItems { get; set; } = new Dictionary<int, OrderDetailsModel.OrderItemModel>();
}

public record PackNameOrderGroupModel : BaseNopModel
{
    public string PackName { get; set; }

    public string Branch { get; set; }

    public string BranchCode { get; set; }

    public string Department { get; set; }

    public string CostCenter { get; set; }

    public string Colour { get; set; }

    public string ForeColor { get; set; }

    public string PackNameIconClass { get; set; }

    public IList<PackNameOrderItemModel> Items { get; set; } = new List<PackNameOrderItemModel>();

    // Shipping
    public bool IsSharedShipping { get; set; }

    public IList<string> SharedWithPackNames { get; set; } = new List<string>();

    public string ShippingMethod { get; set; }

    public bool PickupInStore { get; set; }

    public string AddressName { get; set; }

    public string AddressLine1 { get; set; }

    public string AddressLine2 { get; set; }

    public string AddressCityStateZip { get; set; }

    public string AddressCountry { get; set; }

    public string AddressPhone { get; set; }
}

public record PackNameOrderItemModel : BaseNopModel
{
    public int OrderItemId { get; set; }

    public string ProductName { get; set; }

    public string ProductSeName { get; set; }

    public string AttributeInfo { get; set; }

    public string Sku { get; set; }

    public int Quantity { get; set; }

    public string UnitPrice { get; set; }

    public string SubTotal { get; set; }

    public string PictureUrl { get; set; }

    public string PictureAlternateText { get; set; }

    /// <summary>The subsidy that paid for this line, one row per product group (or one for global credit).</summary>
    public IList<SubsidyItemBreakdownModel> SubsidyBreakdown { get; set; } = new List<SubsidyItemBreakdownModel>();
}