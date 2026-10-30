namespace Nop.Plugin.CW.Core.Services.PackNames.Import;

/// <summary>
/// The errors of one row of the import file
/// </summary>
public class PackNameImportRowError
{
    /// <summary>
    /// The Excel row number
    /// </summary>
    public int Row { get; init; }

    public IList<string> Messages { get; init; } = new List<string>();
}

/// <summary>
/// What an import did, or why it did nothing
/// </summary>
public class PackNameImportResult
{
    /// <summary>
    /// Whether every row was imported. A file with any row that fails its checks imports nothing
    /// </summary>
    public bool Success => FileErrors.Count == 0 && RowErrors.Count == 0;

    /// <summary>
    /// The rows the file holds, header excluded
    /// </summary>
    public int RowCount { get; set; }

    public int PackNamesCreated { get; set; }

    public int CustomersCreated { get; set; }

    /// <summary>
    /// The new accounts whose welcome e-mail was queued; none when the store's welcome message is switched off
    /// </summary>
    public int WelcomeEmailsQueued { get; set; }

    /// <summary>
    /// The packname lists created for branches without an active list
    /// </summary>
    public int PackNameListsCreated { get; set; }

    /// <summary>
    /// Problems with the file as a whole: unreadable, too many rows, a required column missing, values in a column the
    /// store no longer takes
    /// </summary>
    public IList<string> FileErrors { get; } = new List<string>();

    /// <summary>
    /// The rows that failed validation, in row order
    /// </summary>
    public IList<PackNameImportRowError> RowErrors { get; } = new List<PackNameImportRowError>();

    /// <summary>
    /// Whether a row passed every check but was refused, or failed, while it was being written. Its batch and the rows
    /// after it were not imported; the error report, which repeats the checks, would not show that refusal
    /// </summary>
    public bool RefusedOnWrite { get; set; }

    /// <summary>
    /// The first row not imported when the import stopped part way: the rows above it were imported and stay. Null when
    /// the import finished, or stopped before anything was imported
    /// </summary>
    public int? FirstRowNotImported { get; set; }

    /// <summary>
    /// Whether an error report can be built: the file was readable and some rows failed their checks
    /// </summary>
    public bool HasErrorReport => RowErrors.Count > 0 && !RefusedOnWrite;

    /// <summary>
    /// How many packnames each list received, by branch and list name, from the rows imported
    /// </summary>
    public IList<PackNameImportListCount> Lists { get; } = new List<PackNameImportListCount>();
}

/// <summary>
/// The packnames an import put into one list
/// </summary>
public class PackNameImportListCount
{
    /// <summary>
    /// The branch and list, as "Branch › List"
    /// </summary>
    public string Name { get; init; }

    public int Count { get; set; }
}