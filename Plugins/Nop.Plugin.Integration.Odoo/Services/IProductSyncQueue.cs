using Nop.Plugin.Integration.Odoo.Domain;
using System.Collections.Concurrent;
using System.Threading.Channels;

namespace Nop.Plugin.Integration.Odoo.Services;

/// <summary>
/// Represents a queued sync request with a type discriminator.
/// </summary>
public record OdooSyncRequest(Guid RequestId, DateTime QueuedAtUtc, SyncEventType SyncType);

/// <summary>
/// Thin abstraction over the sync channel so controllers don't depend on the channel directly.
/// A sync type is claimed when it is queued and released when the background service finishes it,
/// so a second request of the same type is refused while one is waiting OR running.
/// </summary>
public interface IOdooSyncQueue
{
    /// <summary>
    /// Claims the sync type and queues a request in one atomic step.
    /// </summary>
    /// <returns>The request ID, or null when a sync of that type is already queued or running.</returns>
    ValueTask<Guid?> TryEnqueueSyncAsync(SyncEventType syncType, CancellationToken cancellationToken = default);

    /// <summary>
    /// Releases the sync type. Called by the background service when processing finishes, whatever
    /// the outcome.
    /// </summary>
    void SetSyncCompleted(SyncEventType syncType);
}

public class OdooSyncQueue : IOdooSyncQueue
{
    private readonly Channel<OdooSyncRequest> _channel;

    // Sync types currently queued or running; the key's presence is the claim, the value is unused.
    private readonly ConcurrentDictionary<SyncEventType, byte> _claimed = new();

    public OdooSyncQueue(Channel<OdooSyncRequest> channel)
    {
        _channel = channel;
    }

    public async ValueTask<Guid?> TryEnqueueSyncAsync(SyncEventType syncType, CancellationToken cancellationToken = default)
    {
        // TryAdd is atomic: of two simultaneous requests for the same type, exactly one claims it.
        if (!_claimed.TryAdd(syncType, 0))
            return null;

        try
        {
            var request = new OdooSyncRequest(Guid.NewGuid(), DateTime.UtcNow, syncType);
            await _channel.Writer.WriteAsync(request, cancellationToken);
            return request.RequestId;
        }
        catch
        {
            // Nothing was queued, so nothing will ever release the claim — release it here.
            SetSyncCompleted(syncType);
            throw;
        }
    }

    public void SetSyncCompleted(SyncEventType syncType) => _claimed.TryRemove(syncType, out _);
}
