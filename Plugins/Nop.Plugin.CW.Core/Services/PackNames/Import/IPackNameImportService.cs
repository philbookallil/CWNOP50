using Nop.Core.Domain.Customers;
using Nop.Plugin.CW.Core.Domain.PackNames;

namespace Nop.Plugin.CW.Core.Services.PackNames.Import;

/// <summary>
/// The Excel import of packnames, for a store's managers on the storefront and for administrators in the admin area:
/// a template whose columns follow the store's settings, a file checked row by row and imported whole or not at all,
/// and an error report
/// </summary>
public interface IPackNameImportService
{
    /// <summary>
    /// The columns a manager's file has, and the data the rows are checked against
    /// </summary>
    /// <param name="manager">The manager's own packname</param>
    /// <param name="storeId">The manager's store</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the layout
    /// </returns>
    Task<PackNameImportLayout> BuildLayoutAsync(PackName manager, int storeId);

    /// <summary>
    /// The columns an administrator's file has, and the data the rows are checked against: every active branch of the
    /// store, and the account and subsidy columns whatever the store lets its managers do
    /// </summary>
    /// <param name="storeId">The store the packnames are imported into</param>
    /// <param name="administrator">The administrator who imports the file</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the layout
    /// </returns>
    Task<PackNameImportLayout> BuildAdministratorLayoutAsync(int storeId, Customer administrator);

    /// <summary>
    /// Checks an uploaded file before it is opened: present, an .xlsx, within the size limit
    /// </summary>
    /// <param name="fileName">The name of the uploaded file; null when nothing was uploaded</param>
    /// <param name="length">The size of the uploaded file, in bytes</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the message to show, or null when the file may be read
    /// </returns>
    Task<string> CheckFileAsync(string fileName, long length);

    /// <summary>
    /// The empty template for the layout: a header row, dropdowns for the columns that have options, date formats
    /// </summary>
    /// <param name="layout">The layout</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the workbook as bytes
    /// </returns>
    Task<byte[]> GenerateTemplateAsync(PackNameImportLayout layout);

    /// <summary>
    /// Checks every row of the file and, when all of them pass, creates the packnames, their customer accounts,
    /// addresses and subsidies in batches, each in its own transaction. A file with any error creates nothing; a row
    /// refused while it is written stops the import after the batches already committed
    /// </summary>
    /// <param name="layout">The layout</param>
    /// <param name="file">The uploaded workbook</param>
    /// <param name="languageId">The language of the welcome e-mails</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains what was created, or the errors
    /// </returns>
    Task<PackNameImportResult> ImportAsync(PackNameImportLayout layout, Stream file, int languageId);

    /// <summary>
    /// Checks every row of the file without importing, and returns a copy of the file with an Errors column that
    /// names each row's problems
    /// </summary>
    /// <param name="layout">The layout</param>
    /// <param name="file">The uploaded workbook</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the report as bytes, or null when the file could not be read
    /// </returns>
    Task<byte[]> BuildErrorReportAsync(PackNameImportLayout layout, Stream file);
}