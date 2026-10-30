namespace Nop.Plugin.CW.Core.Services.PackNames.Bulk;

/// <summary>
/// A list packnames can be moved to
/// </summary>
/// <param name="PackNameListId">The list identifier</param>
/// <param name="Text">The list's name after its branch path</param>
public record PackNameBulkMoveTarget(int PackNameListId, string Text);