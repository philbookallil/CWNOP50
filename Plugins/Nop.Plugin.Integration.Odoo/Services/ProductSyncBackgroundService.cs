using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Nop.Plugin.Integration.Odoo.Domain;
using System.Threading.Channels;

namespace Nop.Plugin.Integration.Odoo.Services;

/// <summary>
/// A long-running background service that processes queued sync requests (products, branches,
/// stores, departments) from a <see cref="Channel{T}"/> one at a time, so the HTTP request returns
/// immediately. Each work item runs inside its own DI scope.
/// </summary>
public class OdooSyncBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly Channel<OdooSyncRequest> _channel;
    private readonly IOdooSyncQueue _syncQueue;
    private readonly ILogger<OdooSyncBackgroundService> _logger;

    public OdooSyncBackgroundService(
        IServiceScopeFactory scopeFactory,
        Channel<OdooSyncRequest> channel,
        IOdooSyncQueue syncQueue,
        ILogger<OdooSyncBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _channel = channel;
        _syncQueue = syncQueue;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var request in _channel.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                await ProcessRequestAsync(request);
            }
            catch (Exception ex)
            {
                // Reached only when the sync log itself failed (creating or completing the event);
                // sync failures are logged to the Odoo sync log inside ProcessRequestAsync. Never let
                // one request stop the worker, or no later sync of any type would run.
                _logger.LogError(ex, "Odoo {SyncType} sync request {RequestId} failed outside the sync log.",
                    request.SyncType, request.RequestId);
            }
            finally
            {
                // The type was claimed when the request was queued; a missed release would refuse
                // every later sync of this type until the application restarts.
                _syncQueue.SetSyncCompleted(request.SyncType);
            }
        }
    }

    private async Task ProcessRequestAsync(OdooSyncRequest request)
    {
        // Each sync gets its own scope so scoped services (EF DbContext, etc.) are fresh
        await using var scope = _scopeFactory.CreateAsyncScope();
        var logService = scope.ServiceProvider.GetRequiredService<IOdooLogService>();

        var eventName = request.SyncType switch
        {
            SyncEventType.Product => "Product Sync",
            SyncEventType.Branch => "Branch Sync",
            SyncEventType.Store => "Store Sync",
            SyncEventType.Department => "Department Sync",
            _ => "Unknown Sync"
        };

        var logEvent = await logService.CreateLogEventAsync(eventName, request.SyncType);
        var logEventId = logEvent.Id;

        // Statistics to store when completing the event
        int totalCreated = 0, totalUpdated = 0, totalSkipped = 0, totalProcessed = 0;

        try
        {
            await logService.InformationAsync(
                $"Background {eventName.ToLower()} started (RequestId: {request.RequestId})", logEventId);

            switch (request.SyncType)
            {
                case SyncEventType.Product:
                    var productSyncService = scope.ServiceProvider.GetRequiredService<IProductSyncService>();
                    var productResult = await productSyncService.SyncOdooProductsAsync(logEventId);
                    totalCreated = productResult.Created;
                    totalUpdated = productResult.Updated;
                    totalSkipped = productResult.Skipped;
                    totalProcessed = productResult.Processed;
                    break;

                case SyncEventType.Branch:
                    var branchSyncService = scope.ServiceProvider.GetRequiredService<IBranchSyncService>();
                    var result = await branchSyncService.SyncAllBranchesAsync(logEventId);
                    totalCreated = result.Created;
                    totalUpdated = result.Updated;
                    totalSkipped = result.Skipped;
                    totalProcessed = result.Processed;
                    break;

                case SyncEventType.Store:
                    var storeSyncService = scope.ServiceProvider.GetRequiredService<IStoreSyncService>();
                    var storeResult = await storeSyncService.SyncStoresAsync(logEventId);
                    totalCreated = storeResult.Created;
                    totalUpdated = storeResult.Updated;
                    totalSkipped = storeResult.Skipped;
                    totalProcessed = storeResult.Processed;
                    break;

                case SyncEventType.Department:
                    var departmentSyncService = scope.ServiceProvider.GetRequiredService<IDepartmentSyncService>();
                    var deptResult = await departmentSyncService.SyncDepartmentsAsync(logEventId);
                    totalCreated = deptResult.Created;
                    totalUpdated = deptResult.Updated;
                    totalSkipped = deptResult.Skipped;
                    totalProcessed = deptResult.Processed;
                    break;
            }

            await logService.InformationAsync(
                $"Background {eventName.ToLower()} completed (RequestId: {request.RequestId})", logEventId);
        }
        catch (Exception ex)
        {
            try
            {
                await logService.ErrorAsync(
                    $"Background {eventName.ToLower()} failed (RequestId: {request.RequestId}): {ex.Message}", logEventId,
                    fullMessage: ex.ToString());
            }
            catch
            {
                // Prevent logging failures from crashing the background loop
            }
        }
        finally
        {
            await logService.CompleteLogEventAsync(logEventId, totalCreated, totalUpdated, totalSkipped, totalProcessed);
        }
    }
}
