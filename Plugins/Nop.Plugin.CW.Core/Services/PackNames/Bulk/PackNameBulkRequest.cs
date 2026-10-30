namespace Nop.Plugin.CW.Core.Services.PackNames.Bulk;

/// <summary>
/// A bulk action on packnames of one store
/// </summary>
/// <param name="StoreId">The store the packnames must belong to; a packname of another store is skipped</param>
/// <param name="Action">What to do</param>
/// <param name="PackNameIds">The packnames to act on</param>
/// <param name="TargetPackNameListId">The list to move them to; only read for <see cref="PackNameBulkAction.Move"/></param>
public record PackNameBulkRequest(int StoreId, PackNameBulkAction Action, IReadOnlyList<int> PackNameIds, int TargetPackNameListId = 0);