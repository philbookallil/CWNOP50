namespace Nop.Plugin.CW.Core.Services.PackNames.Bulk;

/// <summary>
/// What a bulk action does to the packnames it is given
/// </summary>
public enum PackNameBulkAction
{
    /// <summary>
    /// Switches the packnames on
    /// </summary>
    Activate = 1,

    /// <summary>
    /// Switches the packnames off
    /// </summary>
    Deactivate = 2,

    /// <summary>
    /// Deletes the packnames, as the single delete does: their subsidy assignments and cart lines go with them
    /// </summary>
    Delete = 3,

    /// <summary>
    /// Moves the packnames to another list of the same store
    /// </summary>
    Move = 4
}