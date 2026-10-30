using Nop.Web.Framework.Models;

namespace Nop.Plugin.CW.Core.Models.Subsidies;

public record SubsidyBarModel : BaseNopModel
{
    #region Ctor

    public SubsidyBarModel()
    {
        ProductGroups = new List<SubsidyCatalogProductGroupModel>();
    }

    #endregion

    #region Properties

    public string PackNameName { get; set; }

    public string PackNameColour { get; set; }

    public string PackNameBranch { get; set; }

    public string PackNameDepartment { get; set; }

    public string PackNameCostCenter { get; set; }

    public string PackNamePOEntry { get; set; }

    public bool PackNameIsGroup { get; set; }

    public bool PackNameIsCustomer { get; set; }

    public bool PackNameIsManager { get; set; }

    // --- Global credit totals (across all subsidies, excluding product-group credit) ---
    public decimal TotalCredit { get; set; }

    public decimal AvailableCredit { get; set; }

    public decimal RemainCredit { get; set; }

    public decimal ShoppingCartCredit { get; set; }

    public string RemainCreditFormatted { get; set; }

    public string ShoppingCartCreditFormatted { get; set; }

    public IList<SubsidyCatalogProductGroupModel> ProductGroups { get; set; }

    public bool CanEditSubsidyTopup { get; set; }

    public bool IsFlyout { get; set; }

    #endregion
}
