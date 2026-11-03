using Nop.Core;
using Nop.Core.Domain.Logging;
using Nop.Plugin.Integration.Odoo.Domain;

namespace Nop.Plugin.Integration.Odoo.Services;

public interface IOdooLogService
{
    Task InsertLogAsync(LogLevel logLevel, int eventId, string shortMessage, string fullMessage = null,
        string entityType = null, int? odooEntityId = null, int? nopEntityId = null);

    Task<IPagedList<OdooLog>> GetAllLogsAsync(
        DateTime? fromUtc = null, DateTime? toUtc = null,
        string message = null, LogLevel? logLevel = null,
        int pageIndex = 0, int pageSize = int.MaxValue);

    Task<OdooLog> GetLogByIdAsync(int logId);

    Task DeleteLogAsync(OdooLog log);

    Task DeleteLogsAsync(IList<OdooLog> logs);

    Task ClearLogAsync();

    // Convenience methods
    Task InformationAsync(string shortMessage, int eventId, string fullMessage = null,
        string entityType = null, int? odooEntityId = null, int? nopEntityId = null);

    Task WarningAsync(string shortMessage, int eventId, string fullMessage = null,
        string entityType = null, int? odooEntityId = null, int? nopEntityId = null);

    Task ErrorAsync(string shortMessage, int eventId, string fullMessage = null,
        string entityType = null, int? odooEntityId = null, int? nopEntityId = null);

    // ── Log Event methods ────────────────────────────────────────────────

    /// <summary>
    /// Creates a new log event and sets it as the active event for all subsequent log calls
    /// within the current scope.
    /// </summary>
    Task<OdooLogEvent> CreateLogEventAsync(string name, SyncEventType eventType);

    /// <summary>
    /// Marks the log event as finished, stores summary statistics,
    /// and clears the active event for the current scope.
    /// </summary>
    Task CompleteLogEventAsync(int logEventId,
        int totalCreated = 0, int totalUpdated = 0,
        int totalSkipped = 0, int totalProcessed = 0);

    Task<OdooLogEvent> GetLogEventByIdAsync(int logEventId);

    Task<IPagedList<OdooLogEvent>> GetAllLogEventsAsync(
        SyncEventType? eventType = null,
        int pageIndex = 0, int pageSize = int.MaxValue);

    Task<IPagedList<OdooLog>> GetLogsByEventIdAsync(
        int logEventId,
        int pageIndex = 0, int pageSize = int.MaxValue);

    Task DeleteLogEventsAsync(IList<OdooLogEvent> logEvents);

    /// <summary>
    /// Returns trace count and whether errors exist for each event ID.
    /// </summary>
    Task<Dictionary<int, (int TraceCount, bool HasErrors)>> GetEventStatisticsAsync(IList<int> eventIds);
}
