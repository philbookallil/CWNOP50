using Nop.Plugin.CW.Core.Services.PackNames.Import;

namespace Nop.Plugin.CW.Core.Extensions;

/// <summary>
/// The answer the Excel import page's script reads, built from the result of an import
/// </summary>
public static class PackNameImportResultExtensions
{
    /// <summary>
    /// The result as the import page's script reads it. The names are written camel-cased here because nop serialises
    /// a response with the property names as they are written
    /// </summary>
    /// <param name="result">The result of the import</param>
    /// <returns>The object the page is answered with</returns>
    public static object ToPageAnswer(this PackNameImportResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        return new
        {
            success = result.Success,
            rowCount = result.RowCount,
            packNamesCreated = result.PackNamesCreated,
            customersCreated = result.CustomersCreated,
            welcomeEmailsQueued = result.WelcomeEmailsQueued,
            packNameListsCreated = result.PackNameListsCreated,
            fileErrors = result.FileErrors,
            rowErrors = result.RowErrors.Select(rowError => new { row = rowError.Row, messages = rowError.Messages }),
            hasErrorReport = result.HasErrorReport,
            firstRowNotImported = result.FirstRowNotImported,
            lists = result.Lists.Select(list => new { name = list.Name, count = list.Count })
        };
    }
}