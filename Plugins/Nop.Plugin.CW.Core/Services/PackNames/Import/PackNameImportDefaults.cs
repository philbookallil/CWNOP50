namespace Nop.Plugin.CW.Core.Services.PackNames.Import;

/// <summary>
/// The limits and fixed names of the packname Excel import
/// </summary>
public static class PackNameImportDefaults
{
    /// <summary>
    /// The most rows one file may hold; the import runs inside the request, so the file stays small
    /// </summary>
    public static int MaxRows => 500;

    /// <summary>
    /// The rows written per transaction. A batch holds its locks for about half a second, so the store's other pages
    /// wait at most that long while a file is imported, not for the whole file
    /// </summary>
    public static int WriteBatchSize => 10;

    /// <summary>
    /// The largest file accepted, in bytes
    /// </summary>
    public static long MaxFileBytes => 2 * 1024 * 1024;

    /// <summary>
    /// How dates are written in the template and read back when a cell holds text rather than a date
    /// </summary>
    public static string DateFormat => "yyyy-MM-dd";

    /// <summary>
    /// The Excel display format of the template's date columns
    /// </summary>
    public static string ExcelDateFormat => "yyyy-mm-dd";

    /// <summary>
    /// The name of the sheet the packnames are read from
    /// </summary>
    public static string DataSheetName => "PackNames";

    /// <summary>
    /// The name of the hidden sheet that holds the dropdown options of the template
    /// </summary>
    public static string OptionsSheetName => "Options";

    /// <summary>
    /// The header of the column the error report adds
    /// </summary>
    public static string ErrorsHeader => "Errors";

    /// <summary>
    /// The file name of the template
    /// </summary>
    public static string TemplateFileName => "PackNames.xlsx";

    /// <summary>
    /// The highest number of subsidy column groups a store may ask for
    /// </summary>
    public static int MaxSubsidyGroups => 10;

    /// <summary>
    /// The activity log type of an import a manager runs from the storefront
    /// </summary>
    public static string ManagerActivityLogType => "PublicStore.ImportPackNames";

    /// <summary>
    /// The activity log type of an import an administrator runs from the admin area
    /// </summary>
    public static string AdministratorActivityLogType => "ImportPackNames";
}