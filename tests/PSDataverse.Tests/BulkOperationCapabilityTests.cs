namespace PSDataverse.Tests;

using FluentAssertions;
using Newtonsoft.Json.Linq;

public class BulkOperationCapabilityTests
{
    [Fact]
    public async Task ConcurrentColdLookupsShareOneLoaderAndWarmLookupUsesCache()
    {
        var cache = new BulkOperationCapabilityCache();
        var loadCount = 0;
        async Task<BulkOperationCapabilities> LoadAsync()
        {
            Interlocked.Increment(ref loadCount);
            await Task.Delay(25);
            return new BulkOperationCapabilities(
                "new_example", "new_examples", true, true, DateTimeOffset.UtcNow);
        }

        var results = await Task.WhenAll(Enumerable.Range(0, 12).Select(_ =>
            cache.GetAsync(BulkOperationCapabilityCache.ForLogicalName("new_example"), false, LoadAsync)));
        var warm = await cache.GetAsync(
            BulkOperationCapabilityCache.ForLogicalName("NEW_EXAMPLE"), false, LoadAsync);

        loadCount.Should().Be(1);
        results.Should().OnlyContain(result => result.Capabilities.CreateMultiple);
        results.Count(result => !result.FromCache).Should().Be(1);
        warm.FromCache.Should().BeTrue();
    }

    [Fact]
    public async Task TableSetAliasUsesCachedLogicalNameResultAndRefreshReplacesAllAliases()
    {
        var cache = new BulkOperationCapabilityCache();
        var loadCount = 0;
        Task<BulkOperationCapabilities> LoadAsync()
        {
            var current = Interlocked.Increment(ref loadCount);
            return Task.FromResult(new BulkOperationCapabilities(
                "new_example", "new_examples", current == 1, true, DateTimeOffset.UtcNow));
        }

        _ = await cache.GetAsync(
            BulkOperationCapabilityCache.ForLogicalName("new_example"), false, LoadAsync);
        var alias = await cache.GetAsync(
            BulkOperationCapabilityCache.ForTableSetName("new_examples"), false, LoadAsync);
        var refreshed = await cache.GetAsync(
            BulkOperationCapabilityCache.ForTableSetName("new_examples"), true, LoadAsync);
        var refreshedAlias = await cache.GetAsync(
            BulkOperationCapabilityCache.ForLogicalName("new_example"), false, LoadAsync);

        alias.FromCache.Should().BeTrue();
        refreshed.FromCache.Should().BeFalse();
        refreshed.Capabilities.CreateMultiple.Should().BeFalse();
        refreshedAlias.FromCache.Should().BeTrue();
        refreshedAlias.Capabilities.CreateMultiple.Should().BeFalse();
        loadCount.Should().Be(2);
    }

    [Fact]
    public async Task FailedLookupIsNotCached()
    {
        var cache = new BulkOperationCapabilityCache();
        var loadCount = 0;
        Task<BulkOperationCapabilities> LoadAsync()
        {
            Interlocked.Increment(ref loadCount);
            throw new InvalidOperationException("metadata unavailable");
        }

        var action = () => cache.GetAsync(
            BulkOperationCapabilityCache.ForLogicalName("account"), false, LoadAsync);

        await action.Should().ThrowAsync<InvalidOperationException>();
        await action.Should().ThrowAsync<InvalidOperationException>();
        loadCount.Should().Be(2);
    }

    [Fact]
    public async Task RefreshedResultCannotBeOverwrittenByOlderInflightAliases()
    {
        var cache = new BulkOperationCapabilityCache();
        var oldStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseOld = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task<BulkOperationCapabilities> LoadOldAsync()
        {
            oldStarted.SetResult();
            await releaseOld.Task;
            return new BulkOperationCapabilities(
                "new_example", "new_examples", true, true, DateTimeOffset.UtcNow);
        }

        var oldLookup = cache.GetAsync(
            BulkOperationCapabilityCache.ForLogicalName("new_example"), false, LoadOldAsync);
        await oldStarted.Task;
        var refreshed = await cache.GetAsync(
            BulkOperationCapabilityCache.ForLogicalName("new_example"),
            true,
            () => Task.FromResult(new BulkOperationCapabilities(
                "new_example", "new_examples", false, true, DateTimeOffset.UtcNow)));
        releaseOld.SetResult();
        _ = await oldLookup;
        var aliasLoaderCalled = false;
        var alias = await cache.GetAsync(
            BulkOperationCapabilityCache.ForTableSetName("new_examples"),
            false,
            () =>
            {
                aliasLoaderCalled = true;
                return Task.FromResult(new BulkOperationCapabilities(
                    "new_example", "new_examples", true, true, DateTimeOffset.UtcNow));
            });

        refreshed.Capabilities.CreateMultiple.Should().BeFalse();
        alias.Capabilities.CreateMultiple.Should().BeFalse();
        aliasLoaderCalled.Should().BeFalse();
    }

    [Fact]
    public void CapabilityResponseIsParsed()
    {
        var json = JObject.Parse("""
            {
              "value": [
                { "sdkmessageid": { "name": "CreateMultiple" } },
                { "sdkmessageid": { "name": "UpdateMultiple" } }
              ]
            }
            """);
        var checkedAt = DateTimeOffset.UtcNow;

        var parsed = TestDataverseBulkOperationSupportCmdlet.ParseCapabilities(
            json, "account", "accounts", checkedAt);

        parsed.TableLogicalName.Should().Be("account");
        parsed.TableSetName.Should().Be("accounts");
        parsed.CreateMultiple.Should().BeTrue();
        parsed.UpdateMultiple.Should().BeTrue();
        parsed.CheckedAt.Should().Be(checkedAt);
    }

    [Fact]
    public void UpsertSupportRequiresBothCreateAndUpdateMessages()
    {
        var capabilities = new BulkOperationCapabilities(
            "account", "accounts", true, false, DateTimeOffset.UtcNow);

        var result = TestDataverseBulkOperationSupportCmdlet.CreateResult(
            capabilities, true, "UpsertMultiple");

        result.Supported.Should().BeFalse();
        result.UpsertMultiple.Should().BeFalse();
        result.FromCache.Should().BeTrue();
    }

    [Fact]
    public async Task DisposedCacheRejectsFurtherLookups()
    {
        var cache = new BulkOperationCapabilityCache();
        cache.Dispose();

        var action = () => cache.GetAsync(
            BulkOperationCapabilityCache.ForLogicalName("account"),
            false,
            () => Task.FromResult(new BulkOperationCapabilities(
                "account", "accounts", true, true, DateTimeOffset.UtcNow)));

        await action.Should().ThrowAsync<ObjectDisposedException>();
    }
}
