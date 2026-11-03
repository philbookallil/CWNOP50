using Nop.Core;
using Nop.Core.Domain.Logging;
using Nop.Data;
using Nop.Plugin.Integration.Odoo.Domain;

namespace Nop.Plugin.Integration.Odoo.Services;

public class OdooLogService : IOdooLogService
{
    #region Fields

    private readonly IRepository<OdooLog> _odooLogRepository;
    private readonly IRepository<OdooLogEvent> _odooLogEventRepository;

    #endregion

    #region Ctor

    public OdooLogService(
        IRepository<OdooLog> odooLogRepository,
        IRepository<OdooLogEvent> odooLogEventRepository)
    {
        _odooLogRepository = odooLogRepository;
        _odooLogEventRepository = odooLogEventRepository;
    }

    #endregion

    #region Log CRUD

    public async Task InsertLogAsync(LogLevel logLevel, int eventId, string shortMessage, string fullMessage = null,
        string entityType = null, int? odooEntityId = null, int? nopEntityId = null)
    {
        var log = new OdooLog
        {
            LogLevel = logLevel,
            ShortMessage = shortMessage ?? string.Empty,
            FullMessage = fullMessage,
            EntityType = entityType,
            OdooEntityId = odooEntityId,
            NopEntityId = nopEntityId,
            LogEventId = eventId,
            CreatedOnUtc = DateTime.UtcNow
        };

        await _odooLogRepository.InsertAsync(log, false);
    }

    public async Task<IPagedList<OdooLog>> GetAllLogsAsync(
        DateTime? fromUtc = null, DateTime? toUtc = null,
        string message = null, LogLevel? logLevel = null,
        int pageIndex = 0, int pageSize = int.MaxValue)
    {
        return await _odooLogRepository.GetAllPagedAsync(query =>
        {
            if (fromUtc.HasValue)
                query = query.Where(l => l.CreatedOnUtc >= fromUtc.Value);

            if (toUtc.HasValue)
                query = query.Where(l => l.CreatedOnUtc <= toUtc.Value);

            if (logLevel.HasValue)
                query = query.Where(l => l.LogLevelId == (int)logLevel.Value);

            if (!string.IsNullOrEmpty(message))
                query = query.Where(l => l.ShortMessage.Contains(message) ||
                                         (l.FullMessage != null && l.FullMessage.Contains(message)));

            query = query.OrderByDescending(l => l.CreatedOnUtc);

            return query;
        }, pageIndex, pageSize);
    }

    public async Task<OdooLog> GetLogByIdAsync(int logId)
    {
        return await _odooLogRepository.GetByIdAsync(logId);
    }

    public async Task DeleteLogAsync(OdooLog log)
    {
        await _odooLogRepository.DeleteAsync(log, false);
    }

    public async Task DeleteLogsAsync(IList<OdooLog> logs)
    {
        await _odooLogRepository.DeleteAsync(logs, false);
    }

    public async Task ClearLogAsync()
    {
        await _odooLogRepository.TruncateAsync();
        await _odooLogEventRepository.TruncateAsync();
    }

    #endregion

    #region Convenience methods

    public async Task InformationAsync(string shortMessage, int eventId, string fullMessage = null,
        string entityType = null, int? odooEntityId = null, int? nopEntityId = null)
    {
        await InsertLogAsync(LogLevel.Information, eventId, shortMessage, fullMessage, entityType, odooEntityId, nopEntityId);
    }

    public async Task WarningAsync(string shortMessage, int eventId, string fullMessage = null,
        string entityType = null, int? odooEntityId = null, int? nopEntityId = null)
    {
        await InsertLogAsync(LogLevel.Warning, eventId, shortMessage, fullMessage, entityType, odooEntityId, nopEntityId);
    }

    public async Task ErrorAsync(string shortMessage, int eventId, string fullMessage = null,
        string entityType = null, int? odooEntityId = null, int? nopEntityId = null)
    {
        await InsertLogAsync(LogLevel.Error, eventId, shortMessage, fullMessage, entityType, odooEntityId, nopEntityId);
    }

    #endregion

    #region Log Event methods

    public async Task<OdooLogEvent> CreateLogEventAsync(string name, SyncEventType eventType)
    {
        var logEvent = new OdooLogEvent
        {
            Name = name,
            EventType = eventType,
            StartedOnUtc = DateTime.UtcNow
        };

        await _odooLogEventRepository.InsertAsync(logEvent, false);

        // Set as active event for all subsequent InsertLogAsync calls in this scope
        //_currentLogEventId = logEvent.Id;

        return logEvent;
    }

    public async Task CompleteLogEventAsync(int logEventId,
        int totalCreated = 0, int totalUpdated = 0,
        int totalSkipped = 0, int totalProcessed = 0)
    {
        var logEvent = await _odooLogEventRepository.GetByIdAsync(logEventId);
        if (logEvent != null)
        {
            logEvent.FinishedOnUtc = DateTime.UtcNow;
            logEvent.TotalCreated = totalCreated;
            logEvent.TotalUpdated = totalUpdated;
            logEvent.TotalSkipped = totalSkipped;
            logEvent.TotalProcessed = totalProcessed;
            await _odooLogEventRepository.UpdateAsync(logEvent, false);
        }

        //_currentLogEventId = null;
    }

    public async Task<OdooLogEvent> GetLogEventByIdAsync(int logEventId)
    {
        return await _odooLogEventRepository.GetByIdAsync(logEventId);
    }

    public async Task<IPagedList<OdooLogEvent>> GetAllLogEventsAsync(
        SyncEventType? eventType = null,
        int pageIndex = 0, int pageSize = int.MaxValue)
    {
        return await _odooLogEventRepository.GetAllPagedAsync(query =>
        {
            if (eventType.HasValue)
                query = query.Where(e => e.EventTypeId == (int)eventType.Value);

            query = query.OrderByDescending(e => e.StartedOnUtc);

            return query;
        }, pageIndex, pageSize);
    }

    public async Task<IPagedList<OdooLog>> GetLogsByEventIdAsync(
        int logEventId,
        int pageIndex = 0, int pageSize = int.MaxValue)
    {
        return await _odooLogRepository.GetAllPagedAsync(query =>
        {
            query = query.Where(l => l.LogEventId == logEventId);
            query = query.OrderByDescending(l => l.CreatedOnUtc);
            return query;
        }, pageIndex, pageSize);
    }

    public async Task DeleteLogEventsAsync(IList<OdooLogEvent> logEvents)
    {
        foreach (var logEvent in logEvents)
        {
            // Cascade-delete all traces belonging to this event
            var traces = await _odooLogRepository.GetAllAsync(query =>
                query.Where(l => l.LogEventId == logEvent.Id));

            if (traces.Any())
                await _odooLogRepository.DeleteAsync(traces, false);
        }

        await _odooLogEventRepository.DeleteAsync(logEvents, false);
    }

    public async Task<Dictionary<int, (int TraceCount, bool HasErrors)>> GetEventStatisticsAsync(IList<int> eventIds)
    {
        var result = new Dictionary<int, (int TraceCount, bool HasErrors)>();

        foreach (var id in eventIds)
        {
            var traceCount = await _odooLogRepository.Table
                .CountAsync(l => l.LogEventId == id);
            var hasErrors = await _odooLogRepository.Table
                .AnyAsync(l => l.LogEventId == id && l.LogLevelId >= (int)LogLevel.Error);

            result[id] = (traceCount, hasErrors);
        }

        return result;
    }

    #endregion
}
