using System.Reflection;
using System.Runtime.CompilerServices;
using AwesomeAssertions;
using Nop.Core.Caching;
using Nop.Plugin.CW.Core.Services.Orders;
using NUnit.Framework;

namespace CW.Tests.Core.Services.Orders;

/// <summary>
/// The packname lock chain that settlement runs under (<c>RunWithPackNameLocksAsync</c>). A checkout over several
/// packnames takes one lock per packname, in ascending id order, nested; the settlement runs only when every lock
/// is held. nop's locker refuses a held lock at once rather than waiting, and it takes a <c>Func&lt;Task&gt;</c>, so
/// a refusal further down the chain is invisible to the outer lock unless the chain carries it out. When it did
/// not, a checkout over packnames A and B with B held by another order reported success with nothing settled.
/// These tests hold one of the locks and check that the chain says "not acquired" and the settlement never runs,
/// which is what lets the caller refuse the order with "try again" instead of placing it half-done.
/// </summary>
/// <remarks>
/// The service is built without its constructor (it has dozens of dependencies and the chain uses only the
/// locker), and the protected method is called through reflection.
/// </remarks>
[TestFixture]
public class PackNameLockChainTests
{
    private FakeLocker _locker;
    private OrderProcessingOverrideService _service;
    private int _settlementRuns;

    [SetUp]
    public void SetUp()
    {
        _locker = new FakeLocker();
        _settlementRuns = 0;

        _service = (OrderProcessingOverrideService)RuntimeHelpers.GetUninitializedObject(typeof(OrderProcessingOverrideService));
        typeof(OrderProcessingOverrideService)
            .GetField("_locker", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(_service, _locker);
    }

    private Task<bool> RunAsync(params int[] packNameIds)
    {
        var method = typeof(OrderProcessingOverrideService)
            .GetMethod("RunWithPackNameLocksAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;

        return (Task<bool>)method.Invoke(_service, new object[] { packNameIds.AsEnumerable(), (Func<Task>)Settle })!;
    }

    private Task Settle()
    {
        _settlementRuns++;
        return Task.CompletedTask;
    }

    [Test]
    public async Task RunWithPackNameLocks_AllLocksFree_SettlesOnceUnderEveryLock()
    {
        var acquired = await RunAsync(7, 3, 7);

        acquired.Should().BeTrue();
        _settlementRuns.Should().Be(1);
        _locker.Taken.Should().Equal("subsidy_apply_packname_3", "subsidy_apply_packname_7");
        _locker.HeldDuringAction.Should().BeEquivalentTo("subsidy_apply_packname_3", "subsidy_apply_packname_7");
        _locker.Held.Should().BeEmpty("every lock is released afterwards");
    }

    [Test]
    public async Task RunWithPackNameLocks_InnerLockHeldByAnotherOrder_ReportsNotAcquiredAndDoesNotSettle()
    {
        _locker.Held.Add("subsidy_apply_packname_7");

        var acquired = await RunAsync(3, 7);

        acquired.Should().BeFalse("the lock on the second packname was refused, so the settlement did not run");
        _settlementRuns.Should().Be(0);
        _locker.Held.Should().BeEquivalentTo(new[] { "subsidy_apply_packname_7" }, "the outer lock is released, the other order keeps its own");
    }

    [Test]
    public async Task RunWithPackNameLocks_OuterLockHeldByAnotherOrder_ReportsNotAcquiredAndDoesNotTryTheRest()
    {
        _locker.Held.Add("subsidy_apply_packname_3");

        var acquired = await RunAsync(3, 7);

        acquired.Should().BeFalse();
        _settlementRuns.Should().Be(0);
        _locker.Taken.Should().BeEmpty("the chain stops at the first refusal");
    }

    [Test]
    public async Task RunWithPackNameLocks_NoPackNames_SettlesWithoutLocking()
    {
        var acquired = await RunAsync();

        acquired.Should().BeTrue();
        _settlementRuns.Should().Be(1);
        _locker.Taken.Should().BeEmpty();
    }

    /// <summary>
    /// Behaves like nop's distributed and memory lockers for this purpose: a held key is refused at once
    /// (false, action not run); a free key is held while the action runs and released afterwards.
    /// </summary>
    private sealed class FakeLocker : ILocker
    {
        public HashSet<string> Held { get; } = [];
        public List<string> Taken { get; } = [];
        public HashSet<string> HeldDuringAction { get; } = [];

        public async Task<bool> PerformActionWithLockAsync(string resource, TimeSpan expirationTime, Func<Task> action)
        {
            if (!Held.Add(resource))
                return false;

            Taken.Add(resource);

            try
            {
                HeldDuringAction.UnionWith(Held);
                await action();
                return true;
            }
            finally
            {
                Held.Remove(resource);
            }
        }

        public Task RunWithHeartbeatAsync(string key, TimeSpan expirationTime, TimeSpan heartbeatInterval,
            Func<CancellationToken, Task> action, CancellationTokenSource cancellationTokenSource = default) =>
            throw new NotSupportedException();

        public Task CancelTaskAsync(string key, TimeSpan expirationTime) => throw new NotSupportedException();

        public Task<bool> IsTaskRunningAsync(string key) => throw new NotSupportedException();
    }
}
