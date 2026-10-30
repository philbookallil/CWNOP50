namespace Nop.Plugin.CW.Core.Data;

/// <summary>
/// Version stamps of the CW.Core migrations (accepted formats: yyyy/MM/dd HH:mm:ss, yyyy-MM-dd HH:mm:ss, yyyy.MM.dd HH:mm:ss)
/// </summary>
public static class MigrationVersions
{
    /// <summary>
    /// The CW.Core schema: every table and the indexes their builders do not declare
    /// </summary>
    public const string InitialSchema = "2026/10/23 12:00:00";
}