using Nop.Core.Domain.Tax;
using Nop.Web.Framework.Models;

namespace Nop.Plugin.CW.Core.Areas.Admin.Models.Orders;

public record AdminPackNameOrderDetailsModel : BaseNopModel
{
    public bool HasPackNameItems => Groups.Count > 0;

    public TaxDisplayType TaxDisplayType { get; set; }

    public bool AllowCustomersToSelectTaxDisplayType { get; set; }

    public IList<AdminPackNameOrderGroupModel> Groups { get; set; } = new List<AdminPackNameOrderGroupModel>();

    public AdminPackNameOrderSettlementModel SettlementTotals { get; set; }

    public bool HasSettlement => SettlementTotals != null;
}

public record AdminPackNameOrderGroupModel : BaseNopModel
{
    public int PackNameId { get; set; }

    public string PackName { get; set; }

    public string Branch { get; set; }

    public string BranchCode { get; set; }

    public string Department { get; set; }

    public string CostCenter { get; set; }

    public bool IsSharedShipping { get; set; }

    public IList<string> SharedWithPackNames { get; set; } = new List<string>();

    public string ShippingMethod { get; set; }

    public string ShippingStatus { get; set; }

    public bool PickupInStore { get; set; }

    // Address fields matching admin's typical address display
    public string AddressName { get; set; }

    public string AddressLine1 { get; set; }

    public string AddressLine2 { get; set; }

    public string AddressCityStateZip { get; set; }

    public string AddressCountry { get; set; }

    public string AddressPhone { get; set; }


    public decimal ShippingInclTax { get; set; }

    public string ShippingInclTaxFormatted { get; set; }

    public string ShippingExclTaxFormatted { get; set; }

    /// <summary>Formatted stock subsidy for this PackName's items (incl tax). Null when no subsidy.</summary>
    public string SubsidyForStockFormatted { get; set; }

    /// <summary>Formatted shipping subsidy for this PackName (incl tax). Null when no shipping subsidy.</summary>
    public string SubsidyForShippingFormatted { get; set; }

    /// <summary>Formatted total subsidy for this PackName (stock + shipping, incl tax).</summary>
    public string SubsidyTotalFormatted { get; set; }

    public bool HasSubsidy { get; set; }

    public bool HasShippingSubsidy { get; set; }

    public IList<AdminPackNameOrderItemModel> Items { get; set; } = new List<AdminPackNameOrderItemModel>();
}

public record AdminPackNameOrderItemModel : BaseNopModel
{
    public int ProductId { get; set; }

    public string PictureThumbnailUrl { get; set; }

    public string ProductName { get; set; }

    public string AttributeInfo { get; set; }

    public string Sku { get; set; }

    public int Quantity { get; set; }

    public string UnitPriceInclTax { get; set; }

    public string UnitPriceExclTax { get; set; }

    public string SubTotalInclTax { get; set; }

    public string SubTotalExclTax { get; set; }
}
