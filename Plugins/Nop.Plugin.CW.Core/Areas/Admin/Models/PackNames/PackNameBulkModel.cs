using Nop.Web.Framework.Models;

namespace Nop.Plugin.CW.Core.Areas.Admin.Models.PackNames;

/// <summary>
/// What a packname grid posts for a bulk action
/// </summary>
public record PackNameBulkModel : BaseNopModel
{
    #region Properties

    /// <summary>
    /// Gets or sets the store of the grid
    /// </summary>
    public int StoreId { get; set; }

    /// <summary>
    /// Gets or sets the action: Activate, Deactivate, Delete or Move
    /// </summary>
    public string BulkAction { get; set; }

    /// <summary>
    /// Gets or sets the ticked packnames, comma separated
    /// </summary>
    public string PackNameIds { get; set; }

    /// <summary>
    /// Gets or sets the list to move the packnames to; only for a move
    /// </summary>
    public int TargetPackNameListId { get; set; }

    #endregion
}