using Nop.Plugin.CW.Core.Domain.PackNames.DTOs;

namespace Nop.Plugin.CW.Core.Services.PackNames.Bulk;

/// <summary>
/// The admin packname grids' bulk actions and Excel export
/// </summary>
public interface IPackNameBulkService
{
    /// <summary>
    /// Activates, deactivates, deletes or moves packnames of one store, with the rules of the single save and delete
    /// </summary>
    /// <remarks>
    /// The request is refused whole when it names no packname, more than <see cref="PackNameBulkDefaults.MaxPackNames"/>,
    /// or, for a move, a list that cannot take packnames. Otherwise every packname is checked and acted on in turn: one
    /// a rule refuses is skipped with its reason and the others go ahead. The writes run
    /// <see cref="PackNameBulkDefaults.WriteBatchSize"/> to a transaction; an error rolls back its batch and stops the
    /// action, and the batches before it stay.
    /// </remarks>
    /// <param name="request">The action</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains what the action did
    /// </returns>
    Task<PackNameBulkResult> ApplyAsync(PackNameBulkRequest request);

    /// <summary>
    /// Gets the lists of a store packnames can be moved to: switched on, in a branch that is switched on, by branch path
    /// </summary>
    /// <param name="storeId">Store identifier</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the lists, ordered by their text
    /// </returns>
    Task<IList<PackNameBulkMoveTarget>> GetMoveTargetsAsync(int storeId);

    /// <summary>
    /// Writes packnames of a store to an Excel workbook, one row each, with the columns of the admin grid the store shows
    /// plus the list, the account's e-mail and the active flag
    /// </summary>
    /// <param name="storeId">Store identifier; its settings decide the optional columns</param>
    /// <param name="packNames">The packnames, in the order to write them</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the workbook as an .xlsx file
    /// </returns>
    Task<byte[]> ExportToXlsxAsync(int storeId, IList<PackNameDTO> packNames);
}