using System.Threading.Channels;
using AwesomeAssertions;
using Nop.Plugin.Integration.Odoo.Domain;
using Nop.Plugin.Integration.Odoo.Services;
using NUnit.Framework;

namespace CW.Tests.Integration.Odoo;

/// <summary>
/// The sync queue claims a sync type when it is queued and releases it when the background service finishes,
/// so the same sync cannot be queued twice while one waits or runs; and the tally counts every record once.
/// </summary>
[TestFixture]
public class SyncQueueTests
{
    #region Fields

    private Channel<OdooSyncRequest> _channel = null!;
    private OdooSyncQueue _queue = null!;

    #endregion

    #region Setup

    [SetUp]
    public void SetUp()
    {
        _channel = Channel.CreateBounded<OdooSyncRequest>(new BoundedChannelOptions(10) { FullMode = BoundedChannelFullMode.Wait });
        _queue = new OdooSyncQueue(_channel);
    }

    #endregion

    #region Queue

    [Test]
    public async Task Enqueue_writes_a_request_of_that_type()
    {
        var id = await _queue.TryEnqueueSyncAsync(SyncEventType.Store);

        id.Should().NotBeNull();
        _channel.Reader.TryRead(out var request).Should().BeTrue();
        request!.RequestId.Should().Be(id!.Value);
        request.SyncType.Should().Be(SyncEventType.Store);
    }

    [Test]
    public async Task A_second_request_of_the_same_type_is_refused_until_completed()
    {
        (await _queue.TryEnqueueSyncAsync(SyncEventType.Product)).Should().NotBeNull();

        (await _queue.TryEnqueueSyncAsync(SyncEventType.Product)).Should().BeNull();

        _queue.SetSyncCompleted(SyncEventType.Product);
        (await _queue.TryEnqueueSyncAsync(SyncEventType.Product)).Should().NotBeNull();
    }

    [Test]
    public async Task Different_types_queue_independently()
    {
        (await _queue.TryEnqueueSyncAsync(SyncEventType.Store)).Should().NotBeNull();
        (await _queue.TryEnqueueSyncAsync(SyncEventType.Department)).Should().NotBeNull();
        (await _queue.TryEnqueueSyncAsync(SyncEventType.Branch)).Should().NotBeNull();
    }

    [Test]
    public async Task A_failed_write_releases_the_claim()
    {
        _channel.Writer.Complete();

        var act = async () => await _queue.TryEnqueueSyncAsync(SyncEventType.Branch);

        await act.Should().ThrowAsync<ChannelClosedException>();

        // Had the claim been kept, the second attempt would be refused (null) instead of trying the write again.
        await act.Should().ThrowAsync<ChannelClosedException>();
    }

    [Test]
    public async Task A_failed_write_leaves_the_type_free_on_the_same_queue()
    {
        var channel = Channel.CreateBounded<OdooSyncRequest>(1);
        var queue = new OdooSyncQueue(channel);
        (await queue.TryEnqueueSyncAsync(SyncEventType.Store)).Should().NotBeNull();

        // The channel is full: the write waits, then is cancelled.
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));
        var act = async () => await queue.TryEnqueueSyncAsync(SyncEventType.Product, cts.Token);
        await act.Should().ThrowAsync<OperationCanceledException>();

        channel.Reader.TryRead(out _);
        (await queue.TryEnqueueSyncAsync(SyncEventType.Product)).Should().NotBeNull();
    }

    #endregion

    #region Tally

    [Test]
    public void Tally_counts_each_outcome_once()
    {
        var tally = new SyncTally();

        tally.Add(SyncOutcome.Created);
        tally.Add(SyncOutcome.Updated);
        tally.Add(SyncOutcome.Updated);
        tally.Add(SyncOutcome.Skipped);

        tally.Created.Should().Be(1);
        tally.Updated.Should().Be(2);
        tally.Skipped.Should().Be(1);
    }

    [Test]
    public void Tally_counts_the_default_outcome_as_skipped()
    {
        var tally = new SyncTally();

        tally.Add(default);

        tally.Skipped.Should().Be(1);
        (tally.Created + tally.Updated).Should().Be(0);
    }

    #endregion
}
