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

    /// <summary>
    /// ReturnRequestItem.ApprovalQueuedEmailId, on a database installed before the column was in the schema
    /// </summary>
    public const string ReturnRequestItemApprovalQueuedEmailId = "2026/11/06 12:00:00";

    /// <summary>
    /// CORE_SUB_SubsidyReminder, on a database installed before the table was in the schema
    /// </summary>
    public const string SubsidyReminder = "2026/11/09 12:00:00";

    /// <summary>
    /// The order approval tables (CORE_APR_*), on a database installed before they were in the schema
    /// </summary>
    public const string OrderApproval = "2026/11/17 12:00:00";

    /// <summary>
    /// The unique index of the approval rules, on a database installed before the index was in the schema
    /// </summary>
    public const string ApprovalRuleUniqueScope = "2026/11/24 12:00:00";

    /// <summary>
    /// The packname → customer key that clears the link, on a database installed when the key refused the customer's delete
    /// </summary>
    public const string PackNameCustomerForeignKey = "2026/11/25 12:00:00";
}