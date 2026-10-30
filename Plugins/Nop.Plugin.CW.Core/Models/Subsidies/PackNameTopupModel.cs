using Nop.Web.Framework.Models;

namespace Nop.Plugin.CW.Core.Models.Subsidies;

/// <summary>Root model for the Change Topup modal panel.</summary>
public record PackNameTopupModel : BaseNopModel
{
    #region Ctor

    public PackNameTopupModel()
    {
        Subsidies = new List<PackNameTopupSubsidyModel>();
    }

    #endregion

    #region Properties

    /// <summary>The PackName whose topups are being managed.</summary>
    public int PackNameId { get; set; }

    /// <summary>Display name of the PackName.</summary>
    public string PackNameName { get; set; } = string.Empty;

    public IList<PackNameTopupSubsidyModel> Subsidies { get; set; }

    #endregion
}
