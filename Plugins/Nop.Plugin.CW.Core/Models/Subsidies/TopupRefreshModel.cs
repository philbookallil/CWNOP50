namespace Nop.Plugin.CW.Core.Models.Subsidies;

/// <summary>
/// Lightweight JSON model for AJAX topup refresh.
/// Id = SubsidyPackNameId.
/// </summary>
public record TopupRefreshModel
{
    #region Ctor

    public TopupRefreshModel()
    {
        ProductGroups = new List<TopupProductGroupRefreshModel>();
    }

    #endregion

    #region Properties

    /// <summary>SubsidyPackNameId</summary>
    public int Id { get; set; }

    /// <summary>Formatted total topup credit for this subsidy (e.g. "$50.00").</summary>
    public string Credit { get; set; } = string.Empty;

    public IList<TopupProductGroupRefreshModel> ProductGroups { get; set; }

    #endregion
}

/// <summary>
/// Per-product-group topup values for AJAX refresh.
/// Id = ProductGroupId.
/// </summary>
public record TopupProductGroupRefreshModel
{
    #region Properties

    public int Id { get; set; }

    public string Credit { get; set; } = string.Empty;

    public int Qty { get; set; }

    #endregion
}