using Nop.Web.Framework.Models;

namespace Nop.Plugin.CW.Core.Models.PackNames;

/// <summary>
/// One page of the packnames the storefront selector offers for a selector entry
/// </summary>
public record PackNameSelectorPageModel : BaseNopModel
{
    #region Properties

    /// <summary>
    /// Gets or sets the packnames of the page, ordered by name
    /// </summary>
    public List<PackNameModel> PackNames { get; set; } = new();

    /// <summary>
    /// Gets or sets whether the entry (or, for a search, the matches) holds packnames the page does not show
    /// </summary>
    public bool HasMore { get; set; }

    #endregion
}