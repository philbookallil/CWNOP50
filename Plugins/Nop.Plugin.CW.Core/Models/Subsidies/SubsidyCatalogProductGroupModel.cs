using Nop.Web.Framework.Models;
using Nop.Web.Models.Catalog;

namespace Nop.Plugin.CW.Core.Models.Subsidies;

public record SubsidyCatalogProductGroupModel : BaseNopModel
{
    #region Ctor

    public SubsidyCatalogProductGroupModel()
    {
        Products = new List<ProductOverviewModel>();
    }

    #endregion

    #region Properties

    // ProductGroupId
    public int Id { get; set; } 

    public string Name { get; set; }

    public string Icon { get; set; }

    public string Colour { get; set; }

    public int DisplayOrder { get; set; }

    // Quantities
    public int TotalQuantity { get; set; }
    public int AvailableQuantity { get; set; }

    // Credits
    public decimal AvailableCredit { get; set; }

    /// <summary>
    /// The packname's global credit left once the cart's use of it is taken off; it can still pay for this group's
    /// products after the group's own units and credit are used up
    /// </summary>
    public decimal RemainGlobalCredit { get; set; }

    /// <summary>
    /// The money the group allocates in this period, credit plus top-ups; zero on a units-only group.
    /// </summary>
    public decimal TotalCredit { get; set; }

    // Cart simulation results
    public int ShoppingCartQuantity { get; set; }
    public decimal ShoppingCartCredit { get; set; }

    // Derived display values
    public decimal RemainCredit { get; set; }
    public string RemainCreditFormatted { get; set; }
    public string ShoppingCartCreditFormatted { get; set; }

    // Products to display
    public string ViewMode { get; set; }

    public IList<ProductOverviewModel> Products { get; set; }

    #endregion
}
