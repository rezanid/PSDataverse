namespace PSDataverse.Tests;

using FluentAssertions;
using PSDataverse.Dataverse.Execute;

public class BoundedChannelSchedulerTests
{
    [Fact]
    public async Task BoundsConcurrentHandlers()
    {
        var active = 0;
        var maximum = 0;
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var scheduler = new BoundedChannelScheduler<int, int>(
            capacity: 6,
            degreeOfParallelism: 3,
            async (value, cancellationToken) =>
            {
                var current = Interlocked.Increment(ref active);
                UpdateMaximum(ref maximum, current);
                await release.Task.WaitAsync(cancellationToken);
                _ = Interlocked.Decrement(ref active);
                return value;
            },
            CancellationToken.None);

        for (var value = 0; value < 6; value++)
        {
            await scheduler.EnqueueAsync(value);
        }
        await WaitUntilAsync(() => Volatile.Read(ref maximum) == 3);
        release.SetResult();
        scheduler.Complete();

        var results = await ReadAllAsync(scheduler);

        results.Should().HaveCount(6);
        results.Should().OnlyContain(result => result.IsSuccess);
        maximum.Should().Be(3);
    }

    [Fact]
    public async Task AppliesBackpressureWhenCapacityIsFull()
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var scheduler = new BoundedChannelScheduler<int, int>(
            capacity: 1,
            degreeOfParallelism: 1,
            async (value, cancellationToken) =>
            {
                started.TrySetResult();
                await release.Task.WaitAsync(cancellationToken);
                return value;
            },
            CancellationToken.None);

        await scheduler.EnqueueAsync(1);
        await started.Task;
        await scheduler.EnqueueAsync(2);
        var blockedWrite = scheduler.EnqueueAsync(3).AsTask();

        blockedWrite.IsCompleted.Should().BeFalse();
        release.SetResult();
        await blockedWrite;
        scheduler.Complete();
        (await ReadAllAsync(scheduler)).Should().HaveCount(3);
    }

    [Fact]
    public async Task StreamsCompletionOrderWithStableInputSequence()
    {
        await using var scheduler = new BoundedChannelScheduler<int, int>(
            capacity: 2,
            degreeOfParallelism: 2,
            async (value, cancellationToken) =>
            {
                await Task.Delay(value == 0 ? 80 : 5, cancellationToken);
                return value;
            },
            CancellationToken.None);

        await scheduler.EnqueueAsync(0);
        await scheduler.EnqueueAsync(1);
        scheduler.Complete();

        var results = await ReadAllAsync(scheduler);

        results.Select(result => result.Value).Should().Equal(1, 0);
        results.Select(result => result.Sequence).Should().Equal(1, 0);
    }

    [Fact]
    public async Task CapturesItemFailureWithoutStoppingOtherWork()
    {
        await using var scheduler = new BoundedChannelScheduler<int, int>(
            capacity: 2,
            degreeOfParallelism: 1,
            (value, _) => value == 1
                ? Task.FromException<int>(new InvalidOperationException("failed item"))
                : Task.FromResult(value),
            CancellationToken.None);

        await scheduler.EnqueueAsync(1);
        await scheduler.EnqueueAsync(2);
        scheduler.Complete();

        var results = await ReadAllAsync(scheduler);

        results[0].Error.Should().BeOfType<InvalidOperationException>();
        results[1].Value.Should().Be(2);
    }

    [Fact]
    public async Task AppliesDegreeOfParallelismHintToSubsequentWork()
    {
        var active = 0;
        var maximum = 0;
        await using var scheduler = new BoundedChannelScheduler<int, int>(
            capacity: 6,
            degreeOfParallelism: 3,
            async (value, cancellationToken) =>
            {
                var current = Interlocked.Increment(ref active);
                UpdateMaximum(ref maximum, current);
                await Task.Delay(20, cancellationToken);
                _ = Interlocked.Decrement(ref active);
                return value;
            },
            CancellationToken.None,
            value => value == 0 ? 1 : null);

        await scheduler.EnqueueAsync(0);
        var first = await scheduler.Results.ReadAsync();
        first.Value.Should().Be(0);
        scheduler.DegreeOfParallelism.Should().Be(1);
        Volatile.Write(ref maximum, 0);

        for (var value = 1; value <= 5; value++)
        {
            await scheduler.EnqueueAsync(value);
        }
        scheduler.Complete();

        (await ReadAllAsync(scheduler)).Should().HaveCount(5);
        maximum.Should().Be(1);
    }

    private static async Task<List<ScheduledResult<int>>> ReadAllAsync(
        BoundedChannelScheduler<int, int> scheduler)
    {
        var results = new List<ScheduledResult<int>>();
        await foreach (var result in scheduler.Results.ReadAllAsync())
        {
            results.Add(result);
        }
        return results;
    }

    private static void UpdateMaximum(ref int maximum, int candidate)
    {
        int observed;
        do
        {
            observed = Volatile.Read(ref maximum);
            if (candidate <= observed)
            {
                return;
            }
        }
        while (Interlocked.CompareExchange(ref maximum, candidate, observed) != observed);
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        while (!condition())
        {
            await Task.Delay(5, timeout.Token);
        }
    }
}
